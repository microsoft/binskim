// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class ErrorRules
    {
        public static IRule InvalidPE = new Rule()
        {
            Id = "BA1001",
            Name = nameof(InvalidPE),
            FullDescription = DriverResources.InvalidPE_Description
        };
    }
}
