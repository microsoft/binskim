// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public interface IBinarySkimmer : IRuleContext
    {
        /// <summary>
        /// Initialize method for skimmer instance. This method will only 
        /// only be called a single time per skimmer instantiation.
        /// </summary>
        /// <param name="context"></param>
        void Initialize(BinaryAnalyzerContext context);

        /// <summary>
        /// Determine whether a target is a valid target for analysis. 
        /// May be called from multiple threads.
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns>An analysis applicability value that indicates whether a check is
        /// applicable to a specified target, is not applicable to a specified target, 
        /// or is not applicable to any target due to the absence of a configured 
        /// policy. In cases where the analysis is determined not to be applicable, 
        /// the 'reasonIfNotApplicable' property should be set to a string that 
        /// describes the observed state or condition that prevents analysis.
        /// </returns>
        AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonIfNotApplicable);

        /// <summary>
        /// Analyze specified binary target and use context-resident loggers
        /// to record the results of the analysis. May be called from multiple threads.
        /// </summary>
        /// <param name="context"></param>
        void Analyze(BinaryAnalyzerContext context);
    }
}
