// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Sarif.DataContracts;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.IL
{
    internal class ExportRulesCommand : DriverCommand<ExportRulesOptions>
    {
        public override int Run(ExportRulesOptions exportOptions)
        {
            int result = FAILED;

            try
            {
                ImmutableArray<IBinarySkimmer> skimmers = DriverUtilities.GetExports<IBinarySkimmer>();

                string format = "";
                string outputFilePath = exportOptions.OutputFilePath;
                string extension = Path.GetExtension(outputFilePath);
                
                switch (extension)
                {
                    case (".json"):
                    case (".sarif"):
                    {
                        format = "SARIF";
                        ImmutableArray<IOptionsProvider> options = DriverUtilities.GetExports<IOptionsProvider>();
                        OutputSarifRulesMetada(outputFilePath, skimmers, options);
                        break;
                    }

                    case (".xml"):
                    {
                        format = "SonarQube";
                        OutputSonarQubeRulesMetada(outputFilePath, skimmers);
                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException("Unrecognized output file extension: " + extension);
                    }
                }

                result = SUCCEEDED;
                Console.WriteLine(format + " rules metadata exported to: " + Path.GetFullPath(outputFilePath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }

            return result;
        }

        private void OutputSonarQubeRulesMetada(string outputFilePath, ImmutableArray<IBinarySkimmer> skimmers)
        {
            const string TAB = "   ";
            var sb = new StringBuilder();

            SortedDictionary<int, IRuleContext> sortedRuleContexts = new SortedDictionary<int, IRuleContext>();

            foreach (IBinarySkimmer skimmer in skimmers)
            {
                var ruleContext = (IRuleContext)skimmer;                
                int numericId = Int32.Parse(ruleContext.Id.Substring(2));
                sortedRuleContexts[numericId] = ruleContext;
            }

            sb.AppendLine("<?xml version='1.0' encoding='UTF-8'?>" + Environment.NewLine +
                         "<rules>");

            foreach (IRuleContext ruleContext in sortedRuleContexts.Values)
            {
                sb.AppendLine(TAB + "<rule>");
                sb.AppendLine(TAB + TAB + "<key>" + ruleContext.Id + "</key>");
                sb.AppendLine(TAB + TAB + "<name>" + ruleContext.Name + "</name>");
                sb.AppendLine(TAB + TAB + "<severity>MAJOR</severity>");

                sb.AppendLine(TAB + TAB + "<description>" + Environment.NewLine +
                              TAB + TAB + TAB + "<![CDATA[" + Environment.NewLine +
                              TAB + TAB + TAB + TAB + ruleContext.FullDescription + Environment.NewLine +
                              TAB + TAB + TAB + "]]>" + Environment.NewLine +
                              TAB + TAB + "</description>");

                sb.AppendLine(TAB + TAB + "<tag>binary</tag>");
                sb.AppendLine(TAB + "</rule>");
            }

            sb.AppendLine("</rules>" + Environment.NewLine + "</profile>");

            File.WriteAllText(outputFilePath, sb.ToString());
        }

        private void OutputSarifRulesMetada(string outputFilePath, ImmutableArray<IBinarySkimmer> skimmers, ImmutableArray<IOptionsProvider> options)
        {
            var log = new ResultLog();

            log.Version = SarifVersion.ZeroDotFour;

            // The SARIF spec currently requires an array
            // of run logs with at least one member
            log.RunLogs = new List<RunLog>();

            var runLog = new RunLog();
            runLog.ToolInfo = SarifLogger.CreateDefaultToolInfo(VersionConstants.Prerelease);
            runLog.Results = new List<Result>();

            log.RunLogs.Add(runLog);
            runLog.ToolInfo.RuleInfo = new List<RuleDescriptor>();

            SortedDictionary<int, RuleDescriptor> sortedRuleDescriptors = new SortedDictionary<int, RuleDescriptor>();

            foreach (IBinarySkimmer skimmer in skimmers)
            {
                var ruleContext = (IRuleContext)skimmer;
                var ruleDescriptor = new RuleDescriptor();

                ruleDescriptor.Id = ruleContext.Id;
                ruleDescriptor.Name = ruleContext.Name;
                ruleDescriptor.FullDescription = ruleContext.FullDescription;

                int numericId = Int32.Parse(ruleDescriptor.Id.Substring(2));

                sortedRuleDescriptors[numericId] = ruleDescriptor;
            }

            foreach (RuleDescriptor ruleDescriptor in sortedRuleDescriptors.Values)
            {
                runLog.ToolInfo.RuleInfo.Add(ruleDescriptor);
            }

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = SarifContractResolver.Instance,
                Formatting = Formatting.Indented,
            };
            File.WriteAllText(outputFilePath, JsonConvert.SerializeObject(log, settings));
        }
    }
}
