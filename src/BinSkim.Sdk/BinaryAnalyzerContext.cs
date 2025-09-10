// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class BinaryAnalyzerContext : AnalyzeContextBase
    {
        private IBinary iBinary;

        static BinaryAnalyzerContext()
        {
        }

        public IBinary Binary
        {
            get
            {
                this.iBinary ??=
                    BinaryTargetManager.GetBinaryFromFile(this.CurrentTarget.Uri,
                                                          this.SymbolPath,
                                                          this.LocalSymbolDirectories,
                                                          this.TracePdbLoads,
                                                          this.ComprehensiveBinaryParsing);

                return this.iBinary;
            }
            set => this.iBinary = value;
        }

        public override bool IsValidAnalysisTarget
        {
            get => this.Binary?.Valid == true;
        }

        public string LocalSymbolDirectories
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.LocalSymbolDirectories);
            set => this.Policy.SetProperty(BinaryParsersProperties.LocalSymbolDirectories, value);
        }

        public bool ComprehensiveBinaryParsing
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.ComprehensiveBinaryParsing) == true;
            set => this.Policy.SetProperty(BinaryParsersProperties.ComprehensiveBinaryParsing, value);
        }

        public bool TracePdbLoads { get; set; }

        public string SymbolPath
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.SymbolPath);
            set => this.Policy.SetProperty(BinaryParsersProperties.SymbolPath, value);
        }

        public override IAnalysisLogger Logger { get; set; }

        public override ReportingDescriptor Rule { get; set; }

        public override HashData Hashes { get; set; }

        public override string MimeType
        {
            get => Sarif.Writers.MimeType.Binary;
            set => throw new InvalidOperationException();
        }

        public override RuntimeConditions RuntimeErrors { get; set; }

        public override bool AnalysisComplete { get; set; }

        public CompilerDataLogger CompilerDataLogger { get; set; }

        public bool IgnorePdbLoadError
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.IgnorePdbLoadError) == true;
            set => this.Policy.SetProperty(BinaryParsersProperties.IgnorePdbLoadError, value);
        }

        public bool IgnorePELoadError
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.IgnorePELoadError) == true;
            set => this.Policy.SetProperty(BinaryParsersProperties.IgnorePELoadError, value);
        }

        public bool NormalizeOutputForComparison
        {
            get => this.Policy.GetProperty(BinaryParsersProperties.NormalizeOutputForComparison);
            set => this.Policy.SetProperty(BinaryParsersProperties.NormalizeOutputForComparison, value);
        }

        public bool DisableTelemetry
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.DisableTelemetry) == true;
            set => this.Policy.SetProperty(BinaryParsersProperties.DisableTelemetry, value);
        }

        public bool IncludeWixBinaries
        {
            get => this.Policy?.GetProperty(BinaryParsersProperties.IncludeWixBinaries) == true;
            set => this.Policy.SetProperty(BinaryParsersProperties.IncludeWixBinaries, value);
        }

        internal bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                this.iBinary?.Dispose();
                this.iBinary = null;

                (this.Logger as IDisposable)?.Dispose();
                this.Logger = null;

                if (this.CompilerDataLogger?.OwningContextHashCode == this.GetHashCode())
                {
                    this.CompilerDataLogger.Dispose();
                }

                this.disposed = true;
            }
        }

        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }
    }
}
