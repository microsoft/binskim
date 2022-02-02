// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL
{
    public static class AnalysisSummaryExtractor
    {
        internal const string ProjectIdVariableName = "System_TeamProjectId";
        internal const string ProjectNameVariableName = "System_TeamProject";
        internal const string RepositoryIdVariableName = "Build_Repository_ID";
        internal const string OrganizationIdVariableName = "System_CollectionId";
        internal const string BuildDefinitionRunIdVariableName = "Build_BuildId";
        internal const string RepositoryNameVariableName = "Build_Repository_Name";
        internal const string OrganizationNameVariableName = "System_CollectionUri";
        internal const string BuildDefinitionIdVariableName = "System_DefinitionId";
        internal const string BuildDefinitionNameVariableName = "Build_DefinitionName";

        public static AnalysisSummary ExtractAnalysisSummary(SarifLog sarifLog,
                                                             string serializedFileSpecifiers,
                                                             string symbolPath)
        {
            if (sarifLog == null || sarifLog.Runs?.Any() != true)
            {
                return null;
            }

            Tool tool = sarifLog.Runs[0].Tool;
            Invocation invocation = sarifLog.Runs[0].Invocations[0];
            IList<Artifact> artifacts = sarifLog.Runs[0].Artifacts;

            TimeSpan analysisTime = invocation.EndTimeUtc - invocation.StartTimeUtc;

            var summary = new AnalysisSummary
            {
                SymbolPath = symbolPath,
                ToolName = tool.Driver.Name,
                ToolVersion = tool.Driver.Version,
                FileAnalyzed = artifacts?.Count ?? 0,
                TimeConsumed = analysisTime,
                EndTimeUtc = invocation.EndTimeUtc,
                StartTimeUtc = invocation.StartTimeUtc,
                NormalizedPath = serializedFileSpecifiers,
            };

            UpdateBuildPipelineInfo(summary);
            return summary;
        }

        public static void UpdateBuildPipelineInfo(AnalysisSummary summary)
        {
            // Build pipeline pre-defined variables can be read from environment variables.
            // https://docs.microsoft.com/en-us/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml
            if (summary != null)
            {
                summary.ProjectId = CompilerDataLogger.RetrieveEnvironmentVariable(ProjectIdVariableName);
                summary.ProjectName = CompilerDataLogger.RetrieveEnvironmentVariable(ProjectNameVariableName);

                summary.RepositoryId = CompilerDataLogger.RetrieveEnvironmentVariable(RepositoryIdVariableName);
                summary.RepositoryName = CompilerDataLogger.RetrieveEnvironmentVariable(RepositoryNameVariableName);

                summary.BuildRunId = CompilerDataLogger.RetrieveEnvironmentVariable(BuildDefinitionRunIdVariableName);
                summary.BuildDefinitionId = CompilerDataLogger.RetrieveEnvironmentVariable(BuildDefinitionIdVariableName);
                summary.BuildDefinitionName = CompilerDataLogger.RetrieveEnvironmentVariable(BuildDefinitionNameVariableName);

                summary.OrganizationName = RetrieveOrganizationName();
                summary.OrganizationId = CompilerDataLogger.RetrieveEnvironmentVariable(OrganizationIdVariableName);
            }
        }

        private static string RetrieveOrganizationName()
        {
            string organizationName = CompilerDataLogger.RetrieveEnvironmentVariable(OrganizationNameVariableName);

            organizationName = organizationName.Replace("https://dev.azure.com/",
                                                        string.Empty,
                                                        StringComparison.OrdinalIgnoreCase);

            return organizationName.TrimEnd('/');
        }

        public static IEnumerable<ExecutionException> ExtractExceptionData(SarifLog sarifLog)
        {
            IList<Sarif.Notification> notifications = sarifLog?.Runs?[0]?.Invocations?[0]?.ToolExecutionNotifications;
            if (notifications == null)
            {
                yield break;
            }

            foreach (Notification notification in notifications)
            {
                if (notification.Exception != null)
                {
                    yield return new ExecutionException(notification.Exception.Kind,
                                                        notification.Exception.Message,
                                                        notification.Exception.Stack.ToString(),
                                                        GetInnerException(notification.Exception.InnerExceptions));
                }
            }
        }

        private static ExecutionException ConvertSarifException(ExceptionData sarifException)
        {
            return new ExecutionException(sarifException.Kind,
                                          sarifException.Message,
                                          sarifException.Stack.ToString(),
                                          GetInnerException(sarifException.InnerExceptions));
        }

        private static Exception GetInnerException(IList<ExceptionData> exceptions)
        {
            if (exceptions == null || exceptions.Count == 0)
            {
                return null;
            }

            if (exceptions.Count > 1)
            {
                var aggregateException = new AggregateException();
                foreach (ExceptionData exception in exceptions)
                {
                    aggregateException.InnerExceptions.Append(ConvertSarifException(exception));
                }
                return aggregateException;
            }

            return ConvertSarifException(exceptions[0]);
        }
    }
}
