// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// Initializes a new instance of the <see cref="LinkerCommandLine"/> struct from a raw PDB-supplied command line.
        /// </summary>
        /// <param name="commandLine">The raw command line from the PDB.</param>
        public LinkerCommandLine(string commandLine)
        {
            this.Raw = commandLine ?? "";
            this.IncrementalLinking = false;

            // https://docs.microsoft.com/cpp/build/reference/incremental-link-incrementally?view=msvc-170
            bool debugSet = false;
            bool optRef = false;
            bool optIcf = false;
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
                if (argument.StartsWith("/debug", System.StringComparison.OrdinalIgnoreCase) || argument.StartsWith("-debug", System.StringComparison.OrdinalIgnoreCase))
                {
                    debugSet = true;
                }
                else if (string.Equals(argument, "/opt:ref", System.StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-opt:ref", System.StringComparison.OrdinalIgnoreCase))
                {
                    optRef = true;
                }
                else if (string.Equals(argument, "/opt:icf", System.StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-opt:icf", System.StringComparison.OrdinalIgnoreCase))
                {
                    optIcf = true;
                }
                else if (string.Equals(argument, "/opt:lbr", System.StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-opt:lbr", System.StringComparison.OrdinalIgnoreCase))
                {
                    optLbr = true;
                }
                else if (string.Equals(argument, "/order", System.StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-order", System.StringComparison.OrdinalIgnoreCase))
                {
                    order = true;
                }
                else if (string.Equals(argument, "/incremental", System.StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-incremental", System.StringComparison.OrdinalIgnoreCase))
                {
                    explicitlyEnabled = true;
                    explicitlyDisabled = false; // Assume that if specified multiple times the last wins
                }
                else if (string.Equals(argument, "/incremental:no", System.StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-incremental:no", System.StringComparison.OrdinalIgnoreCase))
                {
                    explicitlyDisabled = true;
                    explicitlyEnabled = false; // Assume that if specified multiple times the last wins
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
                this.IncrementalLinking = !optRef && !optIcf && !optLbr && !order;
            }
            else
            {
                this.IncrementalLinking = false;
            }
        }
    }
}
