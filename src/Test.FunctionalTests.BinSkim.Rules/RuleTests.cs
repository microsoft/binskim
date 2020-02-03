// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class RuleTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public RuleTests(ITestOutputHelper output)
        {
            this.testOutputHelper = output;
        }

        private void VerifyPass(
            BinarySkimmer skimmer,
            IEnumerable<string> additionalTestFiles = null,
            bool useDefaultPolicy = false)
        {
            this.Verify(skimmer, additionalTestFiles, useDefaultPolicy, expectToPass: true);
        }

        private void VerifyFail(
            BinarySkimmer skimmer,
            IEnumerable<string> additionalTestFiles = null,
            bool useDefaultPolicy = false)
        {
            this.Verify(skimmer, additionalTestFiles, useDefaultPolicy, expectToPass: false);
        }

        private void Verify(
            BinarySkimmer skimmer,
            IEnumerable<string> additionalTestFiles,
            bool useDefaultPolicy,
            bool expectToPass)
        {
            var targets = new List<string>();
            string ruleName = skimmer.GetType().Name;
            string testFilesDirectory = ruleName;
            testFilesDirectory = Path.Combine(Environment.CurrentDirectory, "FunctionalTestsData", testFilesDirectory);
            testFilesDirectory = Path.Combine(testFilesDirectory, expectToPass ? "Pass" : "Fail");

            Assert.True(Directory.Exists(testFilesDirectory));

            foreach (string target in Directory.GetFiles(testFilesDirectory, "*", SearchOption.AllDirectories))
            {
                if (AnalyzeCommand.ValidAnalysisFileExtensions.Contains(Path.GetExtension(target)))
                {
                    targets.Add(target);
                }
            }

            if (additionalTestFiles != null)
            {
                foreach (string additionalTestFile in additionalTestFiles)
                {
                    targets.Add(additionalTestFile);
                }
            }

            var context = new BinaryAnalyzerContext();
            var logger = new TestMessageLogger();
            context.Logger = logger;
            PropertiesDictionary policy = null;

            if (useDefaultPolicy)
            {
                policy = new PropertiesDictionary();
            }
            context.Policy = policy;

            skimmer.Initialize(context);

            foreach (string target in targets)
            {
                context = this.CreateContext(logger, policy, target);

                if (!context.IsValidAnalysisTarget) { continue; }

                context.Rule = skimmer;

                if (skimmer.CanAnalyze(context, out string reasonForNotAnalyzing) != AnalysisApplicability.ApplicableToSpecifiedTarget)
                {
                    continue;
                }

                skimmer.Analyze(context);
            }

            HashSet<string> expected = expectToPass ? logger.PassTargets : logger.FailTargets;
            HashSet<string> other = expectToPass ? logger.FailTargets : logger.PassTargets;
            HashSet<string> configErrors = logger.ConfigurationErrorTargets;

            string expectedText = expectToPass ? "success" : "failure";
            string actualText = expectToPass ? "failed" : "succeeded";
            var sb = new StringBuilder();

            foreach (string target in targets)
            {
                if (expected.Contains(target))
                {
                    expected.Remove(target);
                    continue;
                }
                bool missingEntirely = !other.Contains(target);

                if (missingEntirely &&
                    !expectToPass &&
                    target.Contains("Pdb") &&
                    configErrors.Contains(target))
                {
                    missingEntirely = false;
                    configErrors.Remove(target);
                    continue;
                }

                if (missingEntirely)
                {
                    sb.AppendLine("Expected '" + ruleName + "' " + expectedText + " but saw no result at all for file: " + Path.GetFileName(target));
                }
                else
                {
                    other.Remove(target);
                    sb.AppendLine("Expected '" + ruleName + "' " + expectedText + " but check " + actualText + " for: " + Path.GetFileName(target));
                }
            }

            if (sb.Length > 0)
            {
                this.testOutputHelper.WriteLine(sb.ToString());
            }

            Assert.Equal(0, sb.Length);
            Assert.Empty(expected);
            Assert.Empty(other);
        }

        private BinaryAnalyzerContext CreateContext(TestMessageLogger logger, PropertiesDictionary policy, string target)
        {
            var context = new BinaryAnalyzerContext
            {
                Logger = logger,
                Policy = policy
            };

            if (target != null)
            {
                context.TargetUri = new Uri(target);
            }

            return context;
        }

        private void VerifyThrows<ExceptionType>(
            BinarySkimmer skimmer,
            bool useDefaultPolicy = false) where ExceptionType : Exception
        {
            var targets = new List<string>();
            string ruleName = skimmer.GetType().Name;
            string baseFilesDirectory = ruleName;
            baseFilesDirectory = Path.Combine(Environment.CurrentDirectory, "FunctionalTestsData", baseFilesDirectory);

            string[] testFilesDirectories =
                new string[]
                {
                    Path.Combine(baseFilesDirectory, "Pass"),
                    Path.Combine(baseFilesDirectory, "Fail"),
                    Path.Combine(baseFilesDirectory, "NotApplicable")
                };

            foreach (string testDirectory in testFilesDirectories)
            {
                if (Directory.Exists(testDirectory))
                {
                    foreach (string target in Directory.GetFiles(testDirectory, "*", SearchOption.AllDirectories))
                    {
                        targets.Add(target);
                    }
                }
            }
            var context = new BinaryAnalyzerContext();
            var logger = new TestMessageLogger();
            context.Logger = logger;
            PropertiesDictionary policy = null;

            if (useDefaultPolicy)
            {
                policy = new PropertiesDictionary();
            }
            context.Policy = policy;

            skimmer.Initialize(context);

            foreach (string target in targets)
            {
                context = this.CreateContext(logger, policy, target);

                context.Rule = skimmer;

                if (skimmer.CanAnalyze(context, out string reasonForNotAnalyzing) != AnalysisApplicability.ApplicableToSpecifiedTarget)
                {
                    continue;
                }
                Assert.Throws<ExceptionType>(() => skimmer.Analyze(context));
            }
        }

        private void VerifyNotApplicable(
            BinarySkimmer skimmer,
            HashSet<string> notApplicableConditions,
            AnalysisApplicability expectedApplicability = AnalysisApplicability.NotApplicableToSpecifiedTarget,
            bool useDefaultPolicy = false)
        {
            string ruleName = skimmer.GetType().Name;
            string testFilesDirectory = ruleName;
            testFilesDirectory = Path.Combine(Environment.CurrentDirectory, "FunctionalTestsData", testFilesDirectory);
            testFilesDirectory = Path.Combine(testFilesDirectory, "NotApplicable");

            var context = new BinaryAnalyzerContext();

            HashSet<string> targets = this.GetTestFilesMatchingConditions(notApplicableConditions);

            if (Directory.Exists(testFilesDirectory))
            {
                foreach (string target in Directory.GetFiles(testFilesDirectory, "*", SearchOption.AllDirectories))
                {
                    if (AnalyzeCommand.ValidAnalysisFileExtensions.Contains(Path.GetExtension(target)))
                    {
                        targets.Add(target);
                    }
                }
            }

            var logger = new TestMessageLogger();
            context.Logger = logger;

            var sb = new StringBuilder();

            foreach (string target in targets)
            {
                string extension = Path.GetExtension(target);

                context = this.CreateContext(logger, null, target);
                if (!context.IsValidAnalysisTarget) { continue; }

                if (useDefaultPolicy)
                {
                    context.Policy = new PropertiesDictionary();
                }

                context.Rule = skimmer;

                AnalysisApplicability applicability;
                applicability = skimmer.CanAnalyze(context, out string reasonForNotAnalyzing);
                if (applicability != expectedApplicability)
                {
                    sb.AppendLine("CanAnalyze did not indicate target was invalid for analysis (return was " +
                        applicability + "): " +
                        Path.GetFileName(target));
                    continue;
                }
            }

            if (sb.Length > 0)
            {
                this.testOutputHelper.WriteLine(sb.ToString());
            }

            Assert.Equal(0, sb.Length);
        }

        private HashSet<string> GetTestFilesMatchingConditions(HashSet<string> metadataConditions)
        {
            string testFilesDirectory;
            testFilesDirectory = Path.Combine(Environment.CurrentDirectory, "BaselineTestsData");

            Assert.True(Directory.Exists(testFilesDirectory));
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (metadataConditions.Contains(MetadataConditions.ImageIsNotExe))
            {
                result.Add(Path.Combine(testFilesDirectory, "Native_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "ManagedResourcesOnly.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Managed_x86_VS2015_FSharp.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.CouldNotLoadPdb))
            {
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2013_NoPdb.exe"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x86_VS2013_MissingPdb.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIs64BitBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "Native_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Managed_x64_VS2015_FSharp.exe.exe"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsILOnlyAssembly))
            {
                result.Add(Path.Combine(testFilesDirectory, "Managed_x86_VS2013_Wpf.exe"));
                result.Add(Path.Combine(testFilesDirectory, "Managed_x86_VS2015_FSharp.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Managed_x64_VS2015_FSharp.exe.exe"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsMixedModeBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2013_NoPdb.exe"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x86_VS2013_Default.exe"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x86_VS2013_MissingPdb.dll"));

                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2015_Default.exe"));
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x86_VS2015_Default.exe"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsKernelModeBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "Native_x64_VS2013_KernelModeDriver.sys"));
                result.Add(Path.Combine(testFilesDirectory, "Native_x86_VS2013_KernelModeDriver.sys"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsInteropAssembly))
            {
                result.Add(Path.Combine(testFilesDirectory, "ManagedInteropAssemblyForAtlTestLibrary.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsResourceOnlyAssembly))
            {
                result.Add(Path.Combine(testFilesDirectory, "ManagedResourcesOnly.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsNot32BitBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "MixedMode_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Native_x64_VS2013_Default.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Uwp_ARM_VS2015_DefaultBlankApp.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Managed_x64_VS2015_FSharp.exe"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsNot64BitBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "Managed_x86_VS2013_Wpf.exe"));
                result.Add(Path.Combine(testFilesDirectory, "Native_x86_VS2013_Default.exe"));
                result.Add(Path.Combine(testFilesDirectory, "Uwp_ARM_VS2015_DefaultBlankApp.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsPreVersion7WindowsCEBinary))
            {
                // TODO need test case
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsResourceOnlyBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "ManagedResourcesOnly.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Native_x86_VS2013_ResourceOnly.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsXBoxBinary))
            {
                // TODO need test case
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsDotNetNativeBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "Uwp_x86_VS2015_DefaultBlankApp.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Uwp_x64_VS2015_DefaultBlankApp.dll"));
                result.Add(Path.Combine(testFilesDirectory, "Uwp_ARM_VS2015_DefaultBlankApp.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsWixBinary))
            {
                result.Add(Path.Combine(testFilesDirectory, "Wix_3.11.1_VS2017_Bootstrapper.exe"));
            }

            return result;
        }

        [Fact]
        public void SetNoExecutableBit_Pass()
        {
            this.VerifyPass(new MarkImageAsNXCompatible());
        }

        [Fact]
        public void SetNoExecutableBit_Fail()
        {
            this.VerifyFail(new MarkImageAsNXCompatible());
        }

        [Fact]
        public void SetNoExecutableBit_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary,
                MetadataConditions.ImageIs64BitBinary,
                MetadataConditions.ImageIsKernelModeBinary,
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsPreVersion7WindowsCEBinary
            };

            this.VerifyNotApplicable(new MarkImageAsNXCompatible(), notApplicableTo);
        }

        [Fact]
        public void EnableSpectreMitigations_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new EnableSpectreMitigations(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableSpectreMitigations_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyFail(new EnableSpectreMitigations(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsShared_Pass()
        {
            this.VerifyPass(new DoNotMarkWritableSectionsAsShared());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsShared_Fail()
        {
            this.VerifyFail(new DoNotMarkWritableSectionsAsShared());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsShared_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary
            };

            this.VerifyNotApplicable(new DoNotMarkWritableSectionsAsShared(), notApplicableTo);
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsExecutable_Pass()
        {
            this.VerifyPass(new DoNotMarkWritableSectionsAsExecutable());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsExecutable_Fail()
        {
            this.VerifyFail(new DoNotMarkWritableSectionsAsExecutable());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsExecutable_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsKernelModeBinary
            };

            this.VerifyNotApplicable(new DoNotMarkWritableSectionsAsExecutable(), notApplicableTo);
        }

        [Fact]
        public void EnableHighEntropyVirtualAddresses_Pass()
        {
            this.VerifyPass(new EnableHighEntropyVirtualAddresses());
        }

        [Fact]
        public void EnableHighEntropyVirtualAddresses_Fail()
        {
            this.VerifyFail(new EnableHighEntropyVirtualAddresses());
        }

        [Fact]
        public void EnableHighEntropyVirtualAddresses_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsNotExe,
                MetadataConditions.ImageIsNot64BitBinary,
                MetadataConditions.ImageIsKernelModeBinary
            };

            this.VerifyNotApplicable(new EnableHighEntropyVirtualAddresses(), notApplicableTo);
        }

        [Fact]
        public void EnableAddressSpaceLayoutRandomization_Pass()
        {
            this.VerifyPass(new EnableAddressSpaceLayoutRandomization());
        }

        [Fact]
        public void EnableAddressSpaceLayoutRandomization_Fail()
        {
            this.VerifyFail(new EnableAddressSpaceLayoutRandomization());
        }

        [Fact]
        public void EnableAddressSpaceLayoutRandomization_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary,
                MetadataConditions.ImageIsKernelModeBinary,
                MetadataConditions.ImageIsPreVersion7WindowsCEBinary,
            };

            this.VerifyNotApplicable(new EnableAddressSpaceLayoutRandomization(), notApplicableTo);
        }

        [Fact]
        public void DoNotMarkImportsSectionAsExecutable_Pass()
        {
            this.VerifyPass(new DoNotMarkImportsSectionAsExecutable());
        }

        [Fact]
        public void DoNotMarkImportsSectionAsExecutable_Fail()
        {
            this.VerifyFail(new DoNotMarkImportsSectionAsExecutable());
        }

        [Fact]
        public void DoNotMarkImportsSectionAsExecutable_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsKernelModeBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new DoNotMarkImportsSectionAsExecutable(), notApplicableTo);
        }

        [Fact]
        public void LoadImageAboveFourGigabyteAddress_Fail()
        {
            this.VerifyFail(new LoadImageAboveFourGigabyteAddress());
        }

        [Fact]
        public void LoadImageAboveFourGigabyteAddress_Pass()
        {
            this.VerifyPass(new LoadImageAboveFourGigabyteAddress());
        }

        [Fact]
        public void LoadImageAboveFourGigabyteAddress_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsNot64BitBinary,
                MetadataConditions.ImageIsKernelModeBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new LoadImageAboveFourGigabyteAddress(), notApplicableTo);
        }

        [Fact]
        public void EnableSafeSEH_Fail()
        {
            this.VerifyFail(new EnableSafeSEH());
        }

        [Fact]
        public void EnableSafeSEH_Pass()
        {
            this.VerifyPass(new EnableSafeSEH());
        }

        [Fact]
        public void EnableSafeSEH_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary,
                MetadataConditions.ImageIsNot32BitBinary,
                MetadataConditions.ImageIsResourceOnlyBinary
            };

            this.VerifyNotApplicable(new EnableSafeSEH(), notApplicableTo);
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyFail(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary,
                MetadataConditions.ImageIs64BitBinary,
                MetadataConditions.ImageIsNot32BitBinary,
                MetadataConditions.ImageIsNot64BitBinary,
                MetadataConditions.ImageIsKernelModeBinary,
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsILOnlyAssembly,
                MetadataConditions.ImageIsInteropAssembly,
                MetadataConditions.ImageIsPreVersion7WindowsCEBinary,
                MetadataConditions.ImageIsResourceOnlyAssembly
            };

            this.VerifyNotApplicable(new DoNotShipVulnerableBinaries(), notApplicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
        }

        [Fact]
        public void EnableStackProtection_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyFail(new EnableStackProtection());
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableStackProtection_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new EnableStackProtection());
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableStackProtection_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            this.VerifyNotApplicable(new EnableStackProtection(), notApplicableTo);
        }

        [Fact]
        public void InitializeStackProtection_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var failureConditions = new HashSet<string>
                {
                    MetadataConditions.CouldNotLoadPdb
                };

                this.VerifyFail(
                    new InitializeStackProtection(),
                    this.GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
        }

        [Fact]
        public void InitializeStackProtection_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new InitializeStackProtection());
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void InitializeStackProtection_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            this.VerifyNotApplicable(new InitializeStackProtection(), notApplicableTo);
        }

        [Fact]
        public void DoNotModifyStackProtectionCookie_Fail()
        {
            this.VerifyFail(new DoNotModifyStackProtectionCookie());
        }

        [Fact]
        public void DoNotModifyStackProtectionCookie_Pass()
        {
            this.VerifyPass(new DoNotModifyStackProtectionCookie());
        }

        [Fact]
        public void DoNotModifyStackProtectionCookie_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            // This rule happens to not require PDBs to function. The WIX bootstrapper passes 
            // this analysis, therefore, as no PDB is required and the binary is missing relevant
            // data that indicates that stack protection is relevant to the file.
            notApplicableTo.Remove(MetadataConditions.ImageIsWixBinary);

            this.VerifyNotApplicable(new DoNotModifyStackProtectionCookie(), notApplicableTo);
        }

        private static HashSet<string> GetNotApplicableBinariesForStackProtectionFeature()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary,
                MetadataConditions.ImageIsWixBinary,
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsDotNetNativeBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            return notApplicableTo;
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var failureConditions = new HashSet<string>
                {
                    MetadataConditions.CouldNotLoadPdb,
                };

                this.VerifyFail(
                    new DoNotDisableStackProtectionForFunctions(),
                    this.GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsXBoxBinary,
                MetadataConditions.ImageIsWixBinary,
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new DoNotDisableStackProtectionForFunctions(), notApplicableTo);

            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var applicableTo = new HashSet<string>
                {
                    MetadataConditions.ImageIs64BitBinary
                };
                this.VerifyNotApplicable(new DoNotDisableStackProtectionForFunctions(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
            }

            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var failureConditions = new HashSet<string>
                {
                    MetadataConditions.CouldNotLoadPdb
                };

                this.VerifyFail(
                    new EnableCriticalCompilerWarnings(),
                    this.GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new EnableCriticalCompilerWarnings(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_NotApplicable()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var notApplicableTo = new HashSet<string>
                {
                    MetadataConditions.ImageIsWixBinary,
                    MetadataConditions.ImageIsResourceOnlyBinary,
                    MetadataConditions.ImageIsILOnlyAssembly
                };

                this.VerifyNotApplicable(new EnableCriticalCompilerWarnings(), notApplicableTo);

                var applicableTo = new HashSet<string>
                {
                    MetadataConditions.ImageIs64BitBinary
                };

                this.VerifyNotApplicable(
                    new EnableCriticalCompilerWarnings(),
                    applicableTo,
                    AnalysisApplicability.ApplicableToSpecifiedTarget);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableControlFlowGuard_Fail()
        {
            this.VerifyFail(new EnableControlFlowGuard(), useDefaultPolicy: true);
        }

        [Fact]
        public void EnableControlFlowGuard_Pass()
        {
            this.VerifyPass(new EnableControlFlowGuard(), useDefaultPolicy: true);
        }

        [Fact]
        public void EnableControlFlowGuard_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsMixedModeBinary,
                MetadataConditions.ImageIsWixBinary,
                MetadataConditions.ImageIsKernelModeBinary,
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new EnableControlFlowGuard(), notApplicableTo, useDefaultPolicy: true);
        }

        [Fact]
        public void BuildWithSecureTools_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var failureConditions = new HashSet<string> { MetadataConditions.CouldNotLoadPdb };
                this.VerifyFail(
                    new BuildWithSecureTools(),
                    this.GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void BuildWithSecureTools_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new BuildWithSecureTools(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void BuildWithSecureTools_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsWixBinary,
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new BuildWithSecureTools(), notApplicableTo);
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var failureConditions = new HashSet<string> { MetadataConditions.CouldNotLoadPdb };
                this.VerifyFail(
                    new DoNotIncorporateVulnerableDependencies(),
                    this.GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new DoNotIncorporateVulnerableDependencies(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_NotApplicable()
        {
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), notApplicableTo);

            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var applicableTo = new HashSet<string> { MetadataConditions.ImageIs64BitBinary };
                this.VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void SignSecurely_Fail()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyFail(new SignSecurely());
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void SignSecurely_Pass()
        {
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                string kernel32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
                kernel32Path = Path.Combine(kernel32Path, "kernel32.dll");

                this.VerifyPass(new SignSecurely(), additionalTestFiles: new[] { kernel32Path });
            }
        }

        [Fact]
        public void SignSecurely_NotApplicable()
        {
            var applicableTo = new HashSet<string> { MetadataConditions.ImageIsNotSigned };
            this.VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), applicableTo, AnalysisApplicability.NotApplicableToSpecifiedTarget);
        }

        [Fact]
        public void EnablePositionIndependentExecutable_Pass()
        {
            this.VerifyPass(new EnablePositionIndependentExecutable());
        }

        [Fact]
        public void EnablePositionIndepedentExecutable_Fail()
        {
            this.VerifyFail(new EnablePositionIndependentExecutable());
        }

        [Fact]
        public void EnablePositionIndepedentExecutable_NotApplicable()
        {
            this.VerifyNotApplicable(new EnablePositionIndependentExecutable(), new HashSet<string>());
        }

        [Fact]
        public void DoNotMarkStackAsExecutable_Pass()
        {
            this.VerifyPass(new DoNotMarkStackAsExecutable());
        }

        [Fact]
        public void DoNotMarkStackAsExecutable_Fail()
        {
            this.VerifyFail(new DoNotMarkStackAsExecutable());
        }

        [Fact]
        public void DoNotMarkStackAsExecutable_NotApplicable()
        {
            this.VerifyNotApplicable(new EnablePositionIndependentExecutable(), new HashSet<string>());
        }

        [Fact]
        public void EnableReadOnlyRelocations_Pass()
        {
            this.VerifyPass(new EnableReadOnlyRelocations());
        }

        [Fact]
        public void EnableReadOnlyRelocations_Fail()
        {
            this.VerifyFail(new EnableReadOnlyRelocations());
        }

        [Fact]
        public void EnableReadOnlyRelocations_NotApplicable()
        {
            this.VerifyNotApplicable(new EnablePositionIndependentExecutable(), new HashSet<string>());
        }

        [Fact]
        public void EnableStackProtector_Pass()
        {
            this.VerifyPass(new EnableStackProtector());
        }

        [Fact]
        public void EnableStackProtector_Fail()
        {
            this.VerifyFail(new EnableStackProtector());
        }

        [Fact]
        public void EnableStackProtector_NotApplicable()
        {
            this.VerifyNotApplicable(new EnablePositionIndependentExecutable(), new HashSet<string>());
        }

        [Fact]
        public void UseCheckedFunctionsWithGCC_Pass()
        {
            this.VerifyPass(new UseCheckedFunctionsWithGcc());
        }

        [Fact]
        public void UseCheckedFunctionsWithGCC_Fail()
        {
            this.VerifyFail(new UseCheckedFunctionsWithGcc());
        }

        [Fact]
        public void UseCheckedFunctionsWithGCC_NotApplicable()
        {
            this.VerifyNotApplicable(new UseCheckedFunctionsWithGcc(), new HashSet<string>());
        }
    }
}