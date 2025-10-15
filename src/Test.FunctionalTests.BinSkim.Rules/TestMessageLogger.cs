// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class TestMessageLogger : IAnalysisLogger
    {
        public TestMessageLogger()
        {
            this.ConfigurationErrorTargets = new HashSet<string>();

            this.InitializeTargetsDictionary();
        }

        public RuntimeConditions RuntimeErrors { get; set; }

        public Dictionary<ResultKind, Dictionary<FailureLevel, HashSet<string>>> Targets { get; set; }

        public HashSet<string> PassTargets => GetAllTargetsForKind(ResultKind.Pass);
        public HashSet<string> InformationalTargets => GetAllTargetsForKind(ResultKind.Informational);
        public HashSet<string> NotApplicableTargets => GetAllTargetsForKind(ResultKind.NotApplicable);

        public HashSet<string> ErrorTargets => Targets[ResultKind.Fail][FailureLevel.Error];
        public HashSet<string> WarningTargets => Targets[ResultKind.Fail][FailureLevel.Warning];
        public HashSet<string> NoteTargets => Targets[ResultKind.Fail][FailureLevel.Note];

        public HashSet<string> ConfigurationErrorTargets { get; set; }
        public FileRegionsCache FileRegionsCache { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private HashSet<string> GetAllTargetsForKind(ResultKind kind) => Targets[kind].SelectMany(results => results.Value).ToHashSet();

        public HashSet<string> AllFoundTargets() => Targets.SelectMany(kind => kind.Value.SelectMany(level => level.Value).ToHashSet()).ToHashSet();

        public void AnalysisStarted()
        {
        }

        public void InitializeTargetsDictionary()
        {
            this.Targets = Enum.GetValues<ResultKind>().ToDictionary(
                kind => kind,
                value => Enum.GetValues<FailureLevel>().ToDictionary(
                    level => level,
                    value => new HashSet<string>()
                )
            );
        }

        public Dictionary<ResultKind, FailureLevelSet> GetAllTargetResults(string target)
            => Targets.Select(kind => KeyValuePair.Create(kind.Key, new FailureLevelSet(kind.Value.Where(level => level.Value.Contains(target)).Select(level => level.Key))))
                    .Where(pair => pair.Value.Any())
                    .ToDictionary();

        public (IEnumerable<(ResultKind, FailureLevel)> Missing, IEnumerable<(ResultKind, FailureLevel)> Additional) ValidateTarget(string target, params (ResultKind, FailureLevel)[] expectedResults)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            (IEnumerable<(ResultKind, FailureLevel)> Missing, IEnumerable<(ResultKind, FailureLevel)> Additional) result = (Array.Empty<(ResultKind, FailureLevel)>(), Array.Empty<(ResultKind, FailureLevel)>());

            Dictionary<ResultKind, FailureLevelSet> targetResults = GetAllTargetResults(target);

            foreach ((ResultKind expectedKind, FailureLevel expectedLevel) in expectedResults)
            {
                if (!targetResults
                    .Where(kind => kind.Key == expectedKind)
                    .Select(kind => kind.Value)
                    .Where(level => level.Contains(expectedLevel))
                    .Any())
                {
                    result.Missing.Append((expectedKind, expectedLevel));
                }

                // Remove expected results from the list so we can check for unexpected results later.
                targetResults[expectedKind].Remove(expectedLevel);
            }

            result.Additional = targetResults.SelectMany(kind => kind.Value.Select(level => (kind.Key, level)));

            return result;
        }

        public void AnalysisStopped(RuntimeConditions runtimeConditions)
        {
            this.RuntimeErrors = runtimeConditions;
        }

        public void AnalyzingTarget(IAnalysisContext context)
        {
        }

        public void Log(ReportingDescriptor rule, Result result, int? extensionIndex = null)
        {
            this.Targets[result.Kind][result.Level].Add(result.Locations.First().PhysicalLocation.ArtifactLocation.Uri.LocalPath);
        }

        public void LogToolNotification(Sarif.Notification notification, ReportingDescriptor associatedRule)
        {
        }

        public void LogConfigurationNotification(Sarif.Notification notification)
        {
            this.ConfigurationErrorTargets.Add(notification.Locations[0].PhysicalLocation.ArtifactLocation.Uri.LocalPath);
        }

        public void TargetAnalyzed(IAnalysisContext context)
        {
            throw new NotImplementedException();
        }
    }
}
