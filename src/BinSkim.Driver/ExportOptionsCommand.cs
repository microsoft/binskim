// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.IL
{
    internal class ExportOptionsCommand : DriverCommand<ExportOptionsOptions>
    {
        public override int Run(ExportOptionsOptions exportOptions)
        {
            int result = FAILED;

            try
            {
                PropertyBag allOptions = new PropertyBag();

                // The export command could be updated in the future to accept an arbitrary set
                // of analyzers for which to build an options XML file suitable for configuring them.
                // Currently, we perform discovery against the built-in CodeFormatter rules
                // and analyzers only.
                ImmutableArray<IOptionsProvider> providers = DriverUtilities.GetExports<IOptionsProvider>();
                foreach (IOptionsProvider provider in providers)
                {
                    foreach (IOption option in provider.GetOptions())
                    {
                        allOptions.SetProperty(option, option.DefaultValue);
                    }
                }
                allOptions.SaveTo(exportOptions.OutputFilePath, id: "binskim-policy");
                Console.WriteLine("Options file saved to: " + Path.GetFullPath(exportOptions.OutputFilePath));

                result = SUCCEEDED;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }

            return result;
        }

    }
}
