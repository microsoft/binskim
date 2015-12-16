// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal class StatisticsLogger : IMessageLogger<BinaryAnalyzerContext>, IDisposable
    {
        private Stopwatch _stopwatch;
        private long _targetsCount;
        private long _invalidTargetsCount;

        public StatisticsLogger()
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public void Log(MessageKind messageKind, BinaryAnalyzerContext context, string message)
        {
            switch (messageKind)
            {
                case MessageKind.AnalyzingTarget:
                    {
                        _targetsCount++;
                        break;
                    }

                case MessageKind.Pass:
                    {
                        break;
                    }

                case MessageKind.Fail:
                    {
                        break;
                    }

                case MessageKind.NotApplicable:
                    {
                        if (context.Rule.Id == ErrorRules.InvalidPE.Id)
                        {
                            _invalidTargetsCount++;
                        }
                        break;
                    }

                case MessageKind.Note:
                    {
                        break;
                    }

                case MessageKind.Pending:
                    {
                        break;
                    }

                case MessageKind.InternalError:
                    {
                        break;
                    }

                case MessageKind.ConfigurationError:
                    {
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException();
                    }
            }
        }

        public void Dispose()
        {
            Console.WriteLine();
            Console.WriteLine("# valid targets: " + _targetsCount.ToString());
            Console.WriteLine("# invalid targets: " + _invalidTargetsCount.ToString());
            Console.WriteLine("Time elapsed: " + _stopwatch.Elapsed.ToString());
        }
    }
}
