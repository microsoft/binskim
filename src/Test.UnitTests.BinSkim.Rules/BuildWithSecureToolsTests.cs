// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class BuildWithSecureToolsTests
    {
        private static readonly Random s_random;
        private static readonly int s_randomSeed;

        static BuildWithSecureToolsTests()
        {
            s_randomSeed = (int)DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            s_random = new Random(s_randomSeed);
        }

        [Fact]
        public void BuildMinimumCompilersList_ShouldAlwaysBeOrdered()
        {
            var omDetails = new List<ObjectModuleDetails>();
            var context = new BinaryAnalyzerContext
            {
                Policy = new Sarif.PropertiesDictionary()
            };

            var languageToModules = new Dictionary<Language, List<ObjectModuleDetails>>();
            foreach (Language language in Enum.GetValues(typeof(Language)))
            {
                languageToModules.Add(language, omDetails);
            }

            string orderedVersions = BuildWithSecureTools.BuildMinimumCompilersList(context, languageToModules);

            Dictionary<Language, List<ObjectModuleDetails>> randomLanguageToModules = GenerateRandomLanguageList(omDetails);
            string versions = BuildWithSecureTools.BuildMinimumCompilersList(context, randomLanguageToModules);

            versions.Should().Be(orderedVersions);
        }

        private Dictionary<Language, List<ObjectModuleDetails>> GenerateRandomLanguageList(List<ObjectModuleDetails> omDetails)
        {
            var languageToModules = new Dictionary<Language, List<ObjectModuleDetails>>();

            var languages = new List<Language>(Enum.GetValues(typeof(Language)) as Language[]);
            foreach (Language language in languages.OrderBy(k => s_random.Next()))
            {
                languageToModules.Add(language, omDetails);
            }

            return languageToModules;
        }
    }
}
