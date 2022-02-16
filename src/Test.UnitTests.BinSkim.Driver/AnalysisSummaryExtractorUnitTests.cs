﻿// Copyright (c) Microsoft. All rights reserved.
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
        private const string ToolName = "Test Tool";
        private const string ToolVersion = "1.2.0";
        private const string SerializedPath = @"F:\Application\";
        private const string BinaryPath = @"F:\Application\Binaries";
        private const string SymbolPath = @"\\symbolServer\application\";

        [Fact]
        public void ExtractAnalysisSummary_Postive()
        {
            int numArtifact = 5;
            DateTime currentTime = DateTime.UtcNow;

            SarifLog log = this.GenerateSarifLog(ToolName, ToolVersion, currentTime, numArtifact);

            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(log,
                                                                                      SerializedPath,
                                                                                      SymbolPath);

            summary.Should().NotBeNull();
            summary.ToolName.Should().Be(ToolName);
            summary.SymbolPath.Should().Be(SymbolPath);
            summary.ToolVersion.Should().Be(ToolVersion);
            summary.NormalizedPath.Should().Be(SerializedPath);
            summary.StartTimeUtc.Should().Be(currentTime.AddMinutes(-1).AddSeconds(-10));
            summary.EndTimeUtc.Should().Be(currentTime);
        }

        [Fact]
        public void ExtractAnalysisSummary_ShouldNotThrowExceptionWhenArtifactsIsNull()
        {
            int numArtifact = 5;
            DateTime currentTime = DateTime.UtcNow;

            SarifLog log = this.GenerateSarifLog(ToolName, ToolVersion, currentTime, numArtifact);
            log.Runs[0].Artifacts = null;

            var option = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[]
                {
                    $"{BinaryPath}\\*.exe",
                    $"{BinaryPath}\\*.dll",
                },
                SymbolsPath = @"\\symbolServer\application\",
            };

            Exception exception = Record.Exception(() => AnalysisSummaryExtractor.ExtractAnalysisSummary(log, SerializedPath, SymbolPath));
            exception.Should().BeNull();
        }

        [Fact]
        public void ExtractAnalysisSummary_NullSarifLog()
        {
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(null, SerializedPath, SymbolPath);
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
                (AnalysisSummaryExtractor.BuildDefinitionIdVariableName, buildDefId),
                (AnalysisSummaryExtractor.BuildDefinitionNameVariableName, buildDefName),
                (AnalysisSummaryExtractor.BuildDefinitionRunIdVariableName, buildRunId),
                (AnalysisSummaryExtractor.RepositoryIdVariableName, repositoryId),
                (AnalysisSummaryExtractor.RepositoryNameVariableName, repositoryName),
                (AnalysisSummaryExtractor.OrganizationIdVariableName, organizationId),
                (AnalysisSummaryExtractor.OrganizationNameVariableName, organizationName),
                (AnalysisSummaryExtractor.ProjectIdVariableName, projectId),
                (AnalysisSummaryExtractor.ProjectNameVariableName, projectName),
            });
            AnalysisSummaryExtractor.UpdateBuildPipelineInfo(summary);

            summary.BuildDefinitionId.Should().Be(buildDefId);
            summary.BuildDefinitionName.Should().Be(buildDefName);
            summary.BuildRunId.Should().Be(buildRunId);

            summary.OrganizationId.Should().Be(organizationId);
            summary.OrganizationName.Should().Be(organizationName);

            summary.ProjectId.Should().Be(projectId);
            summary.ProjectName.Should().Be(projectName);

            summary.RepositoryId.Should().Be(repositoryId);
            summary.RepositoryName.Should().Be(repositoryName);
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
