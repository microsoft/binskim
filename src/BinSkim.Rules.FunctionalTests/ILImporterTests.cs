// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
                var assembly = MetadataReference.CreateFromFile(path);
                var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                var compilation = CSharpCompilation.Create("_", references: new[] { assembly, mscorlib });
                var target = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(assembly);

                var type = target.GetTypeByMetadataName(typeof(ILImporterTests).FullName);
                var method = (IMethodSymbol)type.GetMembers().Single(m => m.Name == nameof(Scratch));
                var handle = (MethodDefinitionHandle)method.MetadataHandle;
                var methodDef = metadataReader.GetMethodDefinition(handle);
                var methodBody = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);

                var importer = new ILImporter(compilation, metadataReader, method, methodBody);
                var body = importer.Import();
            }
        }

        public int InstanceField;
        public static float StaticField;
        public static string[] xs;
        public static object Obj;
        public static readonly S ReadonlyMutableStruct = new S();
        public static S MutableStruct;

        class Foo
        {
            public int X = 22;
        }

        class Bar
        {
            public int Y = 23;
        }

        public struct S
        {
            public int X, Y;

            public void Mutate()
            {
                X = Y = 42;
            }
        }

        public unsafe void Scratch(string x, int y, float z, __arglist)
        {
            Scratch("x", y, 1, __arglist(1, 2, 3));

            var iterator = new ArgIterator(__arglist);
            var value = __refvalue(iterator.GetNextArg(), int);
            var type = __reftype(iterator.GetNextArg());
            ByRef(ref __refvalue(iterator.GetNextArg(), int));
            int v = 3;
            ByRef(ref v);
            var r = __makeref(v);

            ReadonlyMutableStruct.Mutate();
            MutableStruct.Mutate();
            new S().Mutate();

            int q = (int)Obj;
            Type t = typeof(Foo);

            byte* b = stackalloc byte[33];

            try
            {
                switch (y)
                {
                    case 0:
                        StaticMethod(1, 1);
                        break;

                    case 2:
                    case 3:
                        InstanceMethod(q);
                        break;

                    default:
                        StaticMethod("c", 2);
                        break;
                }

                xs = new string[3];
                xs[0] = xs[1];

                if (Obj is Foo)
                {
                    InstanceMethod(((Foo)Obj).X);
                }

                var bar = Obj as Bar;
                if (bar != null)
                {
                    InstanceMethod(bar.Y);
                }

                InstanceField = 42;
                StaticField = 42;

                InstanceField += y;
                InstanceField -= y;
                InstanceField /= y;
                InstanceField %= y;

                StaticField += z;
                StaticField -= z;
                StaticField /= z;
                StaticField %= z;

                unsafe
                {
                    int* p = &y;
                    *p = 4242;
                }


                int tmp = StaticMethod(y == 12 ? "hello" : "goodbye", ~InstanceField);
                InstanceMethod(x, -StaticField);
            }
            catch (OverflowException)
            {
                try
                {
                    StaticMethod("goodbye", 99);
                }
                catch (InvalidOperationException)
                {

                }
            }
            catch (Exception ex) when (Condition())
            {
                throw ex;
            }
            finally
            {
                InstanceMethod(x, y);
            }
        }

        public static int StaticMethod(object x, float y)
        {
            return 42;
        }

        public void InstanceMethod(string x, float y)
        {
        }

        public void InstanceMethod(int x)
        {
        }

        public void ByRef(ref int x)
        {
        }

        public static bool Condition()
        {
            return false;
        }
    }
}
