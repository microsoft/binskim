// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class BuildWithSecureToolsTests
    {
        private static readonly Version MinVersion = new Version();

        [Fact]
        public void RetrieveMinimumCompilerVersionByLanguage_ShouldRetrieveMinVersionWhenLanguagesDoNotExistInTheMap()
        {
            using var context = new BinaryAnalyzerContext
            {
                Policy = GeneratePolicyOptions(empty: true),
                Binary = GeneratePEBinary()
            };

            Version version = BuildWithSecureTools.RetrieveMinimumCompilerVersionByLanguage(context, Language.C);
            version.Should().Be(MinVersion);
        }

        [Fact]
        public void RetrieveMinimumCompilerVersionByLanguage_ShouldRetrieveVersionWhenLanguagesExistInTheMap()
        {
            using var context = new BinaryAnalyzerContext
            {
                Policy = GeneratePolicyOptions(empty: false),
                Binary = GeneratePEBinary()
            };

            foreach (Language language in Enum.GetValues(typeof(Language)))
            {
                Version version = BuildWithSecureTools.RetrieveMinimumCompilerVersionByLanguage(context, language);

                if (new[] { Language.C, Language.Cxx }.Contains(language))
                {
                    version.Should().NotBe(MinVersion);
                }
                else
                {
                    version.Should().Be(MinVersion);
                }
            }
        }

        private static PEBinary GeneratePEBinary()
        {
            string fileName = Path.Combine(PEBinaryTests.BaselineTestsDataDirectory, "Native_x64_VS2013_Default.dll");
            return new PEBinary(new Uri(fileName));
        }

        private static PropertiesDictionary GeneratePolicyOptions(bool empty)
        {
            var allOptions = new PropertiesDictionary();
            var buildWithSecureTools = new BuildWithSecureTools();
            string ruleOptionsKey = $"{buildWithSecureTools.Id}.{buildWithSecureTools.Name}.Options";

            allOptions[ruleOptionsKey] = new PropertiesDictionary();

            var properties = (PropertiesDictionary)allOptions[ruleOptionsKey];
            foreach (IOption option in buildWithSecureTools.GetOptions())
            {
                object values = option.DefaultValue;
                if (empty)
                {
                    values = new StringToVersionMap();
                }

                properties.SetProperty(option, values, cacheDescription: true, persistToSettingsContainer: false);
            }

            return allOptions;
        }
    }
}
