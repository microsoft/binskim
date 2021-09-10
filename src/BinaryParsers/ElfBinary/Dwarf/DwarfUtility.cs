// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public static class DwarfUtility
    {
        /// <summary>
        /// Gets distinct names of the modules
        /// </summary>
        /// <param name="commandLineInfoList">The command line info list to get names from</param>
        /// <param name="defaultName">Default name to return</param>
        /// <returns>Distinct names of the modules</returns>
        public static string GetDistinctNames(List<DwarfCompileCommandLineInfo> commandLineInfoList, string defaultName)
        {
            var notEmptyDistinctList = commandLineInfoList.Select(info => info.FileName)
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName)).Distinct().ToList();

            return notEmptyDistinctList.Count == 0 ? defaultName : string.Join(", ", notEmptyDistinctList);
        }
    }
}
