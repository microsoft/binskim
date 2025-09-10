// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Writers;

namespace Microsoft.CodeAnalysis.IL
{
    internal class NormalizingSarifLogger : IAnalysisLogger, IDisposable
    {
        private IAnalysisLogger _innerLogger;
        private SarifRewritingVisitor _pathRewritingVisitor;

        public NormalizingSarifLogger(IAnalysisLogger innerLogger, string enlistmentRoot)
        {
            _innerLogger = innerLogger;

            if (!string.IsNullOrEmpty(enlistmentRoot))
            {
                _pathRewritingVisitor = new PathRewritingVisitor(enlistmentRoot);
            }

            NormalizeRunTool(innerLogger);
        }

        private void NormalizeRunTool(IAnalysisLogger innerLogger)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type t = typeof(SarifLogger);

            // Try private/internal property first (newer SDKs), then known field names (older SDKs)
            Run run = (Run)t.GetField("_run", Flags)?.GetValue((SarifLogger)innerLogger);

            if (run?.Tool?.Driver != null)
            {
                run.Tool.Driver.Name = "BinSkim";
                run.Tool.Driver.Organization = "Microsoft";
                run.Tool.Driver.Product = null;
                run.Tool.Driver.FullName = null;
                run.Tool.Driver.Version = default;
                run.Tool.Driver.SemanticVersion = default;
                run.Tool.Driver.DottedQuadFileVersion = default;

                run.Tool.Driver.RemoveProperty("comments");
            }
        }

        public FileRegionsCache FileRegionsCache { get => _innerLogger.FileRegionsCache; set => _innerLogger.FileRegionsCache = value; }

        public void AnalysisStarted()
        {
            _innerLogger.AnalysisStarted();
        }

        public void AnalysisStopped(RuntimeConditions runtimeConditions)
        {
            _innerLogger.AnalysisStopped(runtimeConditions);
        }

        public void AnalyzingTarget(IAnalysisContext context)
        {
            _innerLogger.AnalyzingTarget(context);
        }

        public void Log(ReportingDescriptor rule, Result result, int? extensionIndex = null)
        {
            _pathRewritingVisitor?.Visit(rule);
            _pathRewritingVisitor?.Visit(result);
            _innerLogger.Log(rule, result, extensionIndex);
        }

        public void LogConfigurationNotification(Notification notification)
        {
            _pathRewritingVisitor?.Visit(notification);
            _innerLogger.LogConfigurationNotification(notification);
        }

        public void LogToolNotification(Notification notification, ReportingDescriptor associatedRule = null)
        {
            _pathRewritingVisitor?.Visit(notification);
            _innerLogger.LogToolNotification(notification, associatedRule);
        }

        public void TargetAnalyzed(IAnalysisContext context)
        {
            _innerLogger.TargetAnalyzed(context);
        }

        public void Dispose()
        {
            ((SarifLogger)_innerLogger)?.Dispose();
            _innerLogger = null;
        }

        private class PathRewritingVisitor : SarifRewritingVisitor
        {
            private readonly string _enlistmentRoot;
            public PathRewritingVisitor(string enlistmentRoot)
            {
                _enlistmentRoot = enlistmentRoot;
            }
            public override ArtifactLocation VisitArtifactLocation(ArtifactLocation artifactLocation)
            {
                if (artifactLocation?.Uri != null && !string.IsNullOrEmpty(_enlistmentRoot))
                {
                    string path = artifactLocation.Uri.GetFilePath();
                    if (path.StartsWith(_enlistmentRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = path.Substring(_enlistmentRoot.Length).TrimStart('\\', '/');
                        artifactLocation.Uri = new Uri(relativePath, UriKind.Relative);
                    }
                }
                return base.VisitArtifactLocation(artifactLocation);
            }
        }
    }
}
