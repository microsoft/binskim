// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class MachOCompiler : ICompiler
    {
        // Regular expressions for extracting compiler types.
        // These should be ordered so that the generic "catch all" mapping to unknown is last.
        private static readonly List<(Regex Regex, ELFCompilerType Compiler)> compilerRegexes = new List<(Regex, ELFCompilerType)>
        {
            (new Regex(@"GNU .+"), ELFCompilerType.GCC), // e.g. "GNU C17 11.1.0 -fPIC -mmacosx-version-min=10.15.0 -mtune=core2 -gdwarf-5"
            (new Regex(@".*clang version.*"), ELFCompilerType.Clang), // e.g. "clang version 7.0.1-8+deb10u2 (tags/RELEASE_701/final)"
            (new Regex(@".*"), ELFCompilerType.Unknown)
        };

        // Regex for extracting something that looks like a version number--Goal is to match at least w.x, and up to w.x.y.z
        private static readonly Regex versionRegex = new Regex(@"\d+(\.\d+){1,3}");

        /// <summary>
        /// Construct a MachOCompiler from a string from the dwarf CompilationUnits producer string.
        /// </summary>
        /// <param name="producerString">string from the dwarf CompilationUnits producer.</param>
        public MachOCompiler(string producerString)
        {
            // If for some reason we get a null string, we will simply return an unknown compiler.
            if (producerString == null)
            {
                this.FullDescription = string.Empty;
                this.Compiler = ELFCompilerType.Unknown;
                this.Version = new Version(0, 0, 0, 0);
            }
            else
            {
                this.FullDescription = producerString;
                this.Compiler = compilerRegexes.First(s => s.Regex.IsMatch(producerString)).Compiler;

                try
                {
                    Match versionStr = versionRegex.Match(producerString);
                    if (versionStr.Success)
                    {
                        this.Version = new Version(versionStr.Value);
                    }
                    else
                    {
                        this.Version = new Version(0, 0, 0, 0);
                    }
                }
                catch (FormatException) // Version we recovered wasn't well formed.
                {
                    this.Version = new Version(0, 0, 0, 0);
                }
            }
        }

        public ELFCompilerType Compiler { get; private set; }

        public Version Version { get; private set; }

        public string FullDescription { get; private set; }
    }
}
