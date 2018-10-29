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
        private CompilerCommandLine _compilerCommandLine;
        
        /// <summary>Initializes a new instance of the <see cref="ObjectModuleDetails"/> class.</summary>
        /// <param name="compilerFrontEndVersion">The front end version of the compiler producing the object module.</param>
        /// <param name="backEndVersion">The back end version of the compiler producing the object module.</param>
        /// <param name="commandLine">The command line passed to the compiler used to build the object module.</param>
        /// <param name="language">The language of the object module.</parm>
        /// <param name="compilerName">The compiler used to create the object module.</param>
        /// <param name="hasSecurityChecks">A boolean that indicates whether the PE contains security checks.</param>
        /// <param name="hasDebugInfo">A boolean that indicates whether the PE comes with a corresponding PDB.</param>
        /// 
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
            Name = name;
            Library = library;
            CompilerFrontEndVersion = compilerFrontEndVersion ?? new Version();
            CompilerBackEndVersion = backEndVersion ?? new Version();
            _compilerCommandLine = new CompilerCommandLine(commandLine ?? String.Empty);
            Language = language;
            CompilerName = compilerName;
            HasSecurityChecks = hasSecurityChecks;
            HasDebugInfo = hasDebugInfo;
        }

        public string Name { get; private set; }

        public string Library { get; private set; }

        /// <summary>
        /// Returns the warning level as defined by the /Wn switch in the compiland command line
        /// </summary>
        public int WarningLevel { get; private set;  }

        /// <summary>
        /// Returns a list of integers corresponding to the set of warnings disabled via -wdnnnn switches on the command line
        /// </summary>
        public ImmutableArray<int> ExplicitlyDisabledWarnings
        {
            get
            {
                return _compilerCommandLine.WarningsExplicitlyDisabled;
            }
        }

        /// <summary>
        /// The raw command line passed to the compiler when building this object module.
        /// </summary>
        public string RawCommandLine { get; private set; }

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
                this.CompilerName == "Microsoft (R) Optimizing Compiler")
            {
                _wellKnownCompiler = WellKnownCompilers.MicrosoftNativeCompiler;
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
            return _compilerCommandLine.GetSwitchState(switchNames, null, SwitchState.SwitchNotFound, precedence);
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
            return _compilerCommandLine.GetSwitchState(switchNames, overrideNames, defaultStateOfFirst, precedence);
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
