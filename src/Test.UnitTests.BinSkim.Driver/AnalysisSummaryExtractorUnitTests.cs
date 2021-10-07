// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class AnalysisSummaryExtractorUnitTests
    {
        [Fact]
        public void ExtractAnalysisSummary_Postive()
        {
            const string toolName = "Test Tool";
            const string toolVersion = "1.2.0";
            const string symbolPath = @"\\symbolServer\application\";
            const string binaryPath = @"F:\Application\Binaries";
            int numArtifact = 5;
            DateTime currentTime = DateTime.UtcNow;

            SarifLog log = this.GenerateSarifLog(toolName, toolVersion, currentTime, numArtifact);

            var option = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[]
                {
                    $"{binaryPath}\\*.exe",
                    $"{binaryPath}\\*.dll",
                },
                SymbolsPath = @"\\symbolServer\application\",
            };

            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(log, option);

            summary.Should().NotBeNull();
            summary.ToolName.Should().BeEquivalentTo(toolName);
            summary.ToolVersion.Should().BeEquivalentTo(toolVersion);
            summary.NormalizedPath.Should().BeEquivalentTo(binaryPath);
            summary.SymbolPath.Should().BeEquivalentTo(symbolPath);
            summary.StartTimeUtc.Should().Equals(currentTime.AddMinutes(-1).AddSeconds(-10));
            summary.EndTimeUtc.Should().Equals(currentTime);
        }

        [Fact]
        public void ExtractAnalysisSummary_NullSarifLog()
        {
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(null, new AnalyzeOptions());
            summary.Should().BeNull();
        }

        [Fact]
        public void ExtractAnalysisSummary_WithBuildPipelineInfo()
        {
            const string buildDefId = "2097";
            const string buildDefName = "AdoScannerTest-ASP.NET-CI";
            const string buildRunId = "11032";
            const string repositoryId = "1";
            const string repositoryName = "repo-name";
            const string projectId = "2";
            const string projectName = "project-name";
            const string organizationId = "3";
            const string organizationName = "organization-name";

            var summary = new AnalysisSummary();

            PrepareEnvironmentVariables(new List<(string, string)>
            {
                (AnalysisSummaryExtractor.BuildDefinitionIdVar, buildDefId),
                (AnalysisSummaryExtractor.BuildDefinitionNameVar, buildDefName),
                (AnalysisSummaryExtractor.BuildDefinitionRunIdVar, buildRunId),
                (AnalysisSummaryExtractor.RepositoryIdVar, repositoryId),
                (AnalysisSummaryExtractor.RepositoryNameVar, repositoryName),
                (AnalysisSummaryExtractor.OrganizationIdVar, organizationId),
                (AnalysisSummaryExtractor.OrganizationNameVar, organizationName),
                (AnalysisSummaryExtractor.ProjectIdVar, projectId),
                (AnalysisSummaryExtractor.ProjectNameVar, projectName),
            });
            AnalysisSummaryExtractor.UpdateBuildPipelineInfo(summary);

            summary.BuildDefinitionId.Should().BeEquivalentTo(buildDefId);
            summary.BuildDefinitionName.Should().BeEquivalentTo(buildDefName);
            summary.BuildRunId.Should().BeEquivalentTo(buildRunId);

            summary.OrganizationId.Should().BeEquivalentTo(buildRunId);
            summary.OrganizationName.Should().BeEquivalentTo(buildRunId);

            summary.ProjectId.Should().BeEquivalentTo(buildRunId);
            summary.ProjectName.Should().BeEquivalentTo(buildRunId);

            summary.RepositoryId.Should().BeEquivalentTo(buildRunId);
            summary.RepositoryName.Should().BeEquivalentTo(buildRunId);
        }

        [Fact]
        public void ExtractAnalysisSummary_WithoutBuildPipelineInfo()
        {
            var summary = new AnalysisSummary();
            AnalysisSummaryExtractor.UpdateBuildPipelineInfo(summary);

            summary.BuildDefinitionId.Should().BeNullOrEmpty();
            summary.BuildDefinitionName.Should().BeNullOrEmpty();
            summary.BuildRunId.Should().BeNullOrEmpty();

            summary.OrganizationId.Should().BeNullOrEmpty();
            summary.OrganizationName.Should().BeNullOrEmpty();

            summary.ProjectId.Should().BeNullOrEmpty();
            summary.ProjectName.Should().BeNullOrEmpty();

            summary.RepositoryId.Should().BeNullOrEmpty();
            summary.RepositoryName.Should().BeNullOrEmpty();
        }

        private SarifLog GenerateSarifLog(string toolName, string toolVersion, DateTime execTime, int numArtifact)
        {
            return new SarifLog
            {
                Runs = new[]
                {
                    new Run
                    {
                        Tool = new Tool
                        {
                            Driver = new ToolComponent
                            {
                                Name = toolName,
                                Version = toolVersion,
                            },
                        },
                        Invocations = new List<Invocation>
                        {
                            new Invocation
                            {
                                StartTimeUtc = execTime.AddMinutes(-1).AddSeconds(-10),
                                EndTimeUtc = execTime,
                            },
                        },
                        Artifacts = Enumerable.Range(1, numArtifact).Select(
                            n => new Artifact
                            {
                                Location = new ArtifactLocation { Uri = new Uri($"src/TestFile{n}.exe", UriKind.Relative) },
                            }).ToList(),
                    },
                },
            };
        }

        private void PrepareEnvironmentVariables(List<(string, string)> valuePairs)
        {
            foreach ((string, string) pair in valuePairs)
            {
                Environment.SetEnvironmentVariable(pair.Item1, pair.Item2);
            }
        }
    }
}
