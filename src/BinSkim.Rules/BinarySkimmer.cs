// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Resources;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class BinarySkimmer : Skimmer<BinaryAnalyzerContext>
    {
        private static readonly Uri s_helpUri = new Uri($"https://github.com/microsoft/binskim/blob/main/docs/BinSkimRules.md");

        public override Uri HelpUri => s_helpUri;

        // TODO: author good help text.
        // https://github.com/Microsoft/binskim/issues/192
        public override MultiformatMessageString Help => this.FullDescription;

        public BinarySkimmer()
        {
            // Set Binscope friendly name for backwards compatibility, if one exists.
            string altId = BinScopeCompatibility.GetBinScopeRuleReadableName(this.Id);
            if (!string.IsNullOrEmpty(altId))
            {
                this.SetProperty<string>(BinScopeCompatibility.EquivalentBinScopeRulePropertyName, altId);
            }

            // We should not emit a default level of anything but warning. The reason is that 
            // doing so prevents any rule from overriding this value to warning (as warning
            // failure levels are elided during serialization). We need to support setting 
            // result levels to 'null' to indicate they are in a default condition.
            this.DefaultConfiguration.Level = FailureLevel.Warning;
        }

        protected override ResourceManager ResourceManager => RuleResources.ResourceManager;
    }
}
