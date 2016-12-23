// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL
{
    internal class DumpCommand : DriverCommand<DumpOptions>
    {
        public override int Run(DumpOptions dumpOptions)
        {
            var targets = new List<string>();

            foreach (string specifier in dumpOptions.BinaryFileSpecifiers)
            {
                if (Directory.Exists(specifier))
                {
                    var fileSpecifier = new FileSpecifier(specifier + ".dll", recurse: dumpOptions.Recurse);
                    targets.AddRange(fileSpecifier.Files);

                    fileSpecifier = new FileSpecifier(specifier + ".exe", recurse: dumpOptions.Recurse);
                    targets.AddRange(fileSpecifier.Files);
                }
                else
                {
                    var fileSpecifier = new FileSpecifier(specifier, recurse: dumpOptions.Recurse);
                    targets.AddRange(fileSpecifier.Files);
                }
            }

            var dumpTask = Task.Run(() => Parallel.ForEach(targets, (target) => DumpFile(target, dumpOptions.Verbose)));
            dumpTask.Wait();

            return 0;
        }

        private const string Indent = "\t";
        private const string Delimiter = ", ";

        private void DumpFile(string target, bool verbose)
        {
            PE pe;
            var sb = new StringBuilder();
            try
            {
                pe = new PE(target);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine(Path.GetFileName(target) + ": Unauthorized access exception");
                return;
            }

            sb.AppendLine(Path.GetFileName(pe.FileName) + ":");

            if (verbose)
            {
                sb.AppendLine(Indent + "Path: " + pe.FileName);
            }

            sb.Append(Indent + "Attr: ");

            if (!pe.IsPEFile)
            {
                sb.AppendLine("Not a portable executable");
                sb.AppendLine();
                return;
            }

            string language = pe.IsManaged ? "Pure Managed" : "Native";
            if (pe.IsManaged && !pe.IsILOnly) { language = "Mixed Managed"; };
            sb.Append(language);

            string machine = pe.Machine.ToString();
            sb.Append(Delimiter + machine);

            string subsystem = pe.Subsystem.ToString();
            sb.Append(Delimiter + subsystem);

            if (pe.IsKernelMode)
            {
                sb.Append(Delimiter + "Kernel Mode");
            }

            if (pe.IsResourceOnly)
            {
                sb.Append(Delimiter + "Resource Only");
            }

            sb.Append(Delimiter + "Link " + pe.LinkerVersion.ToString());

            sb.AppendLine(); // Close comma-separated attributes line

            sb.Append(Indent + "Pdb : ");
            Pdb pdb = null;
            try
            {
                pdb = new Pdb(pe.FileName);
            }
            catch (PdbParseException pdbParseException)
            {
                sb.AppendLine(pdbParseException.ExceptionCode.ToString());
            }

            if (pdb != null)
            {
                if (verbose)
                {
                    sb.AppendLine(pdb.PdbLocation);
                }
                else
                {
                    sb.AppendLine(Path.GetFileName(pdb.PdbLocation));
                }
            }

            sb.AppendLine(Indent + "SHA1: " + pe.SHA1Hash);

            Console.Out.WriteLineAsync(sb.ToString());
        }
    }
}
