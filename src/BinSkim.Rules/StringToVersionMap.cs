// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // TODO: move into driver utilities?
    public class StringToVersionMap : TypedPropertiesDictionary<Version>
    {
    }

    public static class StringToVersionMapExtensions
    {
        internal static readonly Version MaxVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

        public static Version GetVersionByKey(this StringToVersionMap stringToVersionMap, string key)
        {
            if (stringToVersionMap == null || string.IsNullOrWhiteSpace(key))
            {
                return MaxVersion;
            }

            if (!stringToVersionMap.TryGetValue(key, out Version version))
            {
                version = MaxVersion;
            }

            return version;
        }
    }
}
