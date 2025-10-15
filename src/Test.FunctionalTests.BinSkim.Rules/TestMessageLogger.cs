// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class TestMessageLogger : IAnalysisLogger
    {
        public TestMessageLogger()
        {
            this.ErrorTargets = new HashSet<string>();
            this.WarningTargets = new HashSet<string>();
            this.NoteTargets = new HashSet<string>();
            this.InformationalTargets = new HashSet<string>();
            this.PassTargets = new HashSet<string>();
            this.NotApplicableTargets = new HashSet<string>();
            this.ConfigurationErrorTargets = new HashSet<string>();
        }

        public RuntimeConditions RuntimeErrors { get; set; }

        public HashSet<string> PassTargets { get; set; }

        public HashSet<string> ErrorTargets { get; set; }

        public HashSet<string> WarningTargets { get; set; }

        public HashSet<string> NoteTargets { get; set; }

        public HashSet<string> InformationalTargets { get; set; }

        public HashSet<string> ConfigurationErrorTargets { get; set; }

        public HashSet<string> NotApplicableTargets { get; set; }
        public FileRegionsCache FileRegionsCache { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void AnalysisStarted()
        {
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
            this.RecordTestResult(result.Kind, result.Locations.First().PhysicalLocation.ArtifactLocation.Uri.LocalPath);
            this.RecordTestResult(result.Level, result.Locations.First().PhysicalLocation.ArtifactLocation.Uri.LocalPath);
        }

        public void RecordTestResult(ResultKind messageKind, string targetPath)
        {
            switch (messageKind)
            {
                case ResultKind.Pass:
                {
                    this.PassTargets.Add(targetPath);
                    break;
                }

                case ResultKind.NotApplicable:
                {
                    this.NotApplicableTargets.Add(targetPath);
                    break;
                }

                case ResultKind.Informational:
                {
                    this.InformationalTargets.Add(targetPath);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        public void RecordTestResult(FailureLevel messageKind, string targetPath)
        {
            switch (messageKind)
            {
                case FailureLevel.Error:
                {
                    this.ErrorTargets.Add(targetPath);
                    break;
                }

                case FailureLevel.Warning:
                {
                    this.WarningTargets.Add(targetPath);
                    break;
                }

                case FailureLevel.Note:
                {
                    this.NoteTargets.Add(targetPath);
                    break;
                }

                default:
                {
                    break;
                }
            }
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
