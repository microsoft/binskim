// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    /// <summary>Constant values and helper code used by buffer security ("GS") checks.</summary>
    internal static class StackProtectionUtilities
    {
        /// <summary>Name of the gs check function.</summary>
        public static readonly string GSCheckFunctionName = "__security_check_cookie";

        internal static AnalysisApplicability CommonCanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyManagedAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsXBoxBinary;
            if (portableExecutable.IsXBox) { return result; }

            // .NET native compiled binaries are not fully /GS enabled. This is 
            // considered reasonable, as the binaries themselves consist strictly
            // of cross-compiled MSIL. The supporting native libraries for these
            // applications exists in a separate (/GS enabled) native dll. 
            reasonForNotAnalyzing = MetadataConditions.ImageIsDotNetNativeBinary;
            if (portableExecutable.IsDotNetNative) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsBootBinary;
            if (portableExecutable.IsBoot) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        /// <summary>List of names of the gs initialization functions.</summary>
        public static readonly ImmutableArray<string> GSInitializationFunctionNames = ImmutableArray.Create(
            "__security_init_cookie",
            "_GsDriverEntry",
            "GsDriverEntry",
            "_GsDrvEnableDriver",
            "GsDrvEnableDriver"
            );

        /// <summary>List of names of functions which are allowed to be __declspec(safebuffers).</summary>
        public static readonly ImmutableArray<string> AllowedDeclspecSafebuffersFunctionNames = GSInitializationFunctionNames.AddRange(new[] {
            "_TlgWrite" // Disabled as per the Event Tracing for Windows team, see twcsec-tfs01 bug #18731
        });

        /// <summary>The default stack cookie.</summary>
        public const ulong DefaultCookieX64 = 0x00002B992DDFA232;

        public static ImmutableHashSet<uint> DefaultCookiesX86 = new HashSet<uint>(new uint[] { 0xBB40E64E, 0x0000BB40 }).ToImmutableHashSet();
    }
}
