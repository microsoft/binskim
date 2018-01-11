// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis.Sarif.Readers;
using Microsoft.CodeAnalysis.Sarif;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.IL
{
    public class BuiltInRuleFunctionalTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public BuiltInRuleFunctionalTests(ITestOutputHelper output)
        {
            _testOutputHelper = output;
        }

        private static string TestDirectory = GetTestDirectory(@"BinSkim.Driver.FunctionalTests\BaselineTestsData");

        private static string GetTestDirectory(string relativeDirectory)
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            var codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            var dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, @"..\..\..\..\src\");
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }

        [Fact]
        public void Driver_BuiltInRuleFunctionalTests()
        {
            BatchRuleRules(string.Empty, "*.dll", "*.exe");
        }

        private void BatchRuleRules(string ruleName, params string[] inputFilters)
        {
            var sb = new StringBuilder();
            string testDirectory = BuiltInRuleFunctionalTests.TestDirectory + "\\" + ruleName;

            foreach (string inputFilter in inputFilters)
            {
                string[] testFiles = Directory.GetFiles(testDirectory, inputFilter);

                foreach (string file in testFiles)
                {
                    RunRules(sb, file);
                }
            }

            if (sb.Length == 0)
            {
                // Test passes
                return;
            }

            string rebaselineMessage = "If the actual output is expected, generate new baselines by executing `UpdateBaselines.ps1` from a PS command prompt.";
            sb.AppendLine(String.Format(CultureInfo.CurrentCulture, rebaselineMessage));

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Run the following to all test baselines vs. actual results:");
                sb.AppendLine(GenerateDiffCommand(
                    Path.Combine(testDirectory, "Expected"), 
                    Path.Combine(testDirectory, "Actual")));
                _testOutputHelper.WriteLine(sb.ToString());
            }

            Assert.Equal(0, sb.Length);
        }

        private void RunRules(StringBuilder sb, string inputFileName)
        {
            string fileName = Path.GetFileName(inputFileName);
            string actualDirectory = Path.Combine(Path.GetDirectoryName(inputFileName), "Actual");
            string expectedDirectory = Path.Combine(Path.GetDirectoryName(inputFileName), "Expected");

            if (!Directory.Exists(actualDirectory))
            {
                Directory.CreateDirectory(actualDirectory);
            }

            string expectedFileName = Path.Combine(expectedDirectory, fileName + ".sarif");
            string actualFileName = Path.Combine(actualDirectory, fileName + ".sarif");

            AnalyzeCommand command = new AnalyzeCommand();
            AnalyzeOptions options = new AnalyzeOptions();

            options.TargetFileSpecifiers = new string[] { inputFileName };
            options.OutputFilePath = actualFileName;
            options.Verbose = true;
            options.Recurse = false;
            options.ComputeFileHashes = true;
            options.ConfigurationFilePath = "default";

            int result = command.Run(options);

            // Note that we don't ensure a success code. That is because we
            // are running end-to-end tests for valid and invalid files

            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            string expectedText = File.ReadAllText(expectedFileName);
            string actualText = File.ReadAllText(actualFileName);

            // Replace repository root absolute path with Z:\ for machine and enlistment independence
            string repoRoot = Path.GetFullPath(Path.Combine(actualDirectory, "..", "..", "..", ".."));
            actualText = actualText.Replace(repoRoot.Replace(@"\", @"\\"), @"Z:");
            actualText = actualText.Replace(repoRoot.Replace(@"\", @"/"), @"Z:");

            // Remove stack traces as they can change due to inlining differences by configuration and runtime.
            actualText = Regex.Replace(actualText, @"\\r\\n   at [^""]+", "");

            // Write back the normalized actual text so that the diff command given on failure shows what was actually compared.
            File.WriteAllText(actualFileName, actualText);

            // Make sure we can successfully deserialize what was just generated
            SarifLog expectedLog = JsonConvert.DeserializeObject<SarifLog>(expectedText, settings);
            SarifLog actualLog = JsonConvert.DeserializeObject<SarifLog>(actualText, settings);

            var visitor = new ResultDiffingVisitor(expectedLog);

            if (!visitor.Diff(actualLog.Runs[0].Results))
            {
                string errorMessage = "The output of the tool did not match for input {0}.";
                sb.AppendLine(String.Format(CultureInfo.CurrentCulture, errorMessage, inputFileName));
                sb.AppendLine("Check differences with:");
                sb.AppendLine(GenerateDiffCommand(expectedFileName, actualFileName));
            }
        }

        private string GenerateDiffCommand(string expected, string actual)
        {
            expected = Path.GetFullPath(expected);
            actual = Path.GetFullPath(actual);

            string beyondCompare = TryFindBeyondCompare();
            if (beyondCompare != null)
            {
                return String.Format(CultureInfo.InvariantCulture, "\"{0}\" \"{1}\" \"{2}\" /title1=Expected /title2=Actual", beyondCompare, expected, actual);
            }

            return String.Format(CultureInfo.InvariantCulture, "windiff \"{0}\" \"{1}\"", expected, actual);
        }

        private static string TryFindBeyondCompare()
        {
            List<string> directories = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            directories.Add(programFiles);
            directories.Add(programFiles.Replace(" (x86)", ""));

            foreach (string directory in directories)
            {
                for (int idx = 4; idx >= 3; --idx)
                {
                    string beyondComparePath = String.Format(CultureInfo.InvariantCulture, "{0}\\Beyond Compare {1}\\BComp.exe", directory, idx);
                    if (File.Exists(beyondComparePath))
                    {
                        return beyondComparePath;
                    }
                }

                string beyondCompare2Path = programFiles + "\\Beyond Compare 2\\BC2.exe";
                if (File.Exists(beyondCompare2Path))
                {
                    return beyondCompare2Path;
                }
            }

            return null;
        }
    }
}
