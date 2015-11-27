// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat.DataContracts;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    internal static class SarifHelpers
    {
        public static void ValidateRunLog(RunLog runLog, Action<Issue> issueAction)
        {
            ValidateToolInfo(runLog.ToolInfo);

            foreach (Issue issue in runLog.Issues) { issueAction(issue); }
        }

        public static void ValidateToolInfo(ToolInfo toolInfo)
        {
            Assert.Equal("BinSkim", toolInfo.ToolName);
            // TODO version, etc
        }
    }
}
