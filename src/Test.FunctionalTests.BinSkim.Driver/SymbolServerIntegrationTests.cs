// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class SymbolServerIntegrationTests
    {
        private const string PublicOptIn = "BINSKIM_RUN_LIVE_SYMBOL_TESTS";
        private const string PrivateOptIn = "BINSKIM_RUN_PRIVATE_SYMBOL_TESTS";
        private const string PrivateConfiguration = "BINSKIM_SYMBOL_SERVER_TEST_CONFIG";
        private readonly ITestOutputHelper output;

        public SymbolServerIntegrationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [LiveSymbolServerFact]
        [Trait("Category", "LiveSymbolServer")]
        public void Pdb_DownloadsMatchingSymbolsFromMicrosoftPublicServer()
        {
            string binaryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ntdll.dll");
            VerifyDownload(
                new Uri("https://msdl.microsoft.com/download/symbols"),
                binaryPath,
                requirePrivateSymbols: false);
        }

        [LiveSymbolServerFact(privateServer: true)]
        [Trait("Category", "LiveSymbolServer")]
        public void Pdb_DownloadsMatchingSymbolsFromConfiguredPrivateServers()
        {
            string configurationPath = Environment.GetEnvironmentVariable(PrivateConfiguration);
            configurationPath.Should().NotBeNullOrWhiteSpace(
                $"enabled private integration tests require {PrivateConfiguration}");
            File.Exists(configurationPath).Should().BeTrue("the protected server configuration must exist");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };
            ServerCase[] servers = JsonSerializer.Deserialize<ServerCase[]>(File.ReadAllText(configurationPath), options);
            servers.Should().NotBeNullOrEmpty("enabled private tests must not silently run zero server cases");

            foreach (ServerCase server in servers)
            {
                server.Should().NotBeNull();
                server.Name.Should().NotBeNullOrWhiteSpace("each case needs a non-sensitive diagnostic name");
                server.ServerUrl.Should().NotBeNullOrWhiteSpace();
                server.BinaryPath.Should().NotBeNullOrWhiteSpace("use a controlled binary with symbols on this server");

                Uri.TryCreate(server.ServerUrl, UriKind.Absolute, out Uri uri).Should().BeTrue(
                    "the configured symbol server must be an absolute HTTPS URL");
                ValidateServerUri(uri);
                output.WriteLine($"Testing configured server case: {server.Name}");
                VerifyDownload(uri, server.BinaryPath, requirePrivateSymbols: true);
            }
        }

        private void VerifyDownload(Uri server, string binaryPath, bool requirePrivateSymbols)
        {
            ValidateServerUri(server);
            File.Exists(binaryPath).Should().BeTrue("the test must have a real PE with a matching server PDB");

            DirectoryInfo directory = Directory.CreateTempSubdirectory("binskim-live-symbols-");
            try
            {
                string targetPath = Path.Combine(directory.FullName, Path.GetFileName(binaryPath));
                File.Copy(binaryPath, targetPath);
                string cache = Path.Combine(directory.FullName, "cache");
                Directory.CreateDirectory(cache);
                Directory.EnumerateFileSystemEntries(cache).Should().BeEmpty("the first load must use a cold cache");

                CodeViewDebugDirectoryData codeView;
                using (FileStream stream = File.OpenRead(targetPath))
                using (var pe = new PEReader(stream))
                {
                    DebugDirectoryEntry entry = pe.ReadDebugDirectory()
                        .Single(value => value.Type == DebugDirectoryEntryType.CodeView);
                    codeView = pe.ReadCodeViewDebugDirectoryData(entry);
                }

                string cachedPdbPath;
                byte[] downloadedHash;
                string symbolPath = $"SRV*{cache}*{server.AbsoluteUri.TrimEnd('/')}";
                using (var pdb = new Pdb(targetPath, symbolPath: symbolPath, traceLoads: true))
                {
                    cachedPdbPath = pdb.PdbLocation;
                    Path.GetFullPath(cachedPdbPath).Should().StartWith(
                        cache + Path.DirectorySeparatorChar,
                        "the PDB must have been downloaded into this test's previously empty cache");
                    Path.GetFileName(cachedPdbPath).Should().Be(Path.GetFileName(codeView.Path));
                    File.Exists(cachedPdbPath).Should().BeTrue();
                    pdb.GlobalScope.Guid.Should().Be(codeView.Guid, "downloaded symbols must match the target PE");
                    pdb.LoadTrace.Should().Contain($"'{cachedPdbPath}'. HResult: S_OK.");
                    if (requirePrivateSymbols)
                    {
                        pdb.IsStripped.Should().BeFalse(
                            "a private-server case must retrieve private symbols, not just public-symbol fallback");
                        pdb.GetCompilerNameFromCompilandDetails().Should().NotBeNullOrWhiteSpace();
                    }

                    LogNativeDependency("msdia140.dll");
                    LogNativeDependency("symsrv.dll");
                    using FileStream downloaded = File.OpenRead(cachedPdbPath);
                    downloadedHash = SHA256.HashData(downloaded);
                }

                // Reload through DIA from the populated cache without exposing the remote server.
                using var cached = new Pdb(targetPath, symbolPath: $"SRV*{cache}", traceLoads: true);
                cached.PdbLocation.Should().Be(cachedPdbPath);
                cached.GlobalScope.Guid.Should().Be(codeView.Guid);
                using FileStream cachedStream = File.OpenRead(cachedPdbPath);
                SHA256.HashData(cachedStream).Should().Equal(downloadedHash);
                output.WriteLine("Cold-cache download, matching PDB identity and cache-only reload succeeded.");
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        private static void ValidateServerUri(Uri server)
        {
            bool valid = server != null
                && server.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(server.UserInfo)
                && string.IsNullOrEmpty(server.Query)
                && string.IsNullOrEmpty(server.Fragment)
                && !server.AbsoluteUri.Contains('*')
                && !server.AbsoluteUri.Contains(';');
            valid.Should().BeTrue("use an HTTPS server URL without embedded credentials, query strings or symbol-path separators");
        }

        private void LogNativeDependency(string name)
        {
            string path = name == "msdia140.dll" && DiaCandidatePath != null
                ? DiaCandidatePath
                : Path.Combine(AppContext.BaseDirectory, name);
            File.Exists(path).Should().BeTrue("the live test must use the deployed native dependencies");
            using Process process = Process.GetCurrentProcess();
            process.Modules.Cast<ProcessModule>().Should().Contain(module =>
                Path.GetFullPath(module.FileName).Equals(path, StringComparison.OrdinalIgnoreCase));
            using FileStream stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            output.WriteLine($"{name}: version={FileVersionInfo.GetVersionInfo(path).FileVersion}; SHA256={hash}");
        }

        private sealed class LiveSymbolServerFactAttribute : FactAttribute
        {
            public LiveSymbolServerFactAttribute(bool privateServer = false)
            {
                string variable = privateServer ? PrivateOptIn : PublicOptIn;
                if (!OperatingSystem.IsWindows())
                {
                    Skip = "Native symbol-server integration requires Windows.";
                }
                else if (!string.Equals(Environment.GetEnvironmentVariable(variable), "true", StringComparison.OrdinalIgnoreCase))
                {
                    Skip = $"Set {variable}=true on an authorized, network-enabled agent to run live symbol-server tests.";
                }
            }
        }

        private sealed class ServerCase
        {
            public ServerCase()
            {
            }

            public string Name { get; set; }
            public string ServerUrl { get; set; }
            public string BinaryPath { get; set; }
        }
    }
}
