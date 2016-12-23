// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    // TODO: Consider merging this function into Symbol. (Most of the code to support it is there anyway)

    /// <summary>A set of relatively expensive to calculate details from an object module.</summary>
    public class ObjectModuleDetails
    {
        private readonly CompilerCommandLine _commandLine;
        private readonly Version _frontEndVersion;
        private readonly Version _backEndVersion;
        private readonly Language _language;
        private readonly string _compiler;
        private readonly bool _hasSecurityChecks;
        private readonly bool _hasDebugInfo;

        /// <summary>Initializes a new instance of the <see cref="ObjectModuleDetails"/> class.</summary>
        /// <param name="frontEndVersion">The front end version of the compiler producing the
        /// object module.</param>
        /// <param name="backEndVersion">The back end version of the compiler producing the object
        /// module.</param>
        /// <param name="commandLine">The command line passed to the compiler used to build the
        /// object module.</param>
        public ObjectModuleDetails(Version frontEndVersion, Version backEndVersion, string commandLine, Language language, string compiler, bool hasSecurityChecks, bool hasDebugInfo)
        {
            _commandLine = new CompilerCommandLine(commandLine ?? String.Empty);
            _frontEndVersion = frontEndVersion ?? new Version();
            _backEndVersion = backEndVersion ?? new Version();
            _language = language;
            _compiler = compiler;
            _hasSecurityChecks = hasSecurityChecks;
            _hasDebugInfo = hasDebugInfo;
        }

        /// <summary>
        /// Returns the warning level as defined by the /Wn switch in the compiland command line
        /// </summary>
        public int WarningLevel
        {
            get
            {
                return _commandLine.WarningLevel;
            }
        }

        /// <summary>
        /// Returns a list of integers corresponding to the set of warnings disabled via -wdnnnn switches on the command line
        /// </summary>
        public ImmutableArray<int> ExplicitlyDisabledWarnings
        {
            get
            {
                return _commandLine.WarningsExplicitlyDisabled;
            }
        }

        /// <summary>
        /// The raw command line passed to the compiler when building this object module.
        /// </summary>
        public string CommandLine
        {
            get
            {
                return _commandLine.Raw;
            }
        }

        /// <summary>
        /// The version of the compiler backend
        /// </summary>
        public Version CompilerVersion
        {
            get
            {
                return _backEndVersion;
            }
        }

        /// <summary>
        /// The version of the compiler frontend
        /// </summary>
        public Version CompilerFrontEndVersion
        {
            get
            {
                return _frontEndVersion;
            }
        }

        /// <summary>
        /// Determines the language of this object module.
        /// </summary>
        public Language Language
        {
            get
            {
                return _language;
            }
        }

        public string Compiler
        {
            get
            {
                return _compiler;
            }
        }

        Nullable<WellKnownCompilers> _wellKnownCompiler;
        public WellKnownCompilers WellKnownCompiler
        {
            get
            {
                if (!_wellKnownCompiler.HasValue) { ComputeWellKnownCompilerValue(); }
                return _wellKnownCompiler.Value;
            }
        }

        private void ComputeWellKnownCompilerValue()
        {
            _wellKnownCompiler = WellKnownCompilers.Unknown;

            if ((this.Language == Language.C || this.Language == Language.Cxx) &&
                this.Compiler == "Microsoft (R) Optimizing Compiler")
            {
                _wellKnownCompiler = WellKnownCompilers.MicrosoftNativeCompiler;
            }
        }

        public bool HasSecurityChecks
        {
            get
            {
                return _hasSecurityChecks;
            }
        }

        public bool HasDebugInfo
        {
            get
            {
                return _hasDebugInfo;
            }
        }
    }

    public enum Language : uint
    {
        C,
        Cxx,
        FORTRAN,
        MASM,
        Pascal,
        Basic,
        COBOL,
        LINK,
        CVTRES,
        CVTPGD,
        CSharp,
        VisualBasic,
        ILASM,
        Java,
        JScript,
        MSIL,
        HLSL,
        Unknown
    }

    [Flags]
    public enum WellKnownCompilers
    {
        Unknown,
        MicrosoftNativeCompiler = 0x1
    }
}
