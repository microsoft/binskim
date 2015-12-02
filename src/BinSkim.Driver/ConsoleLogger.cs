// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal class ConsoleLogger : IMessageLogger<BinaryAnalyzerContext>
    {
        public ConsoleLogger(bool verbose)
        {
            Verbose = verbose;
        }

        public bool Verbose { get; set; }

        public void Fail(BinaryAnalyzerContext context, string fullMessage)
        {
        }

        public void Pass(BinaryAnalyzerContext context, string fullMessage)
        {
            if (Verbose)
            {
                Console.WriteLine(GetMessageText(context, fullMessage, MessageKind.Pass));
            }
        }

        public void Pending(BinaryAnalyzerContext context, string fullMessage)
        {
            Console.WriteLine(GetMessageText(context, fullMessage, MessageKind.Pending));
        }

        public void NotApplicable(BinaryAnalyzerContext context, string fullMessage)
        {
        }

        public void Note(BinaryAnalyzerContext context, string fullMessage)
        {
            if (Verbose)
            {
                Console.WriteLine(GetMessageText(context, fullMessage, MessageKind.Note));
            }
        }


        public void Log(MessageKind messageKind, BinaryAnalyzerContext context, string message)
        {
            switch (messageKind)
            {
                case MessageKind.AnalyzingTarget:
                    {
                        if (Verbose)
                        {
                            Console.WriteLine("Analyzing target: " + context.PE.FileName);
                        }
                        break;
                    }

                case MessageKind.Pass:
                    {
                        if (Verbose)
                        {
                            Console.WriteLine(GetMessageText(context, message, MessageKind.Pass));
                        }
                        break;
                    }

                case MessageKind.Fail:
                    {
                        Console.WriteLine(GetMessageText(context, message, MessageKind.Fail));
                        break;
                    }

                case MessageKind.NotApplicable:
                    {
                        if (Verbose)
                        {
                            Console.WriteLine(GetMessageText(context, message, MessageKind.NotApplicable));
                        }
                        break;
                    }

                case MessageKind.Note:
                    {
                        if (Verbose)
                        {
                            Console.WriteLine(GetMessageText(context, message, MessageKind.Note));
                        }
                        break;
                    }

                case MessageKind.Pending:
                    {
                        Console.WriteLine(GetMessageText(context, message, MessageKind.Pending));
                        break;
                    }

                case MessageKind.InternalError:
                    {
                        Console.WriteLine(GetMessageText(context, message, MessageKind.InternalError));
                        break;
                    }

                case MessageKind.ConfigurationError:
                    {
                        Console.WriteLine(GetMessageText(context, message, MessageKind.ConfigurationError));
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException();
                    }
            }
        }
        public static string GetMessageText(BinaryAnalyzerContext context, string message, MessageKind messageKind)
        {
            string path = null;
            Uri uri = context.Uri;

            if (uri != null)
            {
                // If a path refers to a URI of form file://blah, we will convert to the local path           
                if (uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeFile)
                {
                    path = uri.LocalPath;
                }
                else
                {
                    path = uri.ToString();
                }
            }

            string issueType = null;

            switch (messageKind)
            {
                case MessageKind.ConfigurationError:
                    {
                        issueType = "CONFIGURATION ERROR";
                        break;
                    }

                case MessageKind.InternalError:
                    {
                        issueType = "INTERNAL ERROR";
                        break;
                    }

                case MessageKind.Fail:
                    {
                        issueType = "error";
                        break;
                    }

                case MessageKind.Pending:
                    {
                        issueType = "pending";
                        break;
                    }

                case MessageKind.Pass:
                    {
                        issueType = "pass";
                        break;
                    }

                case MessageKind.NotApplicable:
                case MessageKind.Note:
                    {
                        issueType = "note";
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException("Unknown message kind:" + messageKind.ToString());
                    }
            }

            string detailedDiagnosis = NormalizeMessage(message, enquote: false);

            return (path != null ? (path + ": ") : "") +
                   issueType + ": " +
                   context.Rule.Id + ": " +
                   detailedDiagnosis;
        }

        public static string NormalizeMessage(string message, bool enquote)
        {
            return (enquote ? "\"" : "") +
                message.Replace('"', '\'') +
                (enquote ? "\"" : "");
        }
    }
}