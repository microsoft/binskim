// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.IL.Sdk;
using System;
using System.Resources;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class BinarySkimmerBase : SkimmerBase<BinaryAnalyzerContext>, IBinarySkimmer
    {
        private static Uri s_helpUri = new Uri("https://github.com/microsoft/binskim");

        public override Uri HelpUri { get { return s_helpUri; } }

        public BinarySkimmerBase()
        {
            // Set Binscope friendly name for backwards compatibility, if one exists.
            string altId = BinscopeCompatibility.GetBinscopeFriendlyName(this.Id);
            if (!String.IsNullOrEmpty(altId))
            {
                this.SetProperty<string>(BinscopeCompatibility.EquivalentBinscopeRulePropertyName, altId);
            }
        }

        protected override ResourceManager ResourceManager
        {
            get
            {
                return RuleResources.ResourceManager;
            }
        }
    }
}
