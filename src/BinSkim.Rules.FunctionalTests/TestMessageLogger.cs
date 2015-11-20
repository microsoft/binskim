// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.BinSkim.Sdk;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    internal class TestMessageLogger : IMessageLogger<BinaryAnalyzerContext>
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

        public void Log(MessageKind messageKind, BinaryAnalyzerContext context, string message)
        {
            switch (messageKind)
            {
                case MessageKind.Pass:
                    {
                        PassTargets.Add(context.PE.FileName);
                        break;
                    }

                case MessageKind.Fail:
                    {
                        FailTargets.Add(context.PE.FileName);
                        break;
                    }

                case MessageKind.NotApplicable:
                    {
                        NotApplicableTargets.Add(context.PE.FileName);
                        break;
                    }

                case MessageKind.Note:
                case MessageKind.Pending:
                case MessageKind.InternalError:
                case MessageKind.ConfigurationError:
                    {
                        throw new NotImplementedException();
                    }
            }
        }
    }
}
