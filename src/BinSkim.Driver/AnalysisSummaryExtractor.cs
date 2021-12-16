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
        internal const string BuildDefinitionIdVar = "System_DefinitionId";
        internal const string BuildDefinitionNameVar = "Build_DefinitionName";
        internal const string BuildDefinitionRunIdVar = "Build_BuildId";
        internal const string RepositoryIdVar = "Build_Repository_ID";
        internal const string RepositoryNameVar = "Build_Repository_Name";
        internal const string ProjectIdVar = "System_TeamProjectId";
        internal const string ProjectNameVar = "System_TeamProject";
        internal const string OrganizationIdVar = "System_CollectionId";
        internal const string OrganizationNameVar = "System_CollectionUri";

        public static AnalysisSummary ExtractAnalysisSummary(SarifLog sarifLog, AnalyzeOptions options)
        {
            if (sarifLog == null || sarifLog.Runs?.Any() != true)
            {
                return null;
            }

            Tool tool = sarifLog.Runs[0].Tool;
            Invocation invocation = sarifLog.Runs[0].Invocations[0];
            IList<Artifact> artifacts = sarifLog.Runs[0].Artifacts;

            var summary = new AnalysisSummary
            {
                ToolName = tool.Driver.Name,
                ToolVersion = tool.Driver.Version,
                NormalizedPath = string.Join(";", options.TargetFileSpecifiers.Select(p => System.IO.Path.GetDirectoryName(p)).Distinct()),
                SymbolPath = options.SymbolsPath,
                FileAnalyzed = artifacts?.Count ?? 0,
                // FileNotAnalyzed =
                StartTimeUtc = invocation.StartTimeUtc,
                EndTimeUtc = invocation.EndTimeUtc,
                TimeConsumed = invocation.EndTimeUtc - invocation.StartTimeUtc,
            };

            UpdateBuildPipelineInfo(summary);
            return summary;
        }

        public static void UpdateBuildPipelineInfo(AnalysisSummary summary)
        {
            // build pipeline pre-defined variables can be read from environment variables
            // https://docs.microsoft.com/en-us/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml
            if (summary != null)
            {
                summary.BuildDefinitionId = ExtractValueFromEnvironmentVariable(BuildDefinitionIdVar);
                summary.BuildDefinitionName = ExtractValueFromEnvironmentVariable(BuildDefinitionNameVar);
                summary.BuildRunId = ExtractValueFromEnvironmentVariable(BuildDefinitionRunIdVar);

                summary.RepositoryId = ExtractValueFromEnvironmentVariable(RepositoryIdVar);
                summary.RepositoryName = ExtractValueFromEnvironmentVariable(RepositoryNameVar);

                summary.ProjectId = ExtractValueFromEnvironmentVariable(ProjectIdVar);
                summary.ProjectName = ExtractValueFromEnvironmentVariable(ProjectNameVar);

                summary.OrganizationId = ExtractValueFromEnvironmentVariable(OrganizationIdVar);
                summary.OrganizationName = ExtractValueFromEnvironmentVariable(OrganizationNameVar);
                summary.OrganizationName = summary.OrganizationName.Replace("https://dev.azure.com/", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd('/');
            }
        }

        public static string ExtractValueFromEnvironmentVariable(string environmentVariable)
        {
            string value = string.Empty;

            try
            {
                value = Environment.GetEnvironmentVariable(environmentVariable);
                if (string.IsNullOrEmpty(value))
                {
                    value = Environment.GetEnvironmentVariable(environmentVariable, EnvironmentVariableTarget.User);
                    if (string.IsNullOrEmpty(value))
                    {
                        value = Environment.GetEnvironmentVariable(environmentVariable, EnvironmentVariableTarget.Machine);
                    }
                }
            }
            catch (SecurityException)
            {
                // User does not have access to retrieve information from environment variables.
            }

            return value ?? string.Empty;
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
            if (exceptions?.Any() != true)
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
