// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Collections.Generic;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// A logger that records rule summary telemetry by aggregating result kinds for each rule.
    /// </summary>
    internal sealed class RuleTelemetryLogger : IAnalysisLogger
    {
        // Application Insights event names
        internal const string AnalysisRequestName = "Analysis";
        internal const string RuleSummaryEventName = "RuleSummary";

        // Application Insights property names
        internal const string RuleIdPropertyName = "RuleId";

        // Application Insights metric names
        internal const string PassCountMetricName = "Pass";
        internal const string FailCountMetricName = "Fail";
        internal const string OpenCountMetricName = "Open";
        internal const string RuleCountMetricName = "RuleCount";
        internal const string ReviewCountMetricName = "Review";
        internal const string NotApplicableCountMetricName = "NA";
        internal const string InformationalCountMetricName = "Info";

        /// <summary>
        /// The Application Insights telemetry client.
        /// </summary>
        private readonly TelemetryClient telemetryClient;

        /// <summary>
        /// Dictionary of aggregated counts of result kinds keyed on the rule ID.
        /// </summary>
        private readonly Dictionary<string, ResultKindCounts> metricsMap = new Dictionary<string, ResultKindCounts>(StringComparer.Ordinal);

        /// <summary>
        /// Tracks the analysis operation. Will be non-null if analysis is in progress.
        /// </summary>
        private IOperationHolder<RequestTelemetry>? analysisOperationHolder;

        public FileRegionsCache FileRegionsCache
        {
            get; set;
        }

        /// <summary>
        /// Construct a new <see cref="RuleTelemetryLogger"/>.
        /// </summary>
        /// <param name="telemetryClient">The Application Insights telemetry client.</param>
        public RuleTelemetryLogger(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            this.FileRegionsCache = new FileRegionsCache();
        }

        public void AnalysisStarted()
        {
            this.analysisOperationHolder ??= this.telemetryClient.StartOperation<RequestTelemetry>(AnalysisRequestName);
        }

        public void AnalysisStopped(RuntimeConditions runtimeConditions)
        {
            foreach (KeyValuePair<string, ResultKindCounts> kv in this.metricsMap)
            {
                var eventTelemetry = new EventTelemetry(RuleSummaryEventName);
                eventTelemetry.Properties[RuleIdPropertyName] = kv.Key;

                // To reduce telemetry volume, record only non-zero values
                ResultKindCounts counts = kv.Value;
                IDictionary<string, double> metrics = eventTelemetry.Metrics;
                AddMetricIfNonZero(metrics, counts.NotApplicableCount, NotApplicableCountMetricName);
                AddMetricIfNonZero(metrics, counts.PassCount, PassCountMetricName);
                AddMetricIfNonZero(metrics, counts.FailCount, FailCountMetricName);
                AddMetricIfNonZero(metrics, counts.ReviewCount, ReviewCountMetricName);
                AddMetricIfNonZero(metrics, counts.OpenCount, OpenCountMetricName);
                AddMetricIfNonZero(metrics, counts.InformationalCount, InformationalCountMetricName);

                this.telemetryClient.TrackEvent(eventTelemetry);
            }

            if (this.analysisOperationHolder != null)
            {
                RequestTelemetry request = this.analysisOperationHolder.Telemetry;
                request.ResponseCode = runtimeConditions.ToString();
                request.Success = runtimeConditions == RuntimeConditions.None;
                request.Metrics[RuleCountMetricName] = metricsMap.Count;
                this.analysisOperationHolder.Dispose();
                this.analysisOperationHolder = null;
            }

            this.metricsMap.Clear();
        }

        public void AnalyzingTarget(IAnalysisContext context)
        {
        }

        public void Log(ReportingDescriptor rule, Result result, int? extensionIndex = null)
        {
            if (!this.metricsMap.TryGetValue(rule.Id, out ResultKindCounts? counts))
            {
                this.metricsMap[rule.Id] = counts = new ResultKindCounts();
            }

            switch (result.Kind)
            {
                case ResultKind.NotApplicable:
                    counts.NotApplicableCount++;
                    break;

                case ResultKind.Pass:
                    counts.PassCount++;
                    break;

                case ResultKind.Fail:
                    counts.FailCount++;
                    break;

                case ResultKind.Review:
                    counts.ReviewCount++;
                    break;

                case ResultKind.Open:
                    counts.OpenCount++;
                    break;

                case ResultKind.Informational:
                    counts.InformationalCount++;
                    break;

                default:
                    break;
            }
        }

        public void LogConfigurationNotification(Sarif.Notification notification)
        {
        }

        public void LogToolNotification(Sarif.Notification notification, ReportingDescriptor? associatedRule)
        {
        }

        private static void AddMetricIfNonZero(IDictionary<string, double> metrics, int count, string name)
        {
            if (count != 0)
            {
                metrics[name] = count;
            }
        }

        public void TargetAnalyzed(IAnalysisContext context)
        {
        }

        /// <summary>
        /// Counts of result kinds recorded for each rule.
        /// </summary>
        private sealed class ResultKindCounts
        {
            /// <summary>
            /// Count of binaries that had <see cref="ResultKind.NotApplicable"/> result.
            /// </summary>
            public int NotApplicableCount;

            /// <summary>
            /// Count of binaries that had a <see cref="ResultKind.Pass"/> result.
            /// </summary>
            public int PassCount;

            /// <summary>
            /// Count of binaries that had a <see cref="ResultKind.Fail"/> result.
            /// </summary>
            public int FailCount;

            /// <summary>
            /// Count of binaries that had a <see cref="ResultKind.Review"/> result.
            /// </summary>
            public int ReviewCount;

            /// <summary>
            /// Count of binaries that had a <see cref="ResultKind.Open"/> result.
            /// </summary>
            public int OpenCount;

            /// <summary>
            /// Count of binaries that had a <see cref="ResultKind.Informational"/> result.
            /// </summary>
            public int InformationalCount;
        }
    }
}
