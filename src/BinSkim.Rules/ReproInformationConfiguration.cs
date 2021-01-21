// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public static class ReproInformationConfiguration
    {
        internal static PerLanguageOption<StringToVersionMap> BA2024_AllowedLibraries { get; } =
            new PerLanguageOption<StringToVersionMap>(
                null, nameof(BA2024_AllowedLibraries), defaultValue: () => null);
    }
}
