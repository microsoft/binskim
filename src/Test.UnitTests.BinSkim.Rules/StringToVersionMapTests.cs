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
                    Key = null
                },
                new StringToVersionMapTestCase
                {
                    Map = new StringToVersionMap(),
                    Key = null
                },
                new StringToVersionMapTestCase
                {
                    Map = null,
                    Key = key1
                },
                new StringToVersionMapTestCase
                {
                    Map = stringToVersionMap,
                    Key = key1
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
                foreach (bool returnMaxValueIfKeyDoesNotExist in new[] { true, false })
                {
                    Version currentVersion = testCase.Map.GetVersionByKey(testCase.Key, returnMaxValueIfKeyDoesNotExist);
                    Version expectedVersion = testCase.ExpectedVersion ??
                        (returnMaxValueIfKeyDoesNotExist ? StringToVersionMapExtensions.s_maxVersion : StringToVersionMapExtensions.s_minVersion);

                    if (currentVersion != expectedVersion)
                    {
                        sb.AppendLine($"The test was expecting '{expectedVersion}' but found '{currentVersion}'" +
                            $"for '{testCase.Map}' and '{testCase.Key}'");
                    }
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
