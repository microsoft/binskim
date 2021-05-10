// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public enum ELFCompilerType
    {
        Unknown = 0,
        Clang = 1,
        GCC = 2
    }

    /// <summary>
    /// Many compilers on Linux note which compiler and version was used to build that code
    /// by adding an entry to the '.comment' section (represented as a series of null terminated ascii strings).
    /// 
    /// This class takes those strings and attempts to match them with a known toolchain & version.
    /// </summary>
    public class ELFCompiler
    {
        // Regular expressions for extracting compiler types.
        // These should be ordered so that the generic "catch all" mapping to unknown is last.
        private static readonly List<(Regex Regex, ELFCompilerType Compiler)> compilerRegexes = new List<(Regex, ELFCompilerType)>
        {
            (new Regex(@"GCC:.+"), ELFCompilerType.GCC),
            (new Regex(@".*clang version.*"), ELFCompilerType.Clang),
            (new Regex(@".*"), ELFCompilerType.Unknown)
        };

        // Regex for extracting something that looks like a version number--Goal is to match at least w.x, and up to w.x.y.z
        private static readonly Regex versionRegex = new Regex(@"\d+(\.\d+){1,3}");

        /// <summary>
        /// Construct a ELFCompiler from a string from the .comments section.
        /// </summary>
        /// <param name="fullDescription">Compiler entry from the .comments section of an ELF binary.</param>
        public ELFCompiler(string fullDescription)
        {
            // If for some reason we get a null string, we will simply return an unknown compiler.
            if (fullDescription == null)
            {
                this.FullDescription = string.Empty;
                this.Compiler = ELFCompilerType.Unknown;
                this.Version = new Version(0, 0, 0, 0);
            }
            else
            {
                this.FullDescription = fullDescription;
                this.Compiler = compilerRegexes.First(s => s.Regex.IsMatch(fullDescription)).Compiler;

                try
                {
                    Match versionStr = versionRegex.Match(fullDescription);
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
