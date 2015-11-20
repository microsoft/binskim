// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinSkim.Sdk
{
    public enum MessageKind
    {
        Unknown = 0,

        /// <summary>
        /// An 'analyzing target' message is logged immediately before 
        /// starting analysis for a portable executable.
        /// </summary>
        AnalyzingTarget,

        /// <summary>
        /// A 'pass' message is logged in cases when the analysis target
        /// is determined to be a valid target for the associated rule and 
        /// to conforms to the desired quality condition. 'Pass' 
        /// messages should only be emitted in 'verbose' logging modes.
        /// </summary>
        Pass,

        /// <summary>
        /// A 'fail' message is logged in cases when the analysis target
        /// is determined to be a valid target for the associated rule and
        /// has been determined not to conform to a desired quality condition.
        /// 'Fail' messages should always be emitted.
        /// </summary>
        Fail,

        /// <summary>
        /// A 'pending' message is logged when the analysis target is 
        /// determined to be a valid target for the associated rule but
        /// the final pass/fail assessment has been deferred because the
        /// relevant quality condition under analysis is extremely fluid.
        /// A policy that enforces a minimal version for a compiler, for 
        /// example, may change its standard frequently. For these cases, 
        /// the tool emits a 'pending' message that contains sufficient
        /// recorded state (such as the observed version of the compiler
        /// that built the target) in order to support a follow-on pass/fail 
        /// determination. 'Pending' messages should always be emitted.
        /// </summary>
        Pending,

        /// <summary>
        /// A 'not applicable' message is logged when the analysis target
        /// is determined not to be a portable executable (in which case no
        /// binskim analysis is relevant) or not applicable for a specific
        /// rule, in which case it would be meaningless or impossible to 
        /// correct a code quality condition flagged by the associated rule. 
        /// 'Not applicable' messages should only be emitted in 'verbose'
        /// logging modes.
        /// </summary>
        NotApplicable,

        /// <summary>
        /// A 'configuration error' is logged to record conditions that 
        /// prevent analysis from making any pass/fail determinations due
        /// to incomplete or incorrect tool configuration. These issues 
        /// should be addressed by parties responsible for configuring
        /// and invoking analysis.
        /// </summary>
        ConfigurationError,

        /// <summary>
        /// An 'internal error' is logged to record conditions that 
        /// prevent analysis from making any pass/fail determinations.
        /// These may include inability to initialize a check or 
        /// unhandled exceptions that occur during analysis. These
        /// issues should be addressed by the binskim owners.
        /// </summary>
        InternalError,

        /// <summary>
        /// A 'note' is logged in order to record additional information
        /// or observed state that might be helpful in resolving an issue.
        /// Analysis 'notes' should only be emitted in 'verbose' logging
        /// modes.
        /// </summary>
        Note
    }
}
