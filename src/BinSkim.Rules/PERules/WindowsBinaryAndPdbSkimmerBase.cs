// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // Windows specific binary and program database-reading skimmers.
    public abstract class WindowsBinaryAndPdbSkimmerBase : WindowsBinarySkimmerBase
    {
        /// <summary>
        /// Gets a property indicating whether the rule should require that PDBs
        /// can be located for managed assemblies. Some checks that inspect both
        /// managed and native code require PDBs for the native case but not
        /// for managed.
        /// </summary>
        public virtual bool EnforcePdbLoadForManagedAssemblies => true;

        public virtual bool LogPdbLoadException => true;

        public static readonly ConcurrentDictionary<string, bool> s_PdbExceptions = new ConcurrentDictionary<string, bool>();

        public sealed override void Analyze(BinaryAnalyzerContext context)
        {
            // Uses PDB Parsing.
            BinaryParsers.PlatformSpecificHelpers.ThrowIfNotOnWindows();
            PEBinary target = context.PEBinary();

            if (target.Pdb != null && !string.IsNullOrEmpty(target.PdbLoadTrace?.ToString()))
            {
                LogPdbLoadTrace(
                    context,
                    pdbLoadSucceeded: true,
                    target.PdbLoadTrace.ToString());

                // Set the trace to null so that we only emit it once.
                target.PdbLoadTrace = null;
            }

            if (LogPdbLoadException)
            {
                if (target.Pdb == null &&
                    (!target.PE.IsManaged ||
                      target.PE.IsMixedMode ||
                      EnforcePdbLoadForManagedAssemblies))
                {
                    LogExceptionLoadingPdb(context, target.PdbParseException, target.PdbLoadTrace?.ToString());
                    return;
                }
            }

            this.AnalyzePortableExecutableAndPdb(context);
        }

        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            AnalysisApplicability result = base.CanAnalyze(context, out reasonForNotAnalyzing);
            if (result != AnalysisApplicability.ApplicableToSpecifiedTarget) { return result; }

            PEBinary peBinary = context.PEBinary();
            PE portableExecutable = peBinary.PE;

            if (portableExecutable == null)
            {
                Debug.Assert(peBinary.Pdb != null);
                reasonForNotAnalyzing = null;
                return AnalysisApplicability.ApplicableToSpecifiedTarget;
            }

            result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsWixBinary;
            if (!context.IncludeWixBinaries && portableExecutable.IsWixBinary) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILLibraryAssembly;
            if (portableExecutable.IsILLibrary) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsDotNetCoreBootstrapExe;
            if (portableExecutable.IsDotNetCoreBootstrapExe) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public abstract void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context);

        public static void LogPdbLoadTrace(
            IAnalysisContext context,
            bool pdbLoadSucceeded,
            string pdbLoadTrace)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string formatString = pdbLoadSucceeded
                // The PDB for '{0}' was found and loaded. Probing details:{1}
                ? RuleResources.PdbLoadSucceeded
                // Could not locate the PDB for '{0'}. Probing details:{1}
                : RuleResources.PdbLoadFailed;

            FailureLevel failureLevel = pdbLoadSucceeded
                ? FailureLevel.Note
                : FailureLevel.Warning;

            context.Logger.LogConfigurationNotification(
                Errors.CreateNotification(
                    context.CurrentTarget.Uri,
                    "TRC001.PdbLoad",
                    context.Rule.Id,
                    failureLevel,
                    exception: null,
                    persistExceptionStack: false,
                    formatString,
                    context.CurrentTarget.Uri.GetFileName(),
                    pdbLoadTrace));
        }

        public static void LogExceptionLoadingPdb(IAnalysisContext context, PdbException pdbException, string pdbLoadTrace)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string path = context.CurrentTarget.Uri.OriginalString;
            string key = $"{path}@{pdbException.ExceptionDisplayMessage}";
            if (s_PdbExceptions.ContainsKey(key))
            {
                return;
            }

            // '{0}' was not evaluated because its PDB could not be loaded ({1}).
            context.Logger.LogConfigurationNotification(
                Errors.CreateNotification(
                    context.CurrentTarget.Uri,
                    "ERR997.ExceptionLoadingPdb",
                    string.Empty,
                    FailureLevel.Error,
                    pdbException,
                    persistExceptionStack: false,
                    RuleResources.ERR997_ExceptionLoadingPdbGeneric,
                    context.CurrentTarget.Uri.GetFileName(),
                    pdbException.ExceptionDisplayMessage));

            s_PdbExceptions.TryAdd(key, true);

            // We should only log if doNotBreak is false
            if (context is BinaryAnalyzerContext binaryAnalyzerContext && !binaryAnalyzerContext.IgnorePdbLoadError)
            {
                context.RuntimeErrors |= RuntimeConditions.ExceptionLoadingPdb;
            }

            if (!string.IsNullOrEmpty(pdbLoadTrace))
            {
                LogPdbLoadTrace(
                    context,
                    pdbLoadSucceeded: false,
                    pdbLoadTrace);
            }

            // Clear the trace data to ensure we never emit it more than once in output.
            pdbException.LoadTrace = null;
        }
    }
}
