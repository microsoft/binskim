// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;

using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>Processes command lines stored by linkers in PDBs.</summary>
    internal struct LinkerCommandLine
    {
        /// <summary>
        /// The raw, unadulterated command line before processing.
        /// </summary>
        public readonly string Raw;

        /// <summary>
        /// Whether or not this command line enables incremental linking
        /// </summary>
        public readonly bool IncrementalLinking;

        /// <summary>
        /// Whether or not this command line enables Link Time Code Generation (/LTCG)
        /// </summary>
        public readonly bool LinkTimeCodeGenerationEnabled;

        /// <summary>
        /// Whether or not this command line enables Optimize References (/OPT:REF)
        /// </summary>
        public readonly bool OptimizeReferencesEnabled;

        /// <summary>
        /// Whether or not this command line enables Identical COMDAT folding (/OPT:ICF)
        /// </summary>
        public readonly bool COMDATFoldingEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkerCommandLine"/> struct from a raw PDB-supplied command line.
        /// </summary>
        /// <param name="commandLine">The raw command line from the PDB.</param>
        public LinkerCommandLine(string commandLine)
        {
            this.Raw = commandLine ?? "";
            this.IncrementalLinking = false;
            this.LinkTimeCodeGenerationEnabled = false;

            // "By default, /OPT:REF is enabled by the linker unless /OPT:NOREF or /DEBUG is specified."
            // https://docs.microsoft.com/cpp/build/reference/opt-optimizations?view=msvc-170#arguments
            this.OptimizeReferencesEnabled = true;

            // "By default, /OPT:ICF is enabled by the linker unless /OPT:NOICF or /DEBUG is specified."
            // https://docs.microsoft.com/cpp/build/reference/opt-optimizations?view=msvc-170#arguments
            this.COMDATFoldingEnabled = false;

            // https://docs.microsoft.com/cpp/build/reference/incremental-link-incrementally?view=msvc-170
            bool debugSet = false;
            bool optLbr = false;
            bool order = false;
            bool explicitlyEnabled = false;
            bool explicitlyDisabled = false;
            foreach (string argument in ArgumentSplitter.CommandLineToArgvW(commandLine))
            {
                if (!CommandLineHelper.IsCommandLineOption(argument))
                {
                    continue;
                }

                // There are multiple /debug options so use StartsWith
                if (ArgumentStartsWith(argument, "debug"))
                {
                    debugSet = true;

                    // "/DEBUG changes the defaults for the /OPT option from REF to NOREF and from ICF to NOICF, so if you
                    //  want the original defaults, you must explicitly specify /OPT:REF or /OPT:ICF."
                    // https://docs.microsoft.com/cpp/build/reference/debug-generate-debug-info?view=msvc-170#remarks
                    this.OptimizeReferencesEnabled = false;
                    this.COMDATFoldingEnabled = false;
                }
                else if (ArgumentStartsWith(argument, "opt"))
                {
                    // "The /OPT arguments may be specified together, separated by commas. For example, instead of
                    //  /OPT:REF /OPT:NOICF, you can specify /OPT:REF,NOICF."
                    // https://docs.microsoft.com/cpp/build/reference/opt-optimizations?view=msvc-170#remarks
                    //
                    // Make sure to match the more-specific noX before X because we are using contains and both would
                    // match the enabled version.
                    if (argument.Contains("noref", System.StringComparison.OrdinalIgnoreCase))
                    {
                        this.OptimizeReferencesEnabled = false;
                    }
                    else if (argument.Contains("ref", System.StringComparison.OrdinalIgnoreCase))
                    {
                        this.OptimizeReferencesEnabled = true;

                        // OPT:REF implicitly enables OPT:ICF.  This does not appear to be publicly documented.
                        this.COMDATFoldingEnabled = true;
                    }
                    else if (argument.Contains("noicf", System.StringComparison.OrdinalIgnoreCase))
                    {
                        this.COMDATFoldingEnabled = false;
                    }
                    else if (argument.Contains("icf", System.StringComparison.OrdinalIgnoreCase))
                    {
                        this.COMDATFoldingEnabled = true;
                    }
                    else if (argument.Contains("nolbr", System.StringComparison.OrdinalIgnoreCase))
                    {
                        optLbr = false;
                    }
                    else if (argument.Contains("lbr", System.StringComparison.OrdinalIgnoreCase))
                    {
                        optLbr = true;
                    }
                }
                else if (ArgumentEquals(argument, "order"))
                {
                    order = true;
                }
                else if (ArgumentEquals(argument, "incremental"))
                {
                    explicitlyEnabled = true;
                    explicitlyDisabled = false; // Assume that if specified multiple times the last wins
                }
                else if (ArgumentEquals(argument, "incremental:no"))
                {
                    explicitlyDisabled = true;
                    explicitlyEnabled = false; // Assume that if specified multiple times the last wins
                }
                else if (ArgumentEquals(argument, "LTCG:OFF"))
                {
                    this.LinkTimeCodeGenerationEnabled = false;
                }
                else if (ArgumentStartsWith(argument, "LTCG"))
                {
                    this.LinkTimeCodeGenerationEnabled = true;
                }
            }

            // Explicit enable or disable wins.
            // If /debug is set then incremental is implied unless certain other flags convert it back to false
            // If nothing is specified then it is disabled.
            if (explicitlyEnabled)
            {
                this.IncrementalLinking = true;
            }
            else if (explicitlyDisabled)
            {
                this.IncrementalLinking = false;
            }
            else if (debugSet)
            {
                this.IncrementalLinking = !this.OptimizeReferencesEnabled && !this.COMDATFoldingEnabled && !optLbr && !order;
            }
            else
            {
                this.IncrementalLinking = false;
            }
        }

        public static bool IsLinkerCommandLine(string commandLine)
        {
            // The command line for link.exe should contain /OUT with the final binary name.  Use that to separate compiler and linker command lines.
            // We should accept both / and - as the argument prefix.  The argument is case insensitive.  We do not know where in the command-line the
            // OUT argument will be provided so we use Contains.
            return ((commandLine != null) &&
                    (commandLine.Contains("/OUT", System.StringComparison.OrdinalIgnoreCase) || commandLine.Contains("-OUT", System.StringComparison.OrdinalIgnoreCase)));
        }

        private static bool ArgumentStartsWith(string target, string argument)
        {
            // Arguments are expected to begin with a - or a /.  Accept either.  They are not case sensitive.
            if (target.Length == 0)
            {
                return false;
            }
            else if ((target[0] != '/') && (target[0] != '-'))
            {
                return false;
            }

            return target.AsSpan().Slice(1).StartsWith(argument, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ArgumentEquals(string target, string argument)
        {
            // Arguments are expected to begin with a - or a /.  Accept either.  They are not case sensitive.
            if (target.Length == 0)
            {
                return false;
            }
            else if ((target[0] != '/') && (target[0] != '-'))
            {
                return false;
            }

            return target.AsSpan().Slice(1).Equals(argument, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
