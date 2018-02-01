// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public enum ELFCompilerType
    {
        Unknown = 0,
        Clang = 1,
        GCC = 2
    }

    public class ELFCompiler
    {
        static Dictionary<Regex, ELFCompilerType> compilerRegexes = new Dictionary<Regex, ELFCompilerType>()
        {
            {
                new Regex(@"GCC:.+"), ELFCompilerType.GCC
            },
            {
                new Regex(@".*clang version.*"), ELFCompilerType.Clang
            },
            {
                new Regex(@".*"), ELFCompilerType.Unknown
            }
        };

        /// <summary>
        /// Regular expressions for extracting the version number from the compiler string
        /// </summary>
        static Dictionary<ELFCompilerType, Regex> versionNumberRegex = new Dictionary<ELFCompilerType, Regex>()
        {
            {
                ELFCompilerType.GCC, new Regex(@"\d+\.\d+\.\d+[\-|~|A-Z|a-z|\.|\d]+")
            },
            {
                ELFCompilerType.Clang, new Regex(@"[\d+]\.[\d+]\.[\d+][\-|A-Z|a-z|\d]*")
            },
            {
                ELFCompilerType.Unknown, new Regex("")
            }
        };

        public ELFCompiler(string fullDescription)
        {
            FullDescription = fullDescription;
            Compiler = compilerRegexes.First(s => s.Key.IsMatch(fullDescription)).Value;

            try
            {
                Match result = versionNumberRegex[Compiler].Match(fullDescription);
                if (result.Success)
                {
                    Version = new Version(result.Value);
                }
                else
                {
                    Version = new Version(0, 0, 0, 0);
                }
            }
            catch (ArgumentException) // Version wasn't well formed.
            {
                Version = new Version(0, 0, 0, 0);
            }
        }

        public ELFCompilerType Compiler { get; private set; }

        public Version Version { get; private set; }

        public string FullDescription { get; private set; }
    }
}
