// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class AnalysisSummary
    {
        public string ToolName { get; set; }
        public string ToolVersion { get; set; }
        public string NormalizedPath { get; set; }
        public string SymbolPath { get; set; }
        public int FileAnalyzed { get; set; }
        public int FileNotAnalyzed { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public TimeSpan TimeConsumed { get; set; }
        public string BuildDefinitionId { get; set; }
        public string BuildDefinitionName { get; set; }
        public string BuildRunId { get; set; }

        public override string ToString()
        {
            return $"{this.ToolName},{this.ToolVersion},{this.NormalizedPath},{this.SymbolPath},{this.FileAnalyzed},{this.FileNotAnalyzed},{this.StartTimeUtc},{this.EndTimeUtc},{this.TimeConsumed},{this.BuildDefinitionId},{this.BuildDefinitionName},{this.BuildRunId}";
        }
    }
}
