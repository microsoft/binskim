// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Sarif;

using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// Result of running BinSkim as an external process.
    /// </summary>
    public sealed class BinSkimRunResult
    {
        public int ExitCode { get; init; }
        public string StdOut { get; init; }
        public string StdErr { get; init; }
        public string SarifOutputPath { get; init; }

        /// <summary>
        /// Deserializes the SARIF output file if it exists.
        /// Returns null if no output path was specified or the file was not created.
        /// </summary>
        public SarifLog LoadSarifLog()
        {
            if (string.IsNullOrEmpty(SarifOutputPath) || !File.Exists(SarifOutputPath))
            {
                return null;
            }

            string json = File.ReadAllText(SarifOutputPath);
            return JsonConvert.DeserializeObject<SarifLog>(json);
        }
    }

    /// <summary>
    /// Launches BinSkim as an external process via "dotnet BinSkim.dll" for cross-platform compatibility.
    /// Captures stdout, stderr, exit code, and optionally deserializes produced SARIF output.
    /// </summary>
    public static class BinSkimRunner
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Resolves the path to the built BinSkim.dll relative to the test assembly location.
        /// Layout: bld/bin/Test.IntegrationTests.BinSkim.Driver/release/ → ../../BinSkim.Driver/release/BinSkim.dll
        /// </summary>
        public static string GetBinSkimDllPath()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string binSkimDll = Path.GetFullPath(
                Path.Combine(assemblyDir, "..", "..", "BinSkim.Driver", "release", "BinSkim.dll"));

            if (!File.Exists(binSkimDll))
            {
                throw new FileNotFoundException(
                    $"BinSkim.dll not found at expected path: {binSkimDll}. " +
                    "Ensure the BinSkim.Driver project has been built in Release configuration.");
            }

            return binSkimDll;
        }

        /// <summary>
        /// Runs BinSkim as an external process with the specified arguments.
        /// </summary>
        public static async Task<BinSkimRunResult> RunAsync(
            string[] args,
            TimeSpan? timeout = null)
        {
            string binSkimDll = GetBinSkimDllPath();
            timeout ??= DefaultTimeout;

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add(binSkimDll);
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };
            using var cts = new CancellationTokenSource(timeout.Value);

            process.Start();

            // Read stdout and stderr concurrently to avoid deadlocks from filled OS buffers.
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort cleanup */ }
                throw new TimeoutException(
                    $"BinSkim process did not exit within {timeout.Value.TotalSeconds}s. " +
                    $"Args: {string.Join(" ", args)}");
            }

            string stdOut = await stdOutTask;
            string stdErr = await stdErrTask;

            string sarifPath = FindOutputPath(args);

            return new BinSkimRunResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdOut,
                StdErr = stdErr,
                SarifOutputPath = sarifPath,
            };
        }

        private static string FindOutputPath(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] is "--output" or "-o")
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
