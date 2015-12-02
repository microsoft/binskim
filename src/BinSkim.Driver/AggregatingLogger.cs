// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    public class AggregatingLogger : IMessageLogger<BinaryAnalyzerContext>, IDisposable
    {
        public AggregatingLogger() : this(null)
        {
        }

        public AggregatingLogger(IEnumerable<IMessageLogger<BinaryAnalyzerContext>> loggers)
        {
            this.Loggers = loggers != null ?
                new List<IMessageLogger<BinaryAnalyzerContext>>(loggers) :
                new List<IMessageLogger<BinaryAnalyzerContext>>();
        }

        public IList<IMessageLogger<BinaryAnalyzerContext>> Loggers { get; set; }

        public void Dispose()
        {
            foreach (IMessageLogger<BinaryAnalyzerContext> logger in Loggers)
            {
                using (logger as IDisposable) { };
            }
        }

        public void Log(MessageKind messageKind, BinaryAnalyzerContext context, string message)
        {
            foreach (IMessageLogger<BinaryAnalyzerContext> logger in Loggers)
            {
                logger.Log(messageKind, context, message);
            }
        }
    }
}
