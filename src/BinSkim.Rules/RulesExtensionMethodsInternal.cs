// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;
using System;
using System.Globalization;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public static class RulesExtensionMethodsInternal
    {
        public static bool IsAutomaticVariableInitializationEnabled(this ObjectModuleDetails objectModuleDetails)
        {
            string commandLine = objectModuleDetails.RawCommandLine;

            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return false;
            }

            /*
               * For `-d1initall*`, `-d1initAll:Mask*`, last one wins.
               *     e.g., "-d1initAll:Mask11 -d1initAll:Mask01" will be effective 1 only.
               *     e.g., "-d1initAll:Mask11 -d1initall03" will be effective 3 only.
               *     
               * For each set of `preset*`, last one wins as well.
               * 
               * `-preset*` will be able to enable flags that was not enabled by `-d1initall*`,
               *     e.g., "-d1initAll:Mask02 -presetScalars" will be effective 3.
               *     
               * but `-preset*-`will not be able to remove those already enabled from `-d1initall*`.
               *     e.g., "-d1initAll:Mask11 -presetScalars-" the result will still be effective 11. 
               * 
               * So the result is based on the `-d1initall*` and adjust it with the "final" `preset*` flags.
               * 
               * And there is also a `-presetArrays` which includes pointerArray.
               *     e.g., "-presetArraysOfPointers -presetArrays-" the result will still have 8 enabled.
               *     e.g., "-presetArrays -presetArraysOfPointers-" the result will still have 8 enabled.
               *     e.g., "-d1initall11 -presetArrays-" the result will still be effective 11.
           */

            // Check for `-presetScalars`, `-presetScalars-`
            bool scalarsEnabledFromPreset = false;

            // Check for `-presetAggregateStorage`, `-presetAggregateStorage-`, `-presetObjectStorage`, `-presetObjectStorage-`
            bool aggregatesEnabledFromPreset = false;

            // Check for `-presetArraysOfPointers`, `-presetArraysOfPointers-`
            bool pointerArrayEnabledFromPreset = false;

            // Check for `-presetArrays`, `-presetArrays-`
            bool pointerArrayEnabledFromPresetArrays = false;

            // Check for `-d1initall*`, `-d1initAll:Mask*`
            uint initAllMask = 0;

            foreach (string argument in ArgumentSplitter.CommandLineToArgvW(commandLine))
            {
                if (!CommandLineHelper.IsCommandLineOption(argument))
                {
                    continue;
                }

                if (argument.StartsWith("-d1initall", StringComparison.OrdinalIgnoreCase))
                {
                    string d1initAll = argument.Replace("-d1initAll:Mask", string.Empty, StringComparison.OrdinalIgnoreCase)
                        .Replace("-d1initall", string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (uint.TryParse(d1initAll, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint d1initAllValue))
                    {
                        initAllMask = d1initAllValue;
                    }
                }
                else if (argument.Equals("-presetScalars", StringComparison.OrdinalIgnoreCase))
                {
                    scalarsEnabledFromPreset = true;
                }
                else if (argument.Equals("-presetScalars-", StringComparison.OrdinalIgnoreCase))
                {
                    scalarsEnabledFromPreset = false;
                }
                else if (argument.Equals("-presetAggregateStorage", StringComparison.OrdinalIgnoreCase) ||
                    argument.Equals("-presetObjectStorage", StringComparison.OrdinalIgnoreCase))
                {
                    aggregatesEnabledFromPreset = true;
                }
                else if (argument.Equals("-presetAggregateStorage-", StringComparison.OrdinalIgnoreCase) ||
                    argument.Equals("-presetObjectStorage-", StringComparison.OrdinalIgnoreCase))
                {
                    aggregatesEnabledFromPreset = false;
                }
                else if (argument.Equals("-presetArraysOfPointers", StringComparison.OrdinalIgnoreCase))
                {
                    pointerArrayEnabledFromPreset = true;
                }
                else if (argument.Equals("-presetArraysOfPointers-", StringComparison.OrdinalIgnoreCase))
                {
                    pointerArrayEnabledFromPreset = false;
                }
                else if (argument.Equals("-presetArrays", StringComparison.OrdinalIgnoreCase))
                {
                    pointerArrayEnabledFromPresetArrays = true;
                }
                else if (argument.Equals("-presetArrays-", StringComparison.OrdinalIgnoreCase))
                {
                    pointerArrayEnabledFromPresetArrays = false;
                }
            }

            bool scalarsEnabledFromInitAllMask = (initAllMask & (uint)ZeroInitializationMode.Scalars) == (uint)ZeroInitializationMode.Scalars;
            bool aggregatesEnabledFromInitAllMask = (initAllMask & (uint)ZeroInitializationMode.Aggregates) == (uint)ZeroInitializationMode.Aggregates;
            bool pointerArrayEnabledFromInitAllMask = (initAllMask & (uint)ZeroInitializationMode.PointerArray) == (uint)ZeroInitializationMode.PointerArray;

            return
                (scalarsEnabledFromInitAllMask || scalarsEnabledFromPreset) &&
                (aggregatesEnabledFromInitAllMask || aggregatesEnabledFromPreset) &&
                (pointerArrayEnabledFromInitAllMask || pointerArrayEnabledFromPreset || pointerArrayEnabledFromPresetArrays);
        }
    }
}
