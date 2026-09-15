// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using FluentAssertions;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Writers;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class MultithreadedAnalyzeCommandTests
    {
        private static readonly Random s_random;
        private static readonly double s_randomSeed;

        static MultithreadedAnalyzeCommandTests()
        {
            s_randomSeed = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            s_random = new Random((int)s_randomSeed);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_ReturnCommonPathRootFromTargetSpecifiersIfOneExists()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Not applicable. Test cases below are Windows paths.
                return;
            }

            var testCases = new[]
            {
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\*.dll"
                    },
                    ExpectedCommonPath = @"C:\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\*.dll",
                        @"D:\*.dll"
                    },
                    ExpectedCommonPath = string.Empty
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path2\"
                    },
                    ExpectedCommonPath = @"C:\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"c:\*.dll",
                        @"c:\*.dll",
                        @"C:\*.DLL"
                    },
                    ExpectedCommonPath = @"c:\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path1\path2\"
                    },
                    ExpectedCommonPath = @"C:\path1\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\*.dll",
                        @"C:\path1\*.exe"
                    },
                    ExpectedCommonPath = @"C:\path1\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path1\1.dll",
                        @"C:\path1\2.exe",
                        @"C:\path1\path2\",
                        @"C:\path1\path2\1.exe",
                        @"C:\path1\path2\path3\",
                        @"C:\path1\path2\path3\1.sys",
                        "c:\\path1\\path2\\path3\\path4\\"
                    },
                    ExpectedCommonPath = @"C:\path1\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"\\PC1\path1\",
                        @"\\PC1\path1\path2\",
                    },
                    ExpectedCommonPath = @"\\PC1\path1\",
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\..\*.dll",
                        @"C:\*.dll",
                    },
                    ExpectedCommonPath = @"C:\",
                },
            };

            var sb = new StringBuilder();
            foreach (var testCase in testCases)
            {
                string commonPath = MultithreadedAnalyzeCommand.ReturnCommonPathRootFromTargetSpecifiersIfOneExists(testCase.TargetFileSpecifiers);
                if (!string.Equals(commonPath, testCase.ExpectedCommonPath, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"The test was expecting '{testCase.ExpectedCommonPath}' but found '{commonPath}'.");
                }

                // Testing the same string array in a random order.
                // This will guarantee that the sorting is working as expected.
                commonPath = MultithreadedAnalyzeCommand.ReturnCommonPathRootFromTargetSpecifiersIfOneExists(Shuffle(testCase.TargetFileSpecifiers));
                if (!string.Equals(commonPath, testCase.ExpectedCommonPath, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"The test was expecting '{testCase.ExpectedCommonPath}' but found '{commonPath}' when shuffled with seed '{s_randomSeed}'.");
                }
            }

            sb.Length.Should().Be(0, sb.ToString());
        }

        private static string[] Shuffle(string[] data)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                int swapWith = s_random.Next(i, data.Length);
                string temp = data[i];
                data[i] = data[swapWith];
                data[swapWith] = temp;
            }

            return data;
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_TelemetryNotSpecifiedHasOneConsoleLogger()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            Assert.IsType<Sarif.Driver.AggregatingLogger>(context.Logger);

            var aggregatingLogger = (Sarif.Driver.AggregatingLogger)context.Logger;
            Assert.Contains(aggregatingLogger.Loggers, l => l is ConsoleLogger);
            Assert.Single(aggregatingLogger.Loggers);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_TelemetrySpecifiedHasTwoLoggers()
        {
            var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            var telemetryClient = new TelemetryClient(telemetryConfiguration);
            var telemetry = new Telemetry(telemetryConfiguration);

            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.OutputFilePath = "test/path/";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand(telemetry);
            command.InitializeGlobalContextFromOptions(options, ref context);

            Assert.IsType<Sarif.Driver.AggregatingLogger>(context.Logger);

            var aggregatingLogger = (Sarif.Driver.AggregatingLogger)context.Logger;
            Assert.Contains(aggregatingLogger.Loggers, l => l is ConsoleLogger);
            Assert.Contains(aggregatingLogger.Loggers, l => l is RuleTelemetryLogger);
            Assert.Equal(2, aggregatingLogger.Loggers.Count);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_QuietOptionSetHasOneRuleTelemetryLogger()
        {
            var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            var telemetryClient = new TelemetryClient(telemetryConfiguration);
            var telemetry = new Telemetry(telemetryConfiguration);

            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.OutputFilePath = "test/path/";
            options.Quiet = true;
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand(telemetry);
            command.InitializeGlobalContextFromOptions(options, ref context);

            Assert.IsType<Sarif.Driver.AggregatingLogger>(context.Logger);

            var aggregatingLogger = (Sarif.Driver.AggregatingLogger)context.Logger;
            Assert.Contains(aggregatingLogger.Loggers, l => l is RuleTelemetryLogger);
            Assert.Single(aggregatingLogger.Loggers);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_QuietOptionSetAndNoTelemetryHasOneConsoleLogger()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.Quiet = true;
            options.OutputFilePath = "test/path/";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            Assert.IsType<Sarif.Driver.AggregatingLogger>(context.Logger);

            var aggregatingLogger = (Sarif.Driver.AggregatingLogger)context.Logger;
            Assert.Empty(aggregatingLogger.Loggers);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_DisableArchiveExtractionClearsOpcFileExtensions()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.DisableArchiveExtraction = true;
            options.OutputFilePath = "test/path/";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.OpcFileExtensions.Should().BeEmpty(
                "OpcFileExtensions should be cleared when --disable-archive-extraction is set");
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_DefaultPreservesOpcFileExtensions()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.OpcFileExtensions.Should().NotBeEmpty(
                "OpcFileExtensions should retain default values when --disable-archive-extraction is not set");
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_SetsSymbolPath()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            options.SymbolsPath = @"C:\symbols";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.SymbolPath.Should().Be(@"C:\symbols");
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_SetsIgnorePdbLoadError()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            options.IgnorePdbLoadError = true;
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.IgnorePdbLoadError.Should().BeTrue();
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_SetsIgnorePELoadError()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            options.IgnorePELoadError = true;
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.IgnorePELoadError.Should().BeTrue();
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_SetsIgnoreBinaryAnalysisErrors()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            options.IgnoreBinaryAnalysisErrors = true;
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.IgnoreBinaryAnalysisErrors.Should().BeTrue();
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_MaxFileSizeDefaultsToMaxValue()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.MaxFileSizeInKilobytes.Should().Be(long.MaxValue);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_InitializeGlobalContextFromOptions_MaxFileSizeHonorsOption()
        {
            var options = new AnalyzeOptions();
            options.TargetFileSpecifiers = new List<string> { "test.dll" };
            options.DisableTelemetry = true;
            options.OutputFilePath = "test/path/";
            options.MaxFileSizeInKilobytes = 1024;
            var context = new BinaryAnalyzerContext();

            var command = new MultithreadedAnalyzeCommand();
            command.InitializeGlobalContextFromOptions(options, ref context);

            context.MaxFileSizeInKilobytes.Should().Be(1024);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_ValidAnalysisFileExtensions_ContainsExpectedExtensions()
        {
            MultithreadedAnalyzeCommand.ValidAnalysisFileExtensions.Should().Contain(".dll");
            MultithreadedAnalyzeCommand.ValidAnalysisFileExtensions.Should().Contain(".exe");
            MultithreadedAnalyzeCommand.ValidAnalysisFileExtensions.Should().Contain(".sys");
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_ValidAnalysisFileExtensions_DoesNotContainUnexpectedExtensions()
        {
            MultithreadedAnalyzeCommand.ValidAnalysisFileExtensions.Should().NotContain(".txt");
            MultithreadedAnalyzeCommand.ValidAnalysisFileExtensions.Should().NotContain(".pdf");
            MultithreadedAnalyzeCommand.ValidAnalysisFileExtensions.Should().NotContain(".cs");
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_Run_NullTargetFileSpecifiers_ThrowsArgumentNullException()
        {
            var command = new MultithreadedAnalyzeCommand();
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = null,
                DisableTelemetry = true
            };

            Action act = () => command.Run(options);

            act.Should().Throw<ArgumentNullException>()
                .Where(e => e.Message.Contains("TargetFileSpecifiers"));
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_Run_EmptyTargetFileSpecifiers_ThrowsArgumentNullException()
        {
            var command = new MultithreadedAnalyzeCommand();
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new List<string>(),
                DisableTelemetry = true
            };

            Action act = () => command.Run(options);

            act.Should().Throw<ArgumentNullException>()
                .Where(e => e.Message.Contains("TargetFileSpecifiers"));
        }
    }
}
