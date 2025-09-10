// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Writers;

namespace Microsoft.CodeAnalysis.IL
{
    internal class NormalizingSarifLogger : IAnalysisLogger, IDisposable
    {
        private IAnalysisLogger _innerLogger;

        public NormalizingSarifLogger(IAnalysisLogger innerLogger)
        {
            _innerLogger = innerLogger;

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
            _innerLogger.Log(rule, result, extensionIndex);
        }

        public void LogConfigurationNotification(Notification notification)
        {
            _innerLogger.LogConfigurationNotification(notification);
        }

        public void LogToolNotification(Notification notification, ReportingDescriptor associatedRule = null)
        {
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
    }
}
