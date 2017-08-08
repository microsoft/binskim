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
                PE pe = new PE(target);
                if (!pe.IsPEFile) { continue; }

                context = CreateContext(logger, policy, target);

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
            Assert.Equal(0, expected.Count);
            Assert.Equal(0, other.Count);
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
                if (!AnalyzeCommand.ValidAnalysisFileExtensions.Contains(extension))
                {
                    Assert.True(false, "Test file with unexpected extension encountered: " + target);
                }

                context = CreateContext(logger, null, target);
                if (!context.PE.IsPEFile) { continue; }

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
            VerifyFail(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
        }

        [Fact]
        public void DoNotShipVulnerableBinaries_Pass()
        {
            VerifyPass(new DoNotShipVulnerableBinaries(), useDefaultPolicy: true);
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
            VerifyFail(new EnableStackProtection());
        }

        [Fact]
        public void EnableStackProtection_Pass()
        {
            VerifyPass(new EnableStackProtection());
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
            HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
            VerifyFail(
                new InitializeStackProtection(),
                GetTestFilesMatchingConditions(failureConditions),
                useDefaultPolicy: true);
        }

        [Fact]
        public void InitializeStackProtection_Pass()
        {
            VerifyPass(new InitializeStackProtection());
        }

        [Fact]
        public void InitializeStackProtection_NotApplicable()
        {
            HashSet<string> notApplicableTo = GetNotApplicableBinariesForStackProtectionFeature();

            VerifyNotApplicable(new InitializeStackProtection(), notApplicableTo);
        }

        [Fact]
        public void DoNotModifyStackProtectionCooke_Fail()
        {
            VerifyFail(new DoNotModifyStackProtectionCookie());
        }

        [Fact]
        public void DoNotModifyStackProtectionCooke_Pass()
        {
            VerifyPass(new DoNotModifyStackProtectionCookie());
        }

        [Fact]
        public void DoNotModifyStackProtectionCooke_NotApplicable()
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
            HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
            VerifyFail(
                new DoNotDisableStackProtectionForFunctions(),
                GetTestFilesMatchingConditions(failureConditions),
                useDefaultPolicy: true);
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_Pass()
        {
            VerifyPass(new DoNotDisableStackProtectionForFunctions(), useDefaultPolicy: true);
        }

        [Fact]
        public void DoNotDisableStackProtectionForFunctions_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);
            notApplicableTo.Add(MetadataConditions.ImageIsXBoxBinary);

            VerifyNotApplicable(new DoNotDisableStackProtectionForFunctions(), notApplicableTo);

            HashSet<string> applicableTo = new HashSet<string>();
            applicableTo.Add(MetadataConditions.ImageIs64BitBinary);
            VerifyNotApplicable(new DoNotDisableStackProtectionForFunctions(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_Fail()
        {
            HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
            VerifyFail(
                new EnableCriticalCompilerWarnings(),
                GetTestFilesMatchingConditions(failureConditions),
                useDefaultPolicy: true);
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_Pass()
        {
            VerifyPass(new EnableCriticalCompilerWarnings(), useDefaultPolicy: true);
        }

        [Fact]
        public void EnableCriticalCompilerWarnings_NotApplicable()
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
            HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
            VerifyFail(
                new BuildWithSecureTools(),
                GetTestFilesMatchingConditions(failureConditions),
                useDefaultPolicy: true);
        }

        [Fact]
        public void BuildWithSecureTools_Pass()
        {
            VerifyPass(new BuildWithSecureTools(), useDefaultPolicy: true);
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
            HashSet<string> failureConditions = new HashSet<string>(new string[] { MetadataConditions.CouldNotLoadPdb });
            VerifyFail(
                new DoNotIncorporateVulnerableDependencies(),
                GetTestFilesMatchingConditions(failureConditions),
                useDefaultPolicy: true);
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_Pass()
        {
            VerifyPass(new DoNotIncorporateVulnerableDependencies(), useDefaultPolicy: true);
        }

        [Fact]
        public void DoNotIncorporateVulnerableDependencies_NotApplicable()
        {
            HashSet<string> notApplicableTo = new HashSet<string>();

            notApplicableTo.Add(MetadataConditions.ImageIsILOnlyManagedAssembly);
            notApplicableTo.Add(MetadataConditions.ImageIsResourceOnlyBinary);

            VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), notApplicableTo);

            HashSet<string> applicableTo = new HashSet<string>();
            applicableTo.Add(MetadataConditions.ImageIs64BitBinary);
            VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
        }

        [Fact]
        public void SignSecurely_Fail()
        {
            VerifyFail(new SignSecurely());
        }

        [Fact]
        public void SignSecurely_Pass()
        {
            string kernel32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
            kernel32Path = Path.Combine(kernel32Path, "kernel32.dll");

            VerifyPass(new SignSecurely(), additionalTestFiles : new[] { kernel32Path });
        }

        [Fact]
        public void SignSecurely_NotApplicable()
        {
            HashSet<string> applicableTo = new HashSet<string>();
            applicableTo.Add(MetadataConditions.ImageIsNotSigned);
            VerifyNotApplicable(new DoNotIncorporateVulnerableDependencies(), applicableTo, AnalysisApplicability.NotApplicableToSpecifiedTarget);
        }
    }
}
