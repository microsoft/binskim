// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal class TestMessageLogger : IResultLogger
    {
        public TestMessageLogger()
        {
            FailTargets = new HashSet<string>();
            PassTargets = new HashSet<string>();
            NotApplicableTargets = new HashSet<string>();
        }

        public HashSet<string> PassTargets { get; set; }

        public HashSet<string> FailTargets { get; set; }

        public HashSet<string> NotApplicableTargets { get; set; }

        public void Log(ResultKind messageKind, IAnalysisContext context, string message)
        {
            switch (messageKind)
            {
                case ResultKind.Pass:
                {
                    PassTargets.Add(context.TargetUri.LocalPath);
                    break;
                }

                case ResultKind.Error:
                {
                    FailTargets.Add(context.TargetUri.LocalPath);
                    break;
                }

                case ResultKind.NotApplicable:
                {
                    NotApplicableTargets.Add(context.TargetUri.LocalPath);
                    break;
                }

                case ResultKind.Note:
                case ResultKind.InternalError:
                case ResultKind.ConfigurationError:
                {
                    throw new NotImplementedException();
                }
                default:
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public void Log(ResultKind messageKind, IAnalysisContext context, FormattedMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
