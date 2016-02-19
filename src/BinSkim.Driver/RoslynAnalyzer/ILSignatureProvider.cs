// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Reflection.Metadata.Decoding;
using System.Reflection.Metadata;
using System.Linq;

namespace Microsoft.CodeAnalysis.IL
{
    internal sealed class ILSignatureProvider : ISignatureTypeProvider<ITypeSymbol>
    {
        private readonly Compilation _compilation;
        private readonly IMethodSymbol _method;

        public ILSignatureProvider(Compilation compilation, IMethodSymbol method)
        {
            _compilation = compilation;
            _method = method;
        }

        public ITypeSymbol GetArrayType(ITypeSymbol elementType, ArrayShape shape)
        {
            return _compilation.CreateArrayTypeSymbol(elementType, shape.Rank);
        }

        public ITypeSymbol GetByReferenceType(ITypeSymbol elementType)
        {
            return _compilation.CreatePointerTypeSymbol(elementType); // TODO: by-ref
        }

        public ITypeSymbol GetFunctionPointerType(MethodSignature<ITypeSymbol> signature)
        {
            return _compilation.GetSpecialType(SpecialType.System_IntPtr);
        }

        public ITypeSymbol GetGenericInstance(ITypeSymbol genericType, ImmutableArray<ITypeSymbol> typeArguments)
        {
            return InstantiateGeneric((INamedTypeSymbol)genericType, typeArguments, 0, typeArguments.Length);
        }

        private INamedTypeSymbol InstantiateGeneric(INamedTypeSymbol type, ImmutableArray<ITypeSymbol> typeArguments, int index, int count)
        {
            if (count == 0)
            {
                return type;
            }

            if (type is IErrorTypeSymbol)
            {
                throw new NotImplementedException();
            }

            if (count == type.Arity)
            {
                var typeArgumentArray = new ITypeSymbol[count];
                typeArguments.CopyTo(index, typeArgumentArray, 0, count);
                return type.Construct(typeArgumentArray);
            }

            var parent = type.ContainingType;

            if (parent == null)
            {
                throw new BadImageFormatException();
            }

            var outerType = InstantiateGeneric(type.ContainingType, typeArguments, index, count - type.Arity);
            var innerType = outerType.GetTypeMembers().Single(t => t.OriginalDefinition == type);
            return InstantiateGeneric(innerType, typeArguments, index + parent.Arity, type.Arity);
        }

        public ITypeSymbol GetGenericMethodParameter(int index)
        {
            return _method.TypeParameters[index];
        }

        public ITypeSymbol GetGenericTypeParameter(int index)
        {
            return GetGenericTypeParameter(_method.ContainingType, index);
        }

        private ITypeParameterSymbol GetGenericTypeParameter(INamedTypeSymbol type, int index)
        {
            while (type != null)
            {
                int combinedParentArity = GetCombinedArity(type.ContainingType);

                if (index >= combinedParentArity)
                {
                    return type.TypeParameters[index - combinedParentArity];
                }

                type = type.ContainingType;
            }

            throw new BadImageFormatException();
        }

        private static int GetCombinedArity(INamedTypeSymbol type)
        {
            // TODO: This loop makes GetGenericTypeParameter O(N^2) where N is the nested type depth.
            //       We can get the combined arity directly from metadata.
            int total = 0;

            while (type != null)
            {
                total += type.Arity;
                type = type.ContainingType;
            }

            return total;
        }

        public ITypeSymbol GetModifiedType(MetadataReader reader, bool isRequired, EntityHandle modifierTypeHandle, ITypeSymbol unmodifiedType)
        {
            return unmodifiedType; // TODO
        }

        public ITypeSymbol GetPinnedType(ITypeSymbol elementType)
        {
            return elementType; // TODO
        }

        public ITypeSymbol GetPointerType(ITypeSymbol elementType)
        {
            return _compilation.CreatePointerTypeSymbol(elementType);
        }

        public ITypeSymbol GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return _compilation.GetSpecialType(GetSpecialType(typeCode));
        }

        public ITypeSymbol GetSZArrayType(ITypeSymbol elementType)
        {
            return _compilation.CreateArrayTypeSymbol(elementType);
        }

        public ITypeSymbol GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, SignatureTypeHandleCode code)
        {
            return GetTypeFromHandle(handle);
        }

        public ITypeSymbol GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, SignatureTypeHandleCode code)
        {
            return GetTypeFromHandle(handle);
        }

        private SpecialType GetSpecialType(PrimitiveTypeCode typeCode)
        {
            switch (typeCode)
            {
                case PrimitiveTypeCode.Boolean:
                    return SpecialType.System_Boolean;
                case PrimitiveTypeCode.Byte:
                    return SpecialType.System_Byte;
                case PrimitiveTypeCode.SByte:
                    return SpecialType.System_SByte;
                case PrimitiveTypeCode.Char:
                    return SpecialType.System_Char;
                case PrimitiveTypeCode.Single:
                    return SpecialType.System_Single;
                case PrimitiveTypeCode.Double:
                    return SpecialType.System_Double;
                case PrimitiveTypeCode.Int16:
                    return SpecialType.System_Int16;
                case PrimitiveTypeCode.Int32:
                    return SpecialType.System_Int32;
                case PrimitiveTypeCode.Int64:
                    return SpecialType.System_Int64;
                case PrimitiveTypeCode.UInt16:
                    return SpecialType.System_UInt16;
                case PrimitiveTypeCode.UInt32:
                    return SpecialType.System_UInt32;
                case PrimitiveTypeCode.UInt64:
                    return SpecialType.System_UInt64;
                case PrimitiveTypeCode.IntPtr:
                    return SpecialType.System_IntPtr;
                case PrimitiveTypeCode.UIntPtr:
                    return SpecialType.System_UIntPtr;
                case PrimitiveTypeCode.Object:
                    return SpecialType.System_Object;
                case PrimitiveTypeCode.String:
                    return SpecialType.System_String;
                case PrimitiveTypeCode.TypedReference:
                    return SpecialType.System_TypedReference;
                case PrimitiveTypeCode.Void:
                    return SpecialType.System_Void;
                default:
                    throw new ArgumentOutOfRangeException("typeCode");
            }
        }

        private ITypeSymbol GetTypeFromHandle(EntityHandle handle)
        {
            return (ITypeSymbol)(_method.ContainingModule.GetSymbolForMetadataHandle(handle, _method));
        }
    }
}
