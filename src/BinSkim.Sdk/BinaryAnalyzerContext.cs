// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class BinaryAnalyzerContext : IAnalysisContext
    {
        private Uri uri;
        private IBinary iBinary;

        public IBinary Binary
        {
            get
            {
                this.iBinary ??= BinaryTargetManager.GetBinaryFromFile(
                        this.uri,
                        this.SymbolPath,
                        this.LocalSymbolDirectories,
                        this.TracePdbLoads);

                return this.iBinary;
            }
            set => this.iBinary = value;
        }

        public Exception TargetLoadException
        {
            get => this.Binary?.LoadException;
            set => throw new InvalidOperationException();
        }

        public bool IsValidAnalysisTarget
        {
            get => this.Binary?.Valid == true;
            set => throw new InvalidOperationException();
        }

        public string LocalSymbolDirectories { get; set; }

        public Uri TargetUri
        {
            get => this.uri;
            set
            {
                if (this.uri != null)
                {
                    throw new InvalidOperationException(SdkResources.IllegalContextReuse);
                }
                this.uri = value;
            }
        }

        public bool TracePdbLoads { get; set; }

        public string SymbolPath { get; set; }

        public IAnalysisLogger Logger { get; set; }

        public ReportingDescriptor Rule { get; set; }

        public PropertiesDictionary Policy { get; set; }

        public HashData Hashes { get; set; }

        public string MimeType
        {
            get => Sarif.Writers.MimeType.Binary;
            set => throw new InvalidOperationException();
        }

        public RuntimeConditions RuntimeErrors { get; set; }

        public bool AnalysisComplete { get; set; }

        public DefaultTraces Traces { get; set; }

        public CompilerDataLogger CompilerDataLogger
        {
            get { return this.Policy.GetProperty(SharedCompilerDataLoggerProperty); }
            set { this.Policy.SetProperty(SharedCompilerDataLoggerProperty, value); }
        }

        public bool IgnorePdbLoadError { get; set; }
        public bool ForceOverwrite { get; set; }

        internal bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                this.iBinary?.Dispose();
                this.iBinary = null;

                if (this.CompilerDataLogger?.OwningContextHashCode == this.GetHashCode())
                {
                    this.CompilerDataLogger.Dispose();
                }

                this.disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }

        public static PerLanguageOption<CompilerDataLogger> SharedCompilerDataLoggerProperty { get; } =
            new PerLanguageOption<CompilerDataLogger>(
                "CompilerTelemetry", nameof(SharedCompilerDataLoggerProperty), defaultValue: () => null,
                "A shared CompilerDataLogger instance that will be passed to all skimmers.");

    }
}
