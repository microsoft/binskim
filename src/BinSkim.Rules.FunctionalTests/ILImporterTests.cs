// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace Microsoft.CodeAnalysis.IL
{
    public class ILImporterTests
    {
        [Fact]
        public void DemonstrateSetup()
        {
            string path = typeof(ILImporterTests).GetTypeInfo().Assembly.Location;

            using (var stream = File.OpenRead(path))
            using (var peReader = new PEReader(stream))
            {
                var metadataReader = peReader.GetMetadataReader();
                var reference = MetadataReference.CreateFromFile(path);
                var compilation = CSharpCompilation.Create("_", references: new[] { reference });
                var target = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference);

                var type = target.GetTypeByMetadataName("Microsoft.CodeAnalysis.IL.ILImporterTests");
                var method = (IMethodSymbol)type.GetMembers().Single(m => m.Name == "Scratch");
                var handle = (MethodDefinitionHandle)((IMetadataSymbol)method).MetadataHandle;
                var methodDef = metadataReader.GetMethodDefinition(handle);
                var methodBody = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);

                var importer = new ILImporter(compilation, method, methodBody);
                var body = importer.Import();
            }
        }

        public void Scratch()
        {
        }
    }
}
