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
            this.compilerCommandLine = new CompilerCommandLine(commandLine ?? string.Empty);
            this.Language = language;
            this.CompilerName = compilerName;
            this.HasSecurityChecks = hasSecurityChecks;
            this.HasDebugInfo = hasDebugInfo;
        }

        public string Name { get; private set; }

        public string Library { get; private set; }

        /// <summary>
        /// Returns the warning level as defined by the /Wn switch in the compiland command line
        /// </summary>
        public int WarningLevel => this.compilerCommandLine.WarningLevel;

        /// <summary>
        /// Returns a list of integers corresponding to the set of warnings disabled via -wdnnnn switches on the command line
        /// </summary>
        public ImmutableArray<int> ExplicitlyDisabledWarnings => this.compilerCommandLine.WarningsExplicitlyDisabled;

        /// <summary>
        /// The raw command line passed to the compiler when building this object module.
        /// </summary>
        public string RawCommandLine => this.compilerCommandLine.Raw;

        /// <summary>
        /// The name of the compiler
        /// </summary>
        public string CompilerName { get; private set; }

        /// <summary>
        /// The version of the compiler backend
        /// </summary>
        public Version CompilerBackEndVersion { get; private set; }

        /// <summary>
        /// The version of the compiler frontend
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

            if (this.Language == Language.C &&
                this.CompilerName == CompilerNames.MicrosoftC)
            {
                this.wellKnownCompiler = WellKnownCompilers.MicrosoftC;
            }

            if (this.Language == Language.Cxx &&
                this.CompilerName == CompilerNames.MicrosoftCxx)
            {
                this.wellKnownCompiler = WellKnownCompilers.MicrosoftCxx;
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

            if (this.Language == Language.MASM &&
                (this.CompilerName == CompilerNames.MicrosoftMasm ||
                 this.CompilerName == CompilerNames.MicrosoftARMasm))
            {
                this.wellKnownCompiler = WellKnownCompilers.MicrosoftMasm;
            }
        }

        public bool HasSecurityChecks { get; private set; }

        public bool HasDebugInfo { get; private set; }

        /// <summary>
        /// Determine the state of a single switch
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
        /// Get the value of one of a set of compiler command-line options
        /// </summary>
        /// <param name="optionNames">Array of command line options to search for a value</param>
        /// <param name="precedence">The precedence ruls for this set of options</param>
        /// <param name="optionValue">string to recieve the value of the command line option</param>
        /// <param name="optionNamesExcluded">Array of command line options to be excluded from the result</param>
        /// <returns>true if one of the options is found, false if none are found</returns>
        public bool GetOptionValue(string[] optionNames, OrderOfPrecedence precedence, ref string optionValue, string[] optionNamesExcluded = null)
        {
            return CommandLineHelper.GetOptionValue(this.compilerCommandLine.Raw, optionNames, precedence, ref optionValue, optionNamesExcluded);
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
        MicrosoftC = 0x1,        // 32|64-bit for x86|x64|ARM|ARM64
        MicrosoftMasm = 0x2,     // ml.exe / ml64.exe / armasm.exe / armasm64.exe
        MicrosoftCsharp = 0x4,   // csc.exe
        MicrosoftRC = 0x8,       // rc.exe
        MicrosoftCvtRes = 0x10,  // cvtres.exe
        MicrosoftCxx = 0x20,     // cl.exe
        MicrosoftLink = 0x40,    // cl.exe
    }
}
