// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal static class SarifHelpers
    {
        public static void ValidateRun(Run run, Action<Result> resultAction)
        {
            ValidateTool(run.Tool);

            foreach (Result result in run.Results) { resultAction(result); }
        }

        public static void ValidateTool(Tool tool)
        {
            Assert.Equal("BinSkim", tool.Driver.Name);
            // TODO version, etc
        }
    }
}
