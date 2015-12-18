// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Reflection;

using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.IL.Rules;

namespace Microsoft.CodeAnalysis.IL
{
    internal class ExportRulesMetadataCommand : ExportRulesMetadataCommandBase
    {
        public override IEnumerable<Assembly> DefaultPlugInAssemblies
        {
            get
            {
                return new Assembly[] {
                    typeof(MarkImageAsNXCompatible).Assembly
                };
            }
        }

        public override string Prerelease { get { return VersionConstants.Prerelease; } }
    }
}
