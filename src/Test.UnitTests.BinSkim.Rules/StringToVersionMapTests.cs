// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class StringToVersionMapTests
    {
        [Fact]
        public void StringToVersionMap_GetVersionByKey_ShouldNotKeyNotFoundException_IfKeyDoesNotExist()
        {
            const string key1 = "key1";

            StringToVersionMap stringToVersionMap = BuildWithSecureTools.BuildMinimumToolVersionsMap();

            var testCases = new List<StringToVersionMapTestCase>
            {
                new StringToVersionMapTestCase
                {
                    Map = null,
                    Key = null,
                    ExpectedVersion = BuildWithSecureTools.MaxVersion,
                },
                new StringToVersionMapTestCase
                {
                    Map = new StringToVersionMap(),
                    Key = null,
                    ExpectedVersion = BuildWithSecureTools.MaxVersion
                },
                new StringToVersionMapTestCase
                {
                    Map = null,
                    Key = key1,
                    ExpectedVersion = BuildWithSecureTools.MaxVersion
                },
                new StringToVersionMapTestCase
                {
                    Map = stringToVersionMap,
                    Key = key1,
                    ExpectedVersion = BuildWithSecureTools.MaxVersion
                },
            };

            foreach (KeyValuePair<string, Version> item in stringToVersionMap)
            {
                testCases.Add(new StringToVersionMapTestCase
                {
                    Map = stringToVersionMap,
                    Key = item.Key,
                    ExpectedVersion = item.Value
                });
            }

            var sb = new StringBuilder();
            foreach (StringToVersionMapTestCase testCase in testCases)
            {
                Version currentVersion = testCase.Map.GetVersionByKey(testCase.Key);
                if (currentVersion != testCase.ExpectedVersion)
                {
                    sb.AppendLine($"The test was expecting '{testCase.ExpectedVersion}' but found '{currentVersion}'" +
                        $"for '{testCase.Map}' and '{testCase.Key}'");
                }
            }

            sb.Length.Should().Be(0, sb.ToString());
        }

        private struct StringToVersionMapTestCase
        {
            public StringToVersionMap Map { get; set; }
            public string Key { get; set; }
            public Version ExpectedVersion { get; set; }
        }
    }
}
