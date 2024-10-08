// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL
{
    public static class ExpandArguments
    {
        public static string[] GenerateArguments(
            string[] args,
            IFileSystem fileSystem,
            IEnvironmentVariables environmentVariables)
        {
            var expandedArguments = new List<string>();

            foreach (string argument in args)
            {
                if (!IsResponseFileArgument(argument))
                {
                    expandedArguments.Add(argument);
                    continue;
                }

                string responseFile = argument.Trim('"').Substring(1);

                responseFile = environmentVariables.ExpandEnvironmentVariables(responseFile);
                responseFile = fileSystem.PathGetFullPath(responseFile);

                string[] responseFileLines = fileSystem.FileReadAllLines(responseFile);
                ExpandResponseFile(responseFileLines, expandedArguments);
            }

            return expandedArguments.ToArray();
        }

        private static bool IsResponseFileArgument(string argument)
        {
            return argument.Length > 1 && argument[0] == '@';
        }

        private static void ExpandResponseFile(string[] responseFileLines, List<string> expandedArguments)
        {
            foreach (string responseFileLine in responseFileLines)
            {
                List<string> fileList = ArgumentSplitter.CommandLineToArgvW(responseFileLine.Trim()) ??
                    throw new InvalidOperationException("Could not parse response file line:" + responseFileLine);

                expandedArguments.AddRange(fileList);
            }
        }
    }
}
