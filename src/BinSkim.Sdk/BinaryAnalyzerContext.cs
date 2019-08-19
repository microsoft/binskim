// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class BinaryAnalyzerContext : IAnalysisContext
    {
        public BinaryAnalyzerContext()
        {

        }

        private Uri _uri;
        private IBinary _iBinary;

        public IBinary Binary
        {
            get
            {
                _iBinary = _iBinary ?? BinaryTargetManager.GetBinaryFromFile(_uri);
                return _iBinary;
            }
            set
            {
                _iBinary = value;
            }
        }

        public Exception TargetLoadException
        {
            get { return Binary != null ? Binary.LoadException : null; }
            set { throw new InvalidOperationException(); }
        }

        public bool IsValidAnalysisTarget
        {
            get { return Binary != null && Binary.Valid; }
            set { throw new InvalidOperationException(); }
        }

        public Uri TargetUri
        {
            get
            {
                return _uri;
            }
            set
            {
                if (_uri != null)
                {
                    throw new InvalidOperationException(SdkResources.IllegalContextReuse);
                }
                _uri = value;
            }
        }

        public IAnalysisLogger Logger { get; set; }

        public ReportingDescriptor Rule { get; set; }

        public PropertiesDictionary Policy { get; set; }

        public HashData Hashes { get; set; }

        public string MimeType
        {
            get { return Microsoft.CodeAnalysis.Sarif.Writers.MimeType.Binary; }
            set { throw new InvalidOperationException(); }
        }

        public RuntimeConditions RuntimeErrors { get; set; }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_iBinary != null)
                    {
                        _iBinary.Dispose();
                        _iBinary = null;
                    }
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
