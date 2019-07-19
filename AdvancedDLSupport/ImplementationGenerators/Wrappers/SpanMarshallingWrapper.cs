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
            var definition = workUnit.Definition;
            if (IsSpan(workUnit.Definition.ReturnType) &&
                !definition.ReturnType.GenericTypeArguments[0].IsValueType)
            {
                // Span<byte> is used because unbound generics are not allowed inside a nameof, and it still results as just 'Span'
                throw new NotSupportedException($"Method is not a {nameof(ValueType)} and cannot be marshaled as a {nameof(Span<byte>)}. Marshalling {nameof(Span<byte>)}" +
                                                $"requires the marshaled type T in {nameof(Span<byte>)}<T> to be a {nameof(ValueType)}");
            }

            var newReturnType = IsSpan(definition.ReturnType)
                ? definition.ReturnType.GenericTypeArguments[0].MakePointerType()
                : definition.ReturnType;
            var parametersTypes = new Type[definition.ParameterTypes.Count];

            for (var i = 0; i < parametersTypes.Length; i++)
            {
                var param = definition.ParameterTypes[i];
                if (IsSpan(param))
                {
                    if (!param.GenericTypeArguments[0].IsValueType)
                    {
                        throw new NotSupportedException($"{param.Name} is not a {nameof(ValueType)} and cannot be " +
                                                        $"marshaled as a {nameof(Span<byte>)}. Marshalling " +
                                                        $"{nameof(Span<byte>)} requires the marshaled type T in" +
                                                        $"{nameof(Span<byte>)}<T> to be a {nameof(ValueType)}");
                    }

                    parametersTypes[i] = param.GenericTypeArguments[0].MakePointerType();
                }
                else
                {
                    parametersTypes[i] = param;
                }
            }

            var passthroughMethod = TargetType.DefineMethod
            (
                $"{workUnit.GetUniqueBaseMemberName()}_wrapped",
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                CallingConventions.Standard,
                newReturnType,
                parametersTypes
            );

            passthroughMethod.ApplyCustomAttributesFrom(definition, newReturnType, parametersTypes);

            return new IntrospectiveMethodInfo
            (
                passthroughMethod,
                newReturnType,
                parametersTypes,
                definition.MetadataType,
                definition
            );
        }

        /// <inheritdoc />
        public override void EmitPrologue(ILGenerator il, PipelineWorkUnit<IntrospectiveMethodInfo> workUnit)
        {
            il.EmitLoadArgument(0);

            for (short i = 0; i < workUnit.Definition.ParameterTypes.Count; i++)
            {
                if (workUnit.Definition.ParameterTypes[i].IsGenericType)
                {
                    var typeDef = workUnit.Definition.ParameterTypes[i];
                    var genTypeDef = typeDef.GetGenericTypeDefinition();
                    if (genTypeDef == typeof(Span<>))
                    {
                        var method = typeDef.GetMethod("GetPinnableReference");
                        if (method is null)
                        {
                            throw new NotSupportedException(typeDef.Name + " does not contain a GetPinnableReference" +
                                                            " method.");
                        }

                        il.EmitLoadArgument((short)(i + 1));
                        il.EmitCallDirect(method);
                        il.EmitConvertToNativeInt();
                    }
                }
                else
                {
                    il.EmitLoadArgument((short)(i + 1));
                }
            }
        }

        /// <inheritdoc />
        public override void EmitEpilogue(ILGenerator il, PipelineWorkUnit<IntrospectiveMethodInfo> workUnit)
        {
            if (IsSpan(workUnit.Definition.ReturnType))
            {
                var ctor = workUnit.Definition.ReturnType.GetConstructor(new[] { typeof(void*), typeof(int) });

                il.EmitConstantInt(GetNativeCollectionLengthMetadata(workUnit.Definition).Length);
                il.EmitNewObject(ctor);
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
            return IsSpan(member.ReturnType) || member.ParameterTypes.Any(IsSpan);
        }

        private bool IsSpan(Type type)
        {
            return type.IsGenericType // prevents exception on the line below
                   && type.GetGenericTypeDefinition() == typeof(Span<>);
        }
    }
}
