// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;
using Xunit.Abstractions;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class RuleTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public RuleTests(ITestOutputHelper output)
        {
            _testOutputHelper = output;
        }

        private void VerifyPass(
            IBinarySkimmer skimmer, 
            IEnumerable<string> additionalTestFiles = null,
            bool useDefaultPolicy = false)
        {
            Verify(skimmer, additionalTestFiles, useDefaultPolicy, expectToPass: true);
        }

        private void VerifyFail(
            IBinarySkimmer skimmer,
            IEnumerable<string> additionalTestFiles = null,
            bool useDefaultPolicy = false)
        {
            Verify(skimmer, additionalTestFiles, useDefaultPolicy, expectToPass: false);
        }

        private void Verify(
            IBinarySkimmer skimmer,
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
                context = CreateContext(logger, policy, target);

                if(!context.IsValidAnalysisTarget) { continue; }

                context.Rule = skimmer;

                string reasonForNotAnalyzing;
                if (skimmer.CanAnalyze(context, out reasonForNotAnalyzing) != AnalysisApplicability.ApplicableToSpecifiedTarget)
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
                _testOutputHelper.WriteLine(sb.ToString());
            }

            Assert.Equal(0, sb.Length);
            Assert.Empty(expected);
            Assert.Empty(other);
        }

        private BinaryAnalyzerContext CreateContext(TestMessageLogger logger, PropertiesDictionary policy, string target)
        {
            var context = new BinaryAnalyzerContext();
            context.Logger = logger;
            context.Policy = policy;

            if (target != null)
            {
                context.TargetUri = new Uri(target);
            }

            return context;
        }

        private void VerifyThrows<ExceptionType>(
            IBinarySkimmer skimmer,
            IEnumerable<string> additionalTestFiles = null,
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

            foreach(var testDirectory in testFilesDirectories)
            {
                if(Directory.Exists(testDirectory))
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
                context = CreateContext(logger, policy, target);

                context.Rule = skimmer;

                if (skimmer.CanAnalyze(context, out string reasonForNotAnalyzing) != AnalysisApplicability.ApplicableToSpecifiedTarget)
                {
                    continue;
                }
                Assert.Throws<ExceptionType>(() => skimmer.Analyze(context));
            }
        }

        private void VerifyNotApplicable(
            IBinarySkimmer skimmer,
            HashSet<string> notApplicableConditions,
            AnalysisApplicability expectedApplicability = AnalysisApplicability.NotApplicableToSpecifiedTarget,
            bool useDefaultPolicy = false)
        {
            string ruleName = skimmer.GetType().Name;
            string testFilesDirectory = ruleName;
            testFilesDirectory = Path.Combine(Environment.CurrentDirectory, "FunctionalTestsData", testFilesDirectory);
            testFilesDirectory = Path.Combine(testFilesDirectory, "NotApplicable");

            var context = new BinaryAnalyzerContext();

            HashSet<string> targets = GetTestFilesMatchingConditions(notApplicableConditions);

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
                
                context = CreateContext(logger, null, target);
                if (!context.IsValidAnalysisTarget) { continue; }

                if (useDefaultPolicy)
                {
                    context.Policy = new PropertiesDictionary();
                }

                context.Rule = skimmer;

                string reasonForNotAnalyzing;
                AnalysisApplicability applicability;
                applicability = skimmer.CanAnalyze(context, out reasonForNotAnalyzing);
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
                _testOutputHelper.WriteLine(sb.ToString());
            }

            Assert.Equal(0, sb.Length);
        }

        private HashSet<string> GetTestFilesMatchingConditions(HashSet<string> metadataConditions)
        {
            string testFilesDirectory;
            testFilesDirectory = Path.Combine(Environment.CurrentDirectory, "BaselineTestsData");

            Assert.True(Directory.Exists(testFilesDirectory));
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            if (metadataConditions.Contains(MetadataConditions.ImageIsILOnlyManagedAssembly))
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

            if (metadataConditions.Contains(MetadataConditions.ImageIsManagedInteropAssembly))
            {
                result.Add(Path.Combine(testFilesDirectory, "ManagedInteropAssemblyForAtlTestLibrary.dll"));
            }

            if (metadataConditions.Contains(MetadataConditions.ImageIsManagedResourceOnlyAssembly))
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

            return result;
        }

        [Fact]
        public void SetNoExecutableBit_Pass()
        {
            VerifyPass(new MarkImageAsNXCompatible());
        }

        [Fact]
        public void SetNoExecutableBit_Fail()
        {
            VerifyFail(new MarkImageAsNXCompatible());
        }

        [Fact]
        public void SetNoExecutableBit_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIs64BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsPreVersion7WindowsCEBinary);

            VerifyNotApplicable(new MarkImageAsNXCompatible(), notApplicableTo);
        }

        [Fact]
        public void EnableSpectreMitigations_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {   
                VerifyPass(new EnableSpectreMitigations(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableSpectreMitigations_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {   
                VerifyFail(new EnableSpectreMitigations(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsShared_Pass()
        {
            VerifyPass(new DoNotMarkWritableSectionsAsShared());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsShared_Fail()
        {
            VerifyFail(new DoNotMarkWritableSectionsAsShared());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsShared_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);

            VerifyNotApplicable(new DoNotMarkWritableSectionsAsShared(), notApplicableTo);
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsExecutable_Pass()
        {
            VerifyPass(new DoNotMarkWritableSectionsAsExecutable());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsExecutable_Fail()
        {
            VerifyFail(new DoNotMarkWritableSectionsAsExecutable());
        }

        [Fact]
        public void DoNotMarkWritableSectionsAsExecutable_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);

            VerifyNotApplicable(new DoNotMarkWritableSectionsAsExecutable(), notApplicableTo);
        }

        [Fact]
        public void EnableHighEntropyVirtualAddresses_Pass()
        {
            VerifyPass(new EnableHighEntropyVirtualAddresses());
        }

        [Fact]
        public void EnableHighEntropyVirtualAddresses_Fail()
        {
            VerifyFail(new EnableHighEntropyVirtualAddresses());
        }

        [Fact]
        public void EnableHighEntropyVirtualAddresses_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsNotExe);
            notApplicableTo.Add(MetadataConditions.ImageIsNot64BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);

            VerifyNotApplicable(new EnableHighEntropyVirtualAddresses(), notApplicableTo);
        }

        [Fact]
        public void EnableAddressSpaceLayoutRandomization_Pass()
        {
            VerifyPass(new EnableAddressSpaceLayoutRandomization());
        }

        [Fact]
        public void EnableAddressSpaceLayoutRandomization_Fail()
        {
            VerifyFail(new EnableAddressSpaceLayoutRandomization());
        }

        [Fact]
        public void EnableAddressSpaceLayoutRandomization_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsPreVersion7WindowsCEBinary);

            VerifyNotApplicable(new EnableAddressSpaceLayoutRandomization(), notApplicableTo);
        }

        [Fact]
        public void DoNotMarkImportsSectionAsExecutable_Pass()
        {
            VerifyPass(new DoNotMarkImportsSectionAsExecutable());
        }

        [Fact]
        public void DoNotMarkImportsSectionAsExecutable_Fail()
        {
            VerifyFail(new DoNotMarkImportsSectionAsExecutable());
        }

        [Fact]
        public void DoNotMarkImportsSectionAsExecutable_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);

            VerifyNotApplicable(new DoNotMarkImportsSectionAsExecutable(), notApplicableTo);
        }

        [Fact]
        public void LoadImageAboveFourGigabyteAddress_Fail()
        {
            VerifyFail(new LoadImageAboveFourGigabyteAddress());
        }

        [Fact]
        public void LoadImageAboveFourGigabyteAddress_Pass()
        {
            VerifyPass(new LoadImageAboveFourGigabyteAddress());
        }

        [Fact]
        public void LoadImageAboveFourGigabyteAddress_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsNot64BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);

            VerifyNotApplicable(new LoadImageAboveFourGigabyteAddress(), notApplicableTo);
        }

        [Fact]
        public void EnableSafeSEH_Fail()
        {
            VerifyFail(new EnableSafeSEH());
        }

        [Fact]
        public void EnableSafeSEH_Pass()
        {
            VerifyPass(new EnableSafeSEH());
        }

        [Fact]
        public void EnableSafeSEH_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();
            notApplicableTo.Add(MetadataConditions.ImageIsNot32BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);

            VerifyNotApplicable(new EnableSafeSEH(), notApplicableTo);
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyFail(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            } 
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyPass(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIs64BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsManagedInteropAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsManagedResourceOnlyAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsNot32BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsNot64BitBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsPreVersion7WindowsCEBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);

            VerifyNotApplicable(new DoNotShipVulnerableBinaries(), notApplicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
        }

        [Fact]
        public void EnableStackProtection_Fail()
        { 
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyFail(new EnableStackProtection()); 
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableStackProtection_Pass()
        { 
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyPass(new EnableStackProtection());
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableStackProtection_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            VerifyNotApplicable(new EnableStackProtection(), notApplicableTo);
        }

        [Fact]
        public void InitializeStackProtection_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
                VerifyFail(
                    new InitializeStackProtection(),
                    GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
        }

        [Fact]
        public void InitializeStackProtection_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyPass(new InitializeStackProtection());
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void InitializeStackProtection_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            VerifyNotApplicable(new InitializeStackProtection(), notApplicableTo);
        }

        [Fact]
        public void DoNotModifyStackProtectionCookie_Fail()
        {
            VerifyFail(new DoNotModifyStackProtectionCookie());
        }

        [Fact]
        public void DoNotModifyStackProtectionCookie_Pass()
        {
            VerifyPass(new DoNotModifyStackProtectionCookie());
        }

        [Fact]
        public void DoNotModifyStackProtectionCookie_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            VerifyNotApplicable(new DoNotModifyStackProtectionCookie(), notApplicableTo);
        }

        private static HashSet<string> GetNotApplicableBinariesForStackProtectionFeature()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsDotNetNativeBinary);
            return notApplicableTo;
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
                VerifyFail(
                    new DoNotDisableStackProtectionForFunctions(),
                    GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyPass(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);

            VerifyNotApplicable(new DoNotDisableStackProtectionForFunctions(), notApplicableTo);
            
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                HashSet<string> applicableTo = new HashSet<string>();
                applicableTo.Add(MetadataConditions.ImageIs64BitBinary);
                VerifyNotApplicable(new DoNotDisableStackProtectionForFunctions(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
            }
            
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
                VerifyFail(
                    new EnableCriticalCompilerWarnings(),
                    GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyPass(new EnableCriticalCompilerWarnings(), useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_NotApplicable()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            { 
                HashSet<string> notApplicableTo = new HashSet<string>();

                notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
                notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);

                VerifyNotApplicable(new EnableCriticalCompilerWarnings(), notApplicableTo);

                HashSet<string> applicableTo = new HashSet<string>();
                applicableTo.Add(MetadataConditions.ImageIs64BitBinary);

                VerifyNotApplicable(
                    new EnableCriticalCompilerWarnings(), 
                    applicableTo, 
                    AnalysisApplicability.ApplicableToSpecifiedTarget);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void EnableControlFlowGuard_Fail()
        {
            VerifyFail(new EnableControlFlowGuard(), useDefaultPolicy : true);
        }

        [Fact]
        public void EnableControlFlowGuard_Pass()
        {
            VerifyPass(new EnableControlFlowGuard(), useDefaultPolicy : true);
        }

        [Fact]
        public void EnableControlFlowGuard_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsMixedModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsKernelModeBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);

            VerifyNotApplicable(new EnableControlFlowGuard(), notApplicableTo, useDefaultPolicy : true);
        }

        [Fact]
        public void BuildWithSecureTools_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {    
                HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
                VerifyFail(
                    new BuildWithSecureTools(),
                    GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void BuildWithSecureTools_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {    
                VerifyPass(new BuildWithSecureTools(), useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void BuildWithSecureTools_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);

            VerifyNotApplicable(new BuildWithSecureTools(), notApplicableTo);
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            { 
                HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
                VerifyFail(
                    new DoNotIncorporateVulnerableDependencies(),
                    GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            { 
                VerifyPass(new DoNotIncorporateVulnerableDependencies(), useDefaultPolicy: true);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);

            VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), notApplicableTo);
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                HashSet<string> applicableTo = new HashSet<string>();
                applicableTo.Add(MetadataConditions.ImageIs64BitBinary);
                VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void SignSecurely_Fail()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                VerifyFail(new SignSecurely());
            }
            else 
            {
                VerifyThrows<PlatformNotSupportedException>(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void SignSecurely_Pass()
        {
            if(BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {    
                string kernel32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
                kernel32Path = Path.Combine(kernel32Path, "kernel32.dll");

                VerifyPass(new SignSecurely(), additionalTestFiles : new[] { kernel32Path });
            }
        }

        [Fact]
        public void SignSecurely_NotApplicable()
        {
            HashSet<string> applicableTo = new HashSet<string>();
            applicableTo.Add(MetadataConditions.ImageIsNotSigned);
            VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), applicableTo, AnalysisApplicability.NotApplicableToSpecifiedTarget);
        }
        
        [Fact]
        public void EnablePIEOnExecutables_Pass()
        {
            VerifyPass(new EnablePIEOnExecutables());
        }

        [Fact]
        public void EnablePIEOnExecutables_Fail()
        {
            VerifyFail(new EnablePIEOnExecutables());
        }

        [Fact]
        public void EnablePIEOnExecutables_NotApplicable()
        {
            VerifyNotApplicable(new EnablePIEOnExecutables(), new HashSet<string>());
        }

        [Fact]
        public void DoNotMarkStackAsExecutable_Pass()
        {
            VerifyPass(new DoNotMarkStackAsExecutable());
        }

        [Fact]
        public void DoNotMarkStackAsExecutable_Fail()
        {
            VerifyFail(new DoNotMarkStackAsExecutable());
        }

        [Fact]
        public void DoNotMarkStackAsExecutable_NotApplicable()
        {
            VerifyNotApplicable(new EnablePIEOnExecutables(), new HashSet<string>());
        }

        [Fact]
        public void EnableReadOnlyRelocations_Pass()
        {
            VerifyPass(new EnableReadOnlyRelocations());
        }

        [Fact]
        public void EnableReadOnlyRelocations_Fail()
        {
            VerifyFail(new EnableReadOnlyRelocations());
        }

        [Fact]
        public void EnableReadOnlyRelocations_NotApplicable()
        {
            VerifyNotApplicable(new EnablePIEOnExecutables(), new HashSet<string>());
        }

        [Fact]
        public void EnableStackProtector_Pass()
        {
            VerifyPass(new EnableStackProtector());
        }

        [Fact]
        public void EnableStackProtector_Fail()
        {
            VerifyFail(new EnableStackProtector());
        }

        [Fact]
        public void EnableStackProtector_NotApplicable()
        {
            VerifyNotApplicable(new EnablePIEOnExecutables(), new HashSet<string>());
        }

        [Fact]
        public void UseCheckedFunctionsWithGCC_Pass()
        {
            VerifyPass(new UseCheckedFunctionsWithGCC());
        }

        [Fact]
        public void UseCheckedFunctionsWithGCC_Fail()
        {
            VerifyFail(new UseCheckedFunctionsWithGCC());
        }

        [Fact]
        public void UseCheckedFunctionsWithGCC_NotApplicable()
        {
            VerifyNotApplicable(new UseCheckedFunctionsWithGCC(), new HashSet<string>());
        }
    }
}
