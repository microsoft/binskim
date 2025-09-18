// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;

using static Microsoft.CodeAnalysis.BinaryParsers.CommandLineHelper;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    // TODO: Consider merging this function into Symbol. (Most of the code to support it is there anyway)

    /// <summary>A set of relatively expensive-to-calculate details from an object module.</summary>
    public class ObjectModuleDetails
    {
        private readonly CompilerCommandLine compilerCommandLine;
        private readonly LinkerCommandLine linkerCommandLine;

        /// <summary>Initializes a new instance of the <see cref="ObjectModuleDetails"/> class.</summary>
        /// <param name="compilerFrontEndVersion">The front end version of the compiler producing the object module.</param>
        /// <param name="backEndVersion">The back end version of the compiler producing the object module.</param>
        /// <param name="commandLine">The command line passed to the compiler used to build the object module.</param>
        /// <param name="compilerName">The compiler used to create the object module.</param>
        /// <param name="language">The language of the object module.</parm>
        /// <param name="hasSecurityChecks">A boolean that indicates whether the PE contains security checks.</param>
        /// <param name="hasDebugInfo">A boolean that indicates whether the PE comes with a corresponding PDB.</param>
        public ObjectModuleDetails(
            string name,
            string library,
            string compilerName,
            Version compilerFrontEndVersion,
            Version backEndVersion,
            string commandLine,
            Language language,
            bool hasSecurityChecks, bool hasDebugInfo)
        {
            this.Name = name;
            this.Library = library;
            this.CompilerFrontEndVersion = compilerFrontEndVersion ?? new Version();
            this.CompilerBackEndVersion = backEndVersion ?? new Version();

            if (LinkerCommandLine.IsLinkerCommandLine(commandLine))
            {
                this.linkerCommandLine = new LinkerCommandLine(commandLine);
                this.compilerCommandLine = new CompilerCommandLine(string.Empty);
            }
            else
            {
                this.linkerCommandLine = new LinkerCommandLine(string.Empty);
                this.compilerCommandLine = new CompilerCommandLine(commandLine ?? string.Empty);
            }
            this.Language = language;
            this.CompilerName = compilerName;
            this.HasSecurityChecks = hasSecurityChecks;
            this.HasDebugInfo = hasDebugInfo;
        }

        public string Name { get; private set; }

        public string Library { get; private set; }

        /// <summary>
        /// Returns the warning level as defined by the /Wn switch in the compiland command line.
        /// </summary>
        public int WarningLevel => this.compilerCommandLine.WarningLevel;

        /// <summary>
        /// Returns whether optimizations were enabled as defined by the various /O switches in the compiland command line.
        /// </summary>
        public bool OptimizationsEnabled => this.compilerCommandLine.OptimizationsEnabled;

        /// <summary>
        /// Returns whether the C runtime options is debug (/MTd /MDd) or release (/MT /MD).
        /// </summary>
        public bool UsesDebugCRuntime => this.compilerCommandLine.UsesDebugCRuntime;

        /// <summary>
        /// Returns whether the compiland command line specifies Enable String Pooling (/GF).
        /// </summary>
        public bool EliminateDuplicateStringsEnabled => this.compilerCommandLine.EliminateDuplicateStringsEnabled;

        /// <summary>
        /// Returns whether the linker had incremental linking enabled, or not.
        /// </summary>
        public bool IncrementalLinkingEnabled => this.linkerCommandLine.IncrementalLinking;

        /// <summary>
        /// Returns whether the linker has COMDAT Folding (/OPT:ICF) enabled.
        /// </summary>
        public bool ComdatFoldingEnabled => this.linkerCommandLine.ComdatFoldingEnabled;

        /// <summary>
        /// Returns whether the linker has Optimize References (/OPT:REF) enabled.
        /// </summary>
        public bool OptimizeReferencesEnabled => this.linkerCommandLine.OptimizeReferencesEnabled;

        /// <summary>
        /// Returns whether the linker requested Link Time Code Generation (/LTCG)
        /// </summary>
        public bool LinkTimeCodeGenerationEnabled => this.linkerCommandLine.LinkTimeCodeGenerationEnabled;

        /// <summary>
        /// Returns whether the compiler has Whole Program Optimziation (/GL) enabled
        /// </summary>
        public bool WholeProgramOptimizationEnabled => this.compilerCommandLine.WholeProgramOptimizationEnabled;

        /// <summary>
        /// Returns a list of integers corresponding to the set of warnings disabled via -wdnnnn switches on the command line.
        /// </summary>
        public ImmutableArray<int> ExplicitlyDisabledWarnings => this.compilerCommandLine.WarningsExplicitlyDisabled;

        /// <summary>
        /// The raw command line passed to the compiler when building this object module.
        /// </summary>
        public string RawCommandLine => this.compilerCommandLine.Raw;

        /// <summary>
        /// The raw command line passed to the linker when building this object module.
        /// </summary>
        public string RawLinkerCommandLine => this.linkerCommandLine.Raw;

        /// <summary>
        /// The name of the compiler.
        /// </summary>
        public string CompilerName { get; private set; }

        /// <summary>
        /// The version of the compiler backend.
        /// </summary>
        public Version CompilerBackEndVersion { get; private set; }

        /// <summary>
        /// The version of the compiler frontend.
        /// </summary>
        public Version CompilerFrontEndVersion { get; private set; }

        /// <summary>
        /// Determines the language of this object module.
        /// </summary>
        public Language Language { get; private set; }

        private WellKnownCompilers? wellKnownCompiler;

        public WellKnownCompilers WellKnownCompiler
        {
            get
            {
                if (!this.wellKnownCompiler.HasValue) { this.ComputeWellKnownCompilerValue(); }
                return this.wellKnownCompiler.Value;
            }
        }

        private void ComputeWellKnownCompilerValue()
        {
            this.wellKnownCompiler = WellKnownCompilers.Unknown;

            if (this.Language == Language.C)
            {
                if (this.CompilerName == CompilerNames.MicrosoftC)
                {
                    this.wellKnownCompiler = WellKnownCompilers.MicrosoftC;
                }
                else if (this.CompilerName.StartsWith(CompilerNames.ClangPrefix))
                {
                    this.wellKnownCompiler = WellKnownCompilers.Clang;
                }
            }

            if (this.Language == Language.Cxx)
            {
                if (this.CompilerName == CompilerNames.MicrosoftCxx)
                {
                    this.wellKnownCompiler = WellKnownCompilers.MicrosoftCxx;
                }
                else if (this.CompilerName.StartsWith(CompilerNames.ClangPrefix))
                {
                    this.wellKnownCompiler = WellKnownCompilers.Clang;
                }
            }

            if (this.Language == Language.LINK &&
                this.CompilerName == CompilerNames.MicrosoftLink)
            {
                this.wellKnownCompiler = WellKnownCompilers.MicrosoftLink;
            }

            if (this.Language == Language.CVTRES &&
                this.CompilerName == CompilerNames.MicrosoftCvtres)
            {
                this.wellKnownCompiler = WellKnownCompilers.MicrosoftCvtRes;
            }

            if (this.Language == Language.MASM)
            {
                if (this.CompilerName == CompilerNames.MicrosoftMasm ||
                    this.CompilerName == CompilerNames.MicrosoftARMasm)
                {
                    this.wellKnownCompiler = WellKnownCompilers.MicrosoftMasm;
                }
                else if (this.CompilerName.StartsWith(CompilerNames.ClangLLVMRustcPrefix, StringComparison.Ordinal))
                {
                    this.wellKnownCompiler = WellKnownCompilers.ClangLLVMRustc;
                }
            }
        }

        public bool HasSecurityChecks { get; private set; }

        public bool HasDebugInfo { get; private set; }

        /// <summary>
        /// Determine the state of a single switch.
        /// </summary>
        /// <param name="switchName">Switch name to check.</param>
        /// <param name="precedence">The precedence rules for this switch.</param>
        public SwitchState GetSwitchState(string switchName, OrderOfPrecedence precedence)
        {
            string[] switchNames = new string[1] { switchName };
            return CommandLineHelper.GetSwitchState(this.compilerCommandLine.Raw, switchNames, null, SwitchState.SwitchNotFound, precedence);
        }

        /// <summary>
        /// Determine if a switch is set,unset or not present on the command-line.
        /// </summary>
        /// <param name="switchNames">Array of switches that alias each other and all set the same compiler state.</param>
        /// <param name="overrideNames">Array of switches that invalidate the state of the switches in switchNames.</param>
        /// <param name="defaultState">The default state of the switch should no instance of the switch or its overrides be found.</param>
        /// <param name="precedence">The precedence rules for this set of switches.</param>
        public SwitchState GetSwitchState(string[] switchNames, string[] overrideNames, SwitchState defaultStateOfFirst, OrderOfPrecedence precedence)
        {
            return CommandLineHelper.GetSwitchState(this.compilerCommandLine.Raw, switchNames, overrideNames, defaultStateOfFirst, precedence);
        }

        /// <summary>
        /// Get the value of one of a set of compiler command-line options.
        /// </summary>
        /// <param name="optionNames">Array of command line options to search for a value.</param>
        /// <param name="precedence">The precedence ruls for this set of options.</param>
        /// <param name="optionValue">string to recieve the value of the command line option.</param>
        /// <param name="optionNamesExcluded">Array of command line options to be excluded from the result.</param>
        /// <returns>true if one of the options is found, false if none are found.</returns>
        public bool GetOptionValue(string[] optionNames, OrderOfPrecedence precedence, ref string optionValue, string[] optionNamesExcluded = null)
        {
            return CommandLineHelper.GetOptionValue(this.compilerCommandLine.Raw, optionNames, precedence, ref optionValue, optionNamesExcluded);
        }

        /// <summary>
        /// Get the dialect of the object module detail.
        /// <param name="versionNumber">the version number of the dialect.</param>
        /// </summary>
        /// <returns>dialect of the object module detail if found.</returns>
        public string GetDialect(out string versionNumber)
        {
            string[] cVersion;
            string[] cVersionExcluded = null;
            versionNumber = string.Empty;

            if (string.IsNullOrWhiteSpace(RawCommandLine) || !this.HasDebugInfo)
            {
                return string.Empty;
            }

            if (this.WellKnownCompiler == WellKnownCompilers.MicrosoftC)
            {
                cVersion = new string[] { "std:c" };
                cVersionExcluded = new string[] { "std:c++" };
            }
            else if (this.WellKnownCompiler == WellKnownCompilers.MicrosoftCxx)
            {
                cVersion = new string[] { "std:c++" };
            }
            else
            {
                return string.Empty;
            }

            this.GetOptionValue(cVersion, OrderOfPrecedence.FirstWins, ref versionNumber, cVersionExcluded);

            if (string.IsNullOrWhiteSpace(versionNumber))
            {
                if (this.WellKnownCompiler == WellKnownCompilers.MicrosoftC)
                {
                    // MSDN:
                    // The default C compiler (that is, the compiler when /std:c11 or /std:c17 isn't specified).
                    // implements ANSI C89, but includes several Microsoft extensions, some of which are part of ISO C99.
                    versionNumber = "89";
                }
                else if (this.WellKnownCompiler == WellKnownCompilers.MicrosoftCxx)
                {
                    // MSDN:
                    // The /std:c++14 option enables C++14 standard-specific features implemented by the MSVC compiler.
                    // This option is the default for code compiled as C++.
                    versionNumber = "14";
                }
            }

            return $"{this.WellKnownCompiler} {versionNumber}";
        }
    }

    /// <summary>
    /// The CV_CFL_LANG enumeration, 
    /// which specifies the code language of the application or linked module in the debug interface access SDK.
    /// https://docs.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/cv-cfl-lang
    /// </summary>
    public enum Language : uint
    {
        C = 0x00,
        Cxx = 0x01,
        FORTRAN = 0x02,
        MASM = 0x03,
        Pascal = 0x04,
        Basic = 0x05,
        COBOL = 0x06,
        LINK = 0x07,
        CVTRES = 0x08,
        CVTPGD = 0x09,
        CSharp = 0x0A,
        VisualBasic = 0x0B,
        ILASM = 0x0C,
        Java = 0x0D,
        JScript = 0x0E,
        MSIL = 0x0F,
        HLSL = 0x10,
        ObjectiveC = 0x11,
        ObjectiveCxx = 0x12,
        Swift = 0x13,
        ALIASOBJ = 0x14,
        Rust = 0x15,
        NASM = 0x4E,
        Unknown
    }

    [Flags]
    public enum WellKnownCompilers
    {
        Unknown,
        MicrosoftC = 0x1,        // 32|64-bit for x86|x64|ARM|ARM64
        MicrosoftMasm = 0x2,     // ml.exe / ml64.exe / armasm.exe / armasm64.exe
        MicrosoftCsharp = 0x4,   // csc.exe
        MicrosoftRC = 0x8,       // rc.exe
        MicrosoftCvtRes = 0x10,  // cvtres.exe
        MicrosoftCxx = 0x20,     // cl.exe
        MicrosoftLink = 0x40,    // cl.exe
        Clang = 0x80,
        ClangLLVMRustc = 0x100,  // rustc.exe
    }
}
