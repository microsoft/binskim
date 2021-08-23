// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public static class CommandLineHelper
    {
        private static readonly char[] switchPrefix = new char[] { '-', '/' };

        /// <summary>Possible status for commandline switches when searching the PDB (no default state is given).</summary>
        public enum SwitchState
        {
            SwitchNotFound = 0,
            SwitchEnabled = 1,
            SwitchDisabled = -1
        }

        /// <summary>Order of precendence for the provided switches to clarify what to do in the presence of multiple disagreeing copies of the switch.</summary>
        public enum OrderOfPrecedence
        {
            FirstWins = 0,
            LastWins = 1
        }

        public static bool IsCommandLineOption(string candidate)
        {
            if (candidate.Length < 2)
            {
                return false;
            }

            char c = candidate[0];
            return c == '/' || c == '-';
        }

        /// <summary>
        /// Get the value of a compiler command-line options
        /// </summary>
        /// <param name="commandLine">Command line to search</param>
        /// <param name="optionNames">Array of command line options to search for a value</param>
        /// <param name="precedence">The precedence ruls for this set of options</param>
        /// <param name="optionValue">string to recieve the value of the command line option</param>
        /// <returns>true if one of the options is found, false if none are found</returns>
        public static bool GetOptionValue(string commandLine, string[] optionNames, OrderOfPrecedence precedence, ref string optionValue)
        {
            bool optionFound = false;

            if (optionNames != null && optionNames.Length > 0)
            {
                // array of strings for the options name without the preceding switchPrefix to make comparison easier
                string[] optionsArray = new string[optionNames.Length];

                for (int index = 0; index < optionNames.Length; index++)
                {
                    // if present remove the slash or minus
                    optionsArray[index] = optionNames[index].TrimStart(switchPrefix);
                }

                foreach (string arg in ArgumentSplitter.CommandLineToArgvW(commandLine))
                {
                    if (IsCommandLineOption(arg))
                    {
                        string realArg = arg.TrimStart(switchPrefix);

                        // Check if this matches one of the names switches
                        for (int index = 0; index < optionsArray.Length; index++)
                        {
                            if (realArg.StartsWith(optionsArray[index]))
                            {
                                optionFound = true;
                                optionValue = realArg.Substring(optionsArray[index].Length);
                            }
                        }

                        if (optionFound == true &&
                            precedence == OrderOfPrecedence.FirstWins)
                        {
                            // we found a switch that impacts the desired state and FirstWins is set
                            break;
                        }
                    }
                }
            }

            return optionFound;
        }

        /// <summary>
        /// Determine if a switch is set,unset or not present on the command-line.
        /// </summary>
        /// <param name="commandLine">Command line to search</param>
        /// <param name="switchNames">Array of switches that alias each other and all set the same compiler state.</param>
        /// <param name="overrideNames">Array of switches that invalidate the state of the switches in switchNames.</param>
        /// <param name="defaultState">The default state of the switch should no instance of the switch or its overrides be found.</param>
        /// <param name="precedence">The precedence rules for this set of switches.</param>
        public static SwitchState GetSwitchState(string commandLine, string[] switchNames, string[] overrideNames, SwitchState defaultState, OrderOfPrecedence precedence)
        {
            // TODO-paddymcd-MSFT - This is an OK first pass.
            // Unfortunately composite switches get tricky and not all switches support the '-' semantics 
            // e.g. /O1- gets translated to /O1 /O-, the second of which is not supported.
            // Additionally, currently /d2guardspecload is translated into /guardspecload, which may be a bug for ENC
            SwitchState namedswitchesState = SwitchState.SwitchNotFound;

            if (switchNames != null && switchNames.Length > 0)
            {
                // array of strings for the switch name without the preceding switchPrefix to make comparison easier
                string[] switchArray = new string[switchNames.Length];
                string[] overridesArray = null;

                SwitchState namedoverridesState = SwitchState.SwitchNotFound;

                for (int index = 0; index < switchNames.Length; index++)
                {
                    // if present remove the slash or minus
                    switchArray[index] = switchNames[index].TrimStart(switchPrefix);
                }

                if (overrideNames != null && overrideNames.Length > 0)
                {
                    overridesArray = new string[overrideNames.Length];

                    for (int index = 0; index < overrideNames.Length; index++)
                    {
                        // if present remove the slash or minus
                        overridesArray[index] = overrideNames[index].TrimStart(switchPrefix);
                    }
                }

                foreach (string arg in ArgumentSplitter.CommandLineToArgvW(commandLine))
                {
                    if (IsCommandLineOption(arg))
                    {
                        string realArg = arg.TrimStart(switchPrefix);

                        // Check if this matches one of the names switches
                        for (int index = 0; index < switchArray.Length; index++)
                        {
                            if (realArg.StartsWith(switchArray[index]))
                            {
                                // partial stem match - now check if this is a full match or a match with a "-" on the end
                                if (realArg.Equals(switchArray[index]))
                                {
                                    namedswitchesState = SwitchState.SwitchEnabled;
                                    // not necessary at this time, but here for completeness...
                                    namedoverridesState = SwitchState.SwitchDisabled;
                                }
                                else if (realArg[switchArray[index].Length] == '-')
                                {
                                    namedswitchesState = SwitchState.SwitchDisabled;
                                }
                                // Else we have a stem match - do nothing
                            }
                        }

                        // check if this matches one of the named overrides
                        if (overridesArray != null)
                        {
                            for (int index = 0; index < overridesArray.Length; index++)
                            {
                                if (realArg.StartsWith(overridesArray[index]))
                                {
                                    // partial stem match - now check if this is a full match or a match with a "-" on the end
                                    if (realArg.Equals(overridesArray[index]))
                                    {
                                        namedoverridesState = SwitchState.SwitchEnabled;
                                        namedswitchesState = SwitchState.SwitchDisabled;
                                    }
                                    else if (realArg[overridesArray[index].Length] == '-')
                                    {
                                        namedoverridesState = SwitchState.SwitchDisabled;
                                        // Unsetting an override has no impact upon the named switches
                                    }
                                    // Else we have a stem match - do nothing
                                }
                            }
                        }

                        if (namedswitchesState != SwitchState.SwitchNotFound &&
                            namedoverridesState != SwitchState.SwitchNotFound &&
                            precedence == OrderOfPrecedence.FirstWins)
                        {
                            // we found a switch that impacts the desired state and FirstWins is set
                            break;
                        }
                    }
                }

                if (namedswitchesState == SwitchState.SwitchNotFound)
                {
                    if (namedoverridesState == SwitchState.SwitchEnabled)
                    {
                        namedswitchesState = SwitchState.SwitchDisabled;
                    }
                    else
                    {
                        namedswitchesState = defaultState;
                    }
                }
            }

            return namedswitchesState;
        }
    }
}
