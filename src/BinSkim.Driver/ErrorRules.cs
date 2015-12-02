// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal class ErrorRuleContext : IRuleContext
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    internal static class ErrorRules
    {
        public static IRuleContext UnhandledRuleException = new ErrorRuleContext()
        { Id = "BA0998", Name = nameof(UnhandledRuleException) };

        public static IRuleContext UnhandledEngineException = new ErrorRuleContext()
        { Id = "BA0999", Name = nameof(UnhandledEngineException) };

        public static IRuleContext InvalidPE = new ErrorRuleContext()
        { Id = "BA1001", Name = nameof(InvalidPE) };

        public static IRuleContext InvalidConfiguration = new ErrorRuleContext()
        { Id = "BA1002", Name = nameof(InvalidConfiguration) };
    }
}
