//
//  SpanMarshallingWrapper.cs
//
//  Copyright (c) 2018 Firwood Software
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AdvancedDLSupport.Extensions;
using AdvancedDLSupport.Pipeline;
using AdvancedDLSupport.Reflection;
using JetBrains.Annotations;
using StrictEmit;

namespace AdvancedDLSupport.ImplementationGenerators
{
    /// <summary>
    /// Generates wrapper instructions for returning <see cref="Span{T}"/> from unmanaged code
    /// through a pointer and provided length.
    /// </summary>
    internal class SpanMarshallingWrapper : CallWrapperBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanMarshallingWrapper"/> class.
        /// </summary>
        /// <param name="targetModule">The module where the implementation should be generated.</param>
        /// <param name="targetType">The type in which the implementation should be generated.</param>
        /// <param name="targetTypeConstructorIL">The IL generator for the target type's constructor.</param>
        /// <param name="options">The configuration object to use.</param>
        public SpanMarshallingWrapper
        (
            [NotNull] ModuleBuilder targetModule,
            [NotNull] TypeBuilder targetType,
            [NotNull] ILGenerator targetTypeConstructorIL,
            ImplementationOptions options
        )
            : base
            (
                targetModule,
                targetType,
                targetTypeConstructorIL,
                options
            )
        {
        }

        /// <inheritdoc />
        public override IntrospectiveMethodInfo GeneratePassthroughDefinition(PipelineWorkUnit<IntrospectiveMethodInfo> workUnit)
        {
            Type returnType = workUnit.Definition.ReturnType, newReturnType;
            var definition = workUnit.Definition;

            if (IsSpanType(returnType))
            {
                var genericType = returnType.GenericTypeArguments[0];

                if (IsOrContainsReferences(genericType))
                {
                    // Span<byte> is used because unbound generics are not allowed inside a nameof, and it still results as just 'Span'
                    throw new NotSupportedException($"Method Return Type is a class or contains references to classes and cannot be marshaled as a {nameof(Span<byte>)}. Marshalling {nameof(Span<byte>)}" +
                                                    $"requires the marshaled type T in {nameof(Span<byte>)}<T> to be a {nameof(ValueType)} without class references.");
                }

                newReturnType = genericType.MakePointerType();
            }
            else
            {
                newReturnType = returnType;
            }

            /* TODO? Add marshaling for Span<> params */

            List<Type> parametersTypes = definition.ParameterTypes.ToList();

            var customAttributes = definition.CustomAttributes.ToList();
            var parameterNames = definition.ParameterNames.ToList();
            var parameterAttributes = definition.ParameterAttributes.ToList();
            var parameterCustomAttributes = definition.ParameterCustomAttributes.ToList();
            Span<int> indices = stackalloc int[definition.ParameterTypes.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            void UpdateIndices(Span<int> ind, int incrementPosition)
            {
                for (int i = 0; i < ind.Length; i++)
                {
                    if (ind[i] >= incrementPosition)
                    {
                        ind[i]++;
                    }
                }
            }

            for (int i = 0; i < definition.ParameterTypes.Count; ++i)
            {
                var paramType = definition.ParameterTypes[i];
                if (IsSpanType(paramType))
                {
                    var genericParam = paramType.GenericTypeArguments[0];

                    if (genericParam.IsGenericType)
                    {
                        throw new NotSupportedException("Generic Type found as Span Generic Argument");
                    }

                    if (IsOrContainsReferences(genericParam))
                    {
                        throw new NotSupportedException("Reference or value type containing references found in Span<T> or ReadOnlySpan<T> generic parameter.");
                    }

                    var currentIndex = indices[i];

                    parametersTypes[currentIndex] = genericParam.MakeByRefType(); // genercParam.MakePointerType();
                    var spanMarshal = GetParameterSpanMarshal(definition.ParameterCustomAttributes[i]);

                    if (spanMarshal != null)
                    {
                        int insertPosition = 0;
                        switch (spanMarshal.LengthParameterDirection)
                        {
                            case LengthParameterDirection.After:
                                insertPosition = currentIndex + 1 + spanMarshal.LengthParameterOffset;
                                break;
                            case LengthParameterDirection.Before:
                                insertPosition = currentIndex - spanMarshal.LengthParameterOffset;
                                break;
                            default:
                                throw new NotSupportedException("Invalid LengthParameterDirection enum value.");
                        }

                        UpdateIndices(indices, insertPosition);

                        if (insertPosition < 0)
                        {
                            throw new InvalidOperationException("Insert position of length parameter must not be negative.");
                        }

                        if (insertPosition > parametersTypes.Count)
                        {
                            throw new InvalidOperationException("Insert position of length parameter must not be bigger than parameter count.");
                        }

                        parametersTypes.Insert(insertPosition, GetSpanMarshalType(spanMarshal));

                        parameterNames.Insert(insertPosition, definition.ParameterNames[i] + "Length");
                        parameterAttributes.Insert(insertPosition, ParameterAttributes.None);
                        var customAttributeData = new InternalCustomAttributeData((short)i);
                        parameterCustomAttributes.Insert(insertPosition, new CustomAttributeData[] { customAttributeData });
                    }
                }
            }

            MethodBuilder passthroughMethod = TargetType.DefineMethod
            (
                $"{workUnit.GetUniqueBaseMemberName()}_wrapped",
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                CallingConventions.Standard,
                newReturnType,
                parametersTypes.ToArray()
            );

            var introspectiveMethod = new IntrospectiveMethodInfo
            (
                passthroughMethod,
                newReturnType,
                parametersTypes,
                definition.MetadataType,
                definition.Attributes,
                definition.ReturnParameterAttributes,
                customAttributes,
                definition.ReturnParameterCustomAttributes,
                parameterNames,
                parameterAttributes,
                parameterCustomAttributes
            );

            passthroughMethod.ApplyCustomAttributesFrom(introspectiveMethod, newReturnType, parametersTypes);

            return introspectiveMethod;
        }

        /// <inheritdoc/>
        public override void EmitPrologue(ILGenerator il, PipelineWorkUnit<IntrospectiveMethodInfo> workUnit, IntrospectiveMethodInfo passthroughMethod)
        {
            var definition = workUnit.Definition;

            var parameterTypes = definition.ParameterTypes;

            il.EmitLoadArgument(0);

            int originalParameterIndex = 0;
            for (short i = 1; i <= Math.Max(parameterTypes.Count, passthroughMethod.ParameterTypes.Count); ++i, ++originalParameterIndex)
            {
                var paramInternalLengthReference = GetInternalLengthReference(passthroughMethod.ParameterCustomAttributes[i - 1]);

                if (paramInternalLengthReference != null)
                {
                    originalParameterIndex--;
                    short index = paramInternalLengthReference.ArgumentIndex;

                    var lengthPropertyGetter = parameterTypes[index].GetProperty(nameof(Span<byte>.Length), BindingFlags.Public | BindingFlags.Instance).GetMethod;
                    il.EmitLoadArgumentAddress((short)(index + 1));
                    il.EmitCallDirect(lengthPropertyGetter);
                }
                else if (IsSpanType(parameterTypes[originalParameterIndex]))
                {
                    var paramType = parameterTypes[originalParameterIndex];
                    Debug.Assert(paramType.GenericTypeArguments.Length == 1, "Span Type does not the correct number of generic parameters (1), CLR/BCL bug?");

                    var pinnedLocal = il.DeclareLocal(paramType.GenericTypeArguments[0].MakeByRefType(), true);

                    var getPinnableReferenceMethod = paramType.GetMethod(nameof(Span<byte>.GetPinnableReference), BindingFlags.Public | BindingFlags.Instance);

                    il.EmitLoadArgumentAddress((short)(originalParameterIndex + 1));
                    il.EmitCallDirect(getPinnableReferenceMethod);
                    il.EmitDuplicate();
                    il.EmitSetLocalVariable(pinnedLocal);
                    // il.EmitConvertToNativeInt();
                }
                else
                {
                    il.EmitLoadArgument(i);
                }
            }
        }

        /// <inheritdoc />
        public override void EmitEpilogue(ILGenerator il, PipelineWorkUnit<IntrospectiveMethodInfo> workUnit, IntrospectiveMethodInfo passthroughMethod)
        {
            Type returnType = workUnit.Definition.ReturnType;

            if (IsSpanType(returnType))
            {
                il.EmitConstantInt(GetNativeCollectionLengthMetadata(workUnit.Definition).Length);
                il.EmitNewObject(returnType.GetConstructor(new[] { typeof(void*), typeof(int) }));
            }
        }

        private NativeCollectionLengthAttribute GetNativeCollectionLengthMetadata(IntrospectiveMethodInfo info)
        {
            IReadOnlyList<CustomAttributeData> attributes = info.ReturnParameterCustomAttributes;

            foreach (CustomAttributeData customAttributeData in attributes)
            {
                if (customAttributeData.AttributeType == typeof(NativeCollectionLengthAttribute))
                {
                    return customAttributeData.ToInstance<NativeCollectionLengthAttribute>();
                }
            }

            throw new InvalidOperationException($"Method return type does not have required {nameof(NativeCollectionLengthAttribute)}");
        }

        /// <inheritdoc />
        public override GeneratorComplexity Complexity => GeneratorComplexity.TransformsParameters | GeneratorComplexity.MemberDependent;

        /// <inheritdoc />
        public override bool IsApplicable(IntrospectiveMethodInfo member)
        {
            return IsSpanType(member.ReturnType) || member.ParameterTypes.Any(IsSpanType);
        }

        private static bool IsSpanType(Type type)
        {
            if (type.IsGenericType)
            {
                var generic = type.GetGenericTypeDefinition();
                return generic == typeof(Span<>) || generic == typeof(ReadOnlySpan<>);
            }

            return false;
        }

        /// <summary>
        /// Gets the parameter's span marshal.
        /// </summary>
        /// <param name="customAttributes">The custom attributes applied to the parameter.</param>
        /// <returns>The <see cref="SpanMarshallingAttribute"/>.</returns>
        [Pure]
        private static SpanMarshallingAttribute GetParameterSpanMarshal([NotNull, ItemNotNull] IEnumerable<CustomAttributeData> customAttributes)
        {
            var paramSpanMarshal = customAttributes.FirstOrDefault
            (
                a =>
                    a.AttributeType == typeof(SpanMarshallingAttribute)
            );

            return paramSpanMarshal?.ToInstance<SpanMarshallingAttribute>();
        }

        /// <summary>
        /// Gets the parameter's internal length reference.
        /// </summary>
        /// <param name="customAttributes">The custom attributes applied to the parameter.</param>
        /// <returns>The <see cref="SpanMarshallingAttribute"/>.</returns>
        [Pure]
        private static InternalLengthReferenceAttribute GetInternalLengthReference([NotNull, ItemNotNull] IEnumerable<CustomAttributeData> customAttributes)
        {
            var paramSpanMarshal = customAttributes.FirstOrDefault
            (
                a =>
                    a.AttributeType == typeof(InternalLengthReferenceAttribute)
            );

            return paramSpanMarshal?.ToInstance<InternalLengthReferenceAttribute>();
        }

        private static Type GetSpanMarshalType(SpanMarshallingAttribute attribute)
        {
            switch (attribute.LengthParameterType)
            {
                case LengthParameterType.SByte:
                    return typeof(sbyte);
                case LengthParameterType.Byte:
                    return typeof(byte);
                case LengthParameterType.Short:
                    return typeof(short);
                case LengthParameterType.UShort:
                    return typeof(ushort);
                case LengthParameterType.Int:
                    return typeof(int);
                case LengthParameterType.UInt:
                    return typeof(uint);
                case LengthParameterType.Long:
                    return typeof(long);
                case LengthParameterType.ULong:
                    return typeof(ulong);
            }

            throw new NotSupportedException("Invalid span marshalling attribute: Invalid length type.");
        }

        private static bool IsOrContainsReferences(Type type)
        {
            if (type.IsPrimitive)
            {
                return false;
            }

            if (type.IsClass)
            {
                return true;
            }

            foreach (var field in type.GetFields())
            {
                if (IsOrContainsReferences(field.FieldType))
                {
                    return true;
                }
            }

            return false;
        }

        private class InternalLengthReferenceAttribute : Attribute
        {
            public InternalLengthReferenceAttribute(short argumentIndex)
            {
                ArgumentIndex = argumentIndex;
            }

            /// <summary>
            /// Gets the index of the argument from which the length gets extracted.
            /// </summary>
            public short ArgumentIndex { get; }
        }

        private class InternalCustomAttributeData : CustomAttributeData
        {
            public InternalCustomAttributeData(short argumentIndex)
            {
                var ctor = typeof(InternalLengthReferenceAttribute).GetConstructor(new[] { typeof(short) });
                if (ctor == null)
                {
                    throw new InvalidProgramException("InternalLengthReferenceAttribute ctor not found.");
                }

                Constructor = ctor;

                ConstructorArguments = new List<CustomAttributeTypedArgument>(new[] { new CustomAttributeTypedArgument(argumentIndex), });
            }

            public override ConstructorInfo Constructor { get; }

            public override IList<CustomAttributeTypedArgument> ConstructorArguments { get; }
        }
    }
}
