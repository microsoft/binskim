// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL
{
    public static class AnalysisSummaryExtractor
    {
        public static AnalysisSummary ExtractAnalysisSummary(SarifLog sarifLog, AnalyzeOptions options)
        {
            if (sarifLog == null || sarifLog.Runs == null || !sarifLog.Runs.Any())
            {
                return null;
            }

            Tool tool = sarifLog.Runs[0].Tool;
            Invocation invocation = sarifLog.Runs[0].Invocations[0];
            IList<Artifact> artifacts = sarifLog.Runs[0].Artifacts;

            return new AnalysisSummary
            {
                ToolName = tool.Driver.Name,
                ToolVersion = tool.Driver.Version,
                NormalizedPath = string.Join(";", options.TargetFileSpecifiers.Select(p => System.IO.Path.GetDirectoryName(p)).Distinct()),
                SymbolPath = options.SymbolsPath,
                FileAnalyzed = artifacts.Count,
                // FileNotAnalyzed = 
                StartTimeUtc = invocation.StartTimeUtc,
                EndTimeUtc = invocation.EndTimeUtc,
                TimeConsumed = invocation.EndTimeUtc - invocation.StartTimeUtc,
            };
        }

        public static IEnumerable<ExecutionException> ExtractExceptionData(SarifLog sarifLog)
        {
            IList<Sarif.Notification> notifications = sarifLog?.Runs?[0]?.Invocations?[0]?.ToolExecutionNotifications;
            if (notifications == null)
            {
                yield break;
            }


            foreach (Sarif.Notification notification in notifications)
            {
                if (notification.Exception != null)
                {
                    yield return new ExecutionException(
                                        notification.Exception.Kind,
                                        notification.Exception.Message,
                                        notification.Exception.Stack.ToString(),
                                        GetInnerException(notification.Exception.InnerExceptions));
                }
            }
        }

        private static ExecutionException ConvertSarifException(ExceptionData sarifException)
        {
            return new ExecutionException(
                sarifException.Kind,
                sarifException.Message,
                sarifException.Stack.ToString(),
                GetInnerException(sarifException.InnerExceptions));
        }

        private static Exception GetInnerException(IList<ExceptionData> exceptions)
        {
            if (exceptions == null || !exceptions.Any())
            {
                return null;
            }

            if (exceptions.Count > 1)
            {
                // its converted from AggregateException
                var aggregateException = new AggregateException();
                foreach (ExceptionData exception in exceptions)
                {
                    aggregateException.InnerExceptions.Append(ConvertSarifException(exception));
                }
                return aggregateException;
            }
            else
            {
                return ConvertSarifException(exceptions.First());
            }
        }
    }
}
