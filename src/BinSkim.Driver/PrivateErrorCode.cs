// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinSkim
{
    // This enum is used to identify specific runtime conditions 
    // encountered during execution. This mechanism is used by
    // unit tests to ensure that failure conditions travel expected
    // code paths. These conditions are a combination of fatal
    // and non-fatal circumstances
    [Flags]
    internal enum RuntimeConditions
    {
        NoErrors = 0,

        // Not used today but perhaps soon...
        //CouldNotLoadCustomLoggerAssembly,
        //CouldNotLoadCustomLoggerType,
        //UnrecognizedDefaultLoggerExtension,
        //MalformedCustomLoggersArgument,
        //LoggerFailedInitialization,
        //LoggerRaisedExceptionOnInitialization,
        //LoggerRaisedExceptionOnWrite,
        //LoggerRaisedExceptionOnClose,

        // Fatal conditions
        ExceptionInstantiatingSkimmers = 0x01,
        ExceptionInSkimmerInitialize = 0x02,
        ExceptionRaisedInSkimmerCanAnalyze = 0x04,
        ExceptionInSkimmerAnalyze = 0x08,
        ExceptionCreatingLogfile = 0x10,
        ExceptionInEngine = 0x20,
        ExceptionLoadingTargetFile = 0x40,
        ExceptionLoadingRoslynAnalyzer = 0x80,
        Fatal = (Int32.MaxValue ^ NonFatal),

        // Non-fatal conditions
        OneOrMoreTargetsNotPortableExecutables = 0x80,

        NonFatal = OneOrMoreTargetsNotPortableExecutables,
    }
}
