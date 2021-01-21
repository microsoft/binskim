// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal class VulnerableDependencyDescriptor : PropertiesDictionary
    {
        public VulnerableDependencyDescriptor()
        {
        }

        public VulnerableDependencyDescriptor(PropertiesDictionary dictionary = null)
        {
            if (dictionary != null)
            {
                foreach (string key in dictionary.Keys)
                {
                    this[key] = dictionary[key];
                }
            }
        }

        private static PerLanguageOption<StringSet> s_fileHashes { get; } =
            new PerLanguageOption<StringSet>(
                nameof(VulnerableDependencyDescriptor), nameof(FileHashes), defaultValue: () => new StringSet());

        private static PerLanguageOption<string> s_id { get; } =
            new PerLanguageOption<string>(
                nameof(VulnerableDependencyDescriptor), nameof(Id), defaultValue: () => string.Empty);

        private static PerLanguageOption<string> s_dependencyName { get; } =
            new PerLanguageOption<string>(
                nameof(VulnerableDependencyDescriptor), nameof(DependencyName), defaultValue: () => string.Empty);

        private static PerLanguageOption<string> s_vulnerabilityDescription { get; } =
            new PerLanguageOption<string>(
                nameof(VulnerableDependencyDescriptor), nameof(VulnerabilityDescription), defaultValue: () => string.Empty);

        private static PerLanguageOption<string> s_resolution { get; } =
            new PerLanguageOption<string>(
                nameof(VulnerableDependencyDescriptor), nameof(Resolution), defaultValue: () => string.Empty);

        public StringSet FileHashes => this.GetProperty(s_fileHashes);

        public string Id
        {
            get => this.GetProperty(s_id);
            set => this.SetProperty(s_id, value);
        }
        public string DependencyName
        {
            get => this.GetProperty(s_dependencyName);
            set => this.SetProperty(s_dependencyName, value);
        }

        public string VulnerabilityDescription
        {
            get => this.GetProperty(s_vulnerabilityDescription);
            set => this.SetProperty(s_vulnerabilityDescription, value);
        }

        public string Resolution
        {
            get => this.GetProperty(s_resolution);
            set => this.SetProperty(s_resolution, value);
        }
    }
}
