// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat;
using Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat.DataContracts;
using Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat.Writers;

using Newtonsoft.Json;

using Sarif = Microsoft.CodeAnalysis.StaticAnalysisResultsInterchangeFormat;

namespace Microsoft.CodeAnalysis.IL
{
    public class SarifLogger : IBinaryMessageLogger, IDisposable
    {
        private FileStream _fileStream;
        private TextWriter _textWriter;
        private JsonTextWriter _jsonTextWriter;
        private ResultLogJsonWriter _issueLogJsonWriter;

        public SarifLogger(
            string outputFilePath,
            bool verbose,
            IEnumerable<string> analysisTargets,
            bool computeTargetsHash)
        {
            Verbose = verbose;

            _fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            _textWriter = new StreamWriter(_fileStream);
            _jsonTextWriter = new JsonTextWriter(_textWriter);

            // for debugging it is nice to have the following line added.
            _jsonTextWriter.Formatting = Newtonsoft.Json.Formatting.Indented;

            _issueLogJsonWriter = new ResultLogJsonWriter(_jsonTextWriter);

            Assembly binskimAssembly = this.GetType().Assembly;

            Version version = binskimAssembly.GetName().Version;
            ToolInfo toolInfo = new ToolInfo();
            toolInfo.Name = "BinSkim";
            toolInfo.Version = version.Major.ToString() + "." + version.Minor.ToString() + "." + version.Build.ToString();
            toolInfo.FullName = "BinSkim " + toolInfo.Version + "-beta";

            RunInfo runInfo = new RunInfo();
            runInfo.AnalysisTargets = new List<FileReference>();

            foreach (string target in analysisTargets)
            {
                var fileReference = new FileReference()
                {
                    Uri = target.CreateUriForJsonSerialization(),
                };

                if (computeTargetsHash)
                {
                    string sha256Hash = PE.ComputeSha256Hash(target) ?? "[could not compute file hash]";
                    fileReference.Hashes = new List<Hash>(new Hash[]
                    {
                            new Hash()
                            {
                                Value = sha256Hash,
                                Algorithm = "SHA-256",
                            }
                    });
                }
                runInfo.AnalysisTargets.Add(fileReference);
            }
            runInfo.InvocationInfo = Environment.CommandLine;

            _issueLogJsonWriter.WriteToolAndRunInfo(toolInfo, runInfo);
        }

        public bool Verbose { get; set; }

        public void Dispose()
        {
            // Disposing the json writer closes the stream but the textwriter 
            // still needs to be disposed or closed to write the results
            if (_issueLogJsonWriter != null) { _issueLogJsonWriter.Dispose(); }
            if (_textWriter != null) { _textWriter.Dispose(); }
        }

        public void Log(MessageKind messageKind, BinaryAnalyzerContext context, string message)
        {
            switch (messageKind)
            {
                case MessageKind.AnalyzingTarget:
                    {
                        break;
                    }

                case MessageKind.Pass:
                    {
                        if (Verbose)
                        {
                            WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.Pass);
                        }
                        break;
                    }

                case MessageKind.Fail:
                    {
                        WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.Error);
                        break;
                    }

                case MessageKind.NotApplicable:
                    {
                        if (Verbose)
                        {
                            WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.NotApplicable);
                        }
                        break;
                    }

                case MessageKind.Note:
                    {
                        if (Verbose)
                        {
                            WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.Note);
                        }
                        break;
                    }

                case MessageKind.Pending:
                    {
                        WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.Pending);
                        break;
                    }

                case MessageKind.ConfigurationError:
                {
                    WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.ConfigurationError);
                    break;
                }

                case MessageKind.InternalError:
                    {
                        WriteJsonIssue(context.PE.FileName, context.Rule.Id, message, IssueKind.InternalError);
                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException();
                    }
            }
        }
        private void WriteJsonIssue(string binary, string ruleId, string message, IssueKind issueKind)
        {
            Result result = new Result();

            result.RuleId = ruleId;
            result.FullMessage = message;
            result.Kind = issueKind.ToString().ToLowerInvariant()[0] + issueKind.ToString().Substring(1);
            result.Locations = new[]{
                new Sarif.DataContracts.Location {  
                    AnalysisTarget = new[]
                    {
                        new PhysicalLocationComponent
                        {
                            // Why? When NewtonSoft serializes this Uri, it will use the
                            // original string used to construct the Uri. For a file path, 
                            // this will be the local file path. We want to persist this 
                            // information using the file:// protocol rendering, however.
                            Uri = binary.CreateUriForJsonSerialization(),
                            MimeType = MimeType.Binary
                        }
                    }
                }
            };

            _issueLogJsonWriter.WriteResult(result);
        }
    }
}



