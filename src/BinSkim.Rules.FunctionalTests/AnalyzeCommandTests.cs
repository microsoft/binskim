// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis.Driver;
using Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat.DataContracts;
using Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat.Readers;

using Newtonsoft.Json;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class AnalyzeCommandTests
    {
        private void ExceptionTestHelper(
            ExceptionCondition exceptionCondition,
            RuntimeConditions runtimeConditions,
            FailureReason expectedExitReason = FailureReason.ExceptionsDidNotHaltAnalysis,
            AnalyzeOptions analyzeOptions = null)
        {
            AnalyzeCommand.DefaultRuleAssemblies = new Assembly[] { typeof(ExceptionRaisingRule).Assembly };
            ExceptionRaisingRule.s_exceptionCondition = exceptionCondition;
            analyzeOptions = analyzeOptions ?? new AnalyzeOptions()
            {
                BinaryFileSpecifiers = new string[0]
            };

            var command = new AnalyzeCommand();
            int result = command.Run(analyzeOptions);

            int expectedResult =
                (runtimeConditions & RuntimeConditions.Fatal) == RuntimeConditions.NoErrors ?
                    AnalyzeCommand.SUCCESS : AnalyzeCommand.FAILURE;

            Assert.Equal(runtimeConditions, command.RuntimeErrors);
            Assert.Equal(expectedResult, result);

            if (expectedExitReason != FailureReason.ExceptionsDidNotHaltAnalysis)
            {
                Assert.NotNull(command.ExecutionException);

                if (expectedExitReason != FailureReason.UnhandledExceptionInEngine)
                {
                    var eax = command.ExecutionException as ExitApplicationException<FailureReason>;
                    Assert.NotNull(eax);
                }
            }
            else
            {
                Assert.Null(command.ExecutionException);
            }
            ExceptionRaisingRule.s_exceptionCondition = ExceptionCondition.None;
            AnalyzeCommand.DefaultRuleAssemblies = null;
        }

        [Fact]
        public void ExceptionRaisedInstantiatingSkimmers()
        {
            ExceptionTestHelper(
                ExceptionCondition.InvokingConstructor,
                RuntimeConditions.ExceptionInstantiatingSkimmers,
                FailureReason.UnhandledExceptionInstantiatingSkimmers);
        }

        [Fact]
        public void ExceptionRaisedInvokingInitialize()
        {
            ExceptionTestHelper(
                ExceptionCondition.InvokingInitialize,
                RuntimeConditions.ExceptionInSkimmerInitialize
            );
        }

        [Fact]
        public void ExceptionRaisedInvokingCanAnalyze()
        {
            var options = new AnalyzeOptions()
            {
                BinaryFileSpecifiers = new string[] { this.GetType().Assembly.Location },
            };

            ExceptionTestHelper(
                ExceptionCondition.InvokingCanAnalyze,
                RuntimeConditions.ExceptionRaisedInSkimmerCanAnalyze,
                analyzeOptions: options
            );
        }

        [Fact]
        public void ExceptionRaisedInvokingAnalyze()
        {
            var options = new AnalyzeOptions()
            {
                BinaryFileSpecifiers = new string[] { this.GetType().Assembly.Location },
            };

            ExceptionTestHelper(
                ExceptionCondition.InvokingAnalyze,
                RuntimeConditions.ExceptionInSkimmerAnalyze,
                analyzeOptions: options
            );
        }

        [Fact]
        public void ExceptionRaisedInEngine()
        {
            AnalyzeCommand.RaiseUnhandledExceptionInDriverCode = true;

            var options = new AnalyzeOptions()
            {
                BinaryFileSpecifiers = new string[] { this.GetType().Assembly.Location },
            };

            ExceptionTestHelper(
                ExceptionCondition.None,
                RuntimeConditions.ExceptionInEngine,
                FailureReason.UnhandledExceptionInEngine);

            AnalyzeCommand.RaiseUnhandledExceptionInDriverCode = false;
        }

        [Fact]
        public void IOExceptionRaisedCreatingSarifLog()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var stream = File.OpenWrite(path))
                {
                    // our log file is locked for write
                    // causing exceptions at analysis time

                    var options = new AnalyzeOptions()
                    {
                        BinaryFileSpecifiers = new string[] { this.GetType().Assembly.Location },
                        OutputFilePath = path,
                        Verbose = true,
                    };

                    ExceptionTestHelper(
                        ExceptionCondition.None,
                        RuntimeConditions.ExceptionCreatingLogfile,
                        expectedExitReason: FailureReason.ExceptionCreatingLogFile,
                        analyzeOptions: options);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void UnauthorizedAccessExceptionCreatingSarifLog()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            path = Path.Combine(path, Guid.NewGuid().ToString());

            try
            {
                // attempt to persist to unauthorized location will raise exception
                var options = new AnalyzeOptions()
                {
                    BinaryFileSpecifiers = new string[] { this.GetType().Assembly.Location },
                    OutputFilePath = path,
                    Verbose = true,
                };

                ExceptionTestHelper(
                    ExceptionCondition.None,
                    RuntimeConditions.ExceptionCreatingLogfile,
                    expectedExitReason: FailureReason.ExceptionCreatingLogFile,
                    analyzeOptions: options);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TargetIsNotAPortableExecutable()
        {
            string path = Path.GetTempFileName();

            try
            {
                var options = new AnalyzeOptions()
                {
                    BinaryFileSpecifiers = new string[] { path },
                };

                ExceptionTestHelper(
                    ExceptionCondition.None,
                    RuntimeConditions.OneOrMoreTargetsNotPortableExecutables,
                    analyzeOptions: options);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ExceptionLoadingTargetFile()
        {
            string path = Path.GetTempFileName();

            try
            {
                var options = new AnalyzeOptions()
                {
                    BinaryFileSpecifiers = new string[] { path },
                };

                using (var lockedStream = File.OpenWrite(path))
                {
                    ExceptionTestHelper(
                        ExceptionCondition.None,
                        RuntimeConditions.ExceptionLoadingTargetFile,
                        analyzeOptions: options);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }


        public RunLog AnalyzeFile(string fileName)
        {
            string path = Path.GetTempFileName();
            RunLog runLog = null;

            try
            {
                var options = new AnalyzeOptions()
                {
                    BinaryFileSpecifiers = new string[] { fileName },
                    Verbose = true,
                    Statistics = true,
                    ComputeTargetsHash = true,
                    PolicyFilePath = "default",
                    Recurse = true,
                    OutputFilePath = path,
                    SymbolsPath = "SRV*http://symweb"
                };

                var command = new AnalyzeCommand();
                int result = command.Run(options);
                Assert.Equal(AnalyzeCommand.SUCCESS, result);

                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    ContractResolver = SarifContractResolver.Instance
                };

                ResultLog log = JsonConvert.DeserializeObject<ResultLog>(File.ReadAllText(path), settings);
                Assert.NotNull(log);
                Assert.Equal<int>(1, log.RunLogs.Count);

                runLog = log.RunLogs[0];
            }
            finally
            {
                File.Delete(path);
            }

            return runLog;
        }

        [Fact]
        public void EndToEndAnalysisWithNoIssues()
        {
            AnalyzeCommand.DefaultRuleAssemblies = new Assembly[] { this.GetType().Assembly };
            RunLog runLog = AnalyzeFile(this.GetType().Assembly.Location);

            int issueCount = 0;
            SarifHelpers.ValidateRunLog(runLog, (issue) => { issueCount++; });
            Assert.Equal(0, issueCount);
            AnalyzeCommand.DefaultRuleAssemblies = null;
        }

        [Fact]
        public void EndToEndAnalysisWithDefaultRules()
        {
            AnalyzeCommand.DefaultRuleAssemblies = null;
            RunLog runLog = AnalyzeFile(this.GetType().Assembly.Location);

            int resultCount = 0;
            SarifHelpers.ValidateRunLog(runLog, (result) => { resultCount++; });
            Assert.Equal(17, resultCount);
        }
    }
}