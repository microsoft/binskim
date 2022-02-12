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
        internal static readonly Version s_maxVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        internal static readonly Version s_minVersion = new Version(0, 0, 0, 0);

        public static Version GetVersionByKey(this StringToVersionMap stringToVersionMap, string key, bool returnMaxValueIfKeyDoesNotExist = false)
        {
            if (stringToVersionMap == null || string.IsNullOrWhiteSpace(key))
            {
                return returnMaxValueIfKeyDoesNotExist ? s_maxVersion : s_minVersion;
            }

            if (!stringToVersionMap.TryGetValue(key, out Version version))
            {
                return returnMaxValueIfKeyDoesNotExist ? s_maxVersion : s_minVersion;
            }

            return version;
        }
    }
}
