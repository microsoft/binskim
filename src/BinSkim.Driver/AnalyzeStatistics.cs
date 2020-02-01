// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    internal class AnalyzeStatistics : IDisposable
    {
        private readonly Stopwatch _stopwatch;

        public AnalyzeStatistics(AnalyzeOptions analyzerOptions)
        {
            if (!analyzerOptions.Statistics)
            {
                return;
            }
            this._stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            this.DumpStatisticsToConsole();
        }

        internal void DumpStatisticsToConsole()
        {
            if (this._stopwatch == null)
            {
                return;
            }
        }
    }
}
