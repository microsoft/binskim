// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal static class SarifHelpers
    {
        public static void ValidateRunLog(RunLog runLog, Action<Result> resultAction)
        {
            ValidateTool(runLog.Tool);

            foreach (Result result in runLog.Results) { resultAction(result); }
        }

        public static void ValidateTool(Tool tool)
        {
            Assert.Equal("BinSkim", tool.Name);
            // TODO version, etc
        }
    }
}
