// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class ErrorRules
    {
        public static IRuleDescriptor InvalidPE = new RuleDescriptor()
        {
            Id = "BA1001",
            Name = nameof(InvalidPE),
            FullDescription = DriverResources.InvalidPE_Description
        };
    }
}
