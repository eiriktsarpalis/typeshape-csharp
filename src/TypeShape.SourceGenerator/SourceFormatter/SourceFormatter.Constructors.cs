using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Text;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private const string BitArrayFQN = "global::System.Collections.BitArray";

    private static void FormatConstructorFactory(SourceWriter writer, string methodName, ObjectShapeModel type)
    {
        writer.WriteLine($"private IEnumerable<IConstructorShape> {methodName}() => new IConstructorShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        var argumentStateFQNs = new List<string>();
        foreach (ConstructorShapeModel constructor in type.Constructors)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            string constructorArgumentStateFQN = FormatConstructorArgumentStateFQN(type, constructor);
            argumentStateFQNs.Add(constructorArgumentStateFQN);

            writer.WriteLine($$"""
                new SourceGenConstructorShape<{{type.Type.FullyQualifiedName}}, {{constructorArgumentStateFQN}}>
                {
                    DeclaringType = (IObjectTypeShape<{{type.Type.FullyQualifiedName}}>){{type.Type.GeneratedPropertyName}},
                    ParameterCount = {{constructor.TotalArity}},
                    GetParametersFunc = {{(constructor.TotalArity == 0 ? "null" : FormatConstructorParameterFactoryName(type, i))}},
                    DefaultConstructorFunc = {{FormatDefaultCtor(type, constructor)}},
                    ArgumentStateConstructorFunc = {{FormatArgumentStateCtor(constructor, constructorArgumentStateFQN)}},
                    ParameterizedConstructorFunc = {{FormatParameterizedCtor(type, constructor, constructorArgumentStateFQN)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, constructor)}},
                    IsPublic = {{FormatBool(constructor.IsPublic)}},
                },
                """, trimNullAssignmentLines: true);

            i++;

            static string FormatAttributeProviderFunc(ObjectShapeModel type, ConstructorShapeModel constructor)
            {
                if (type.IsTupleType || constructor.IsStaticFactory)
                {
                    return "null";
                }

                string parameterTypes = constructor.Parameters.Length == 0
                    ? "Array.Empty<Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})";
            }

            static string FormatArgumentStateCtor(ConstructorShapeModel constructor, string constructorArgumentStateFQN)
            {
                if (constructor.TotalArity == 0)
                {
                    return "null";
                }
                
                string argumentCtorExpr = (constructor.Parameters.Length, constructor.RequiredOrInitMembers.Length, constructor.OptionalMemberFlagsType) switch
                {
                    (0, 0, OptionalMemberFlagsType.None) => "null!",
                    (1, 0, OptionalMemberFlagsType.None) => FormatDefaultValueExpr(constructor.Parameters[0]),
                    (0, 1, OptionalMemberFlagsType.None) => FormatDefaultValueExpr(constructor.RequiredOrInitMembers[0]),
                    (_, _, not OptionalMemberFlagsType.BitArray) when (!constructor.Parameters.Any(p => p.HasDefaultValue)) => $"default({constructorArgumentStateFQN})",
                    (_, _, OptionalMemberFlagsType.None) => 
                        FormatTupleConstructor(
                            constructor.Parameters
                            .Concat(constructor.RequiredOrInitMembers)
                            .Select(FormatDefaultValueExpr)),
                    (_, _, OptionalMemberFlagsType flagType) =>
                        FormatTupleConstructor(
                            constructor.Parameters
                            .Concat(constructor.RequiredOrInitMembers)
                            .Concat(constructor.OptionalMembers)
                            .Select(FormatDefaultValueExpr)
                            .Append(
                                flagType is OptionalMemberFlagsType.BitArray 
                                ? $"new {BitArrayFQN}({constructor.OptionalMembers.Length})"
                                : "default")),
                };

                return $"static () => {argumentCtorExpr}";
                static string FormatTupleConstructor(IEnumerable<string> elementValues)
                    => $"({string.Join(", ", elementValues)})";
            }

            static string FormatParameterizedCtor(ObjectShapeModel type, ConstructorShapeModel constructor, string constructorArgumentStateFQN)
            {
                if (constructor.TotalArity == 0)
                {
                    return "null";
                }
                
                return $"static (ref {constructorArgumentStateFQN} state) => {FormatParameterizedCtorExpr(type, constructor, "state")}";
                static string FormatParameterizedCtorExpr(ObjectShapeModel type, ConstructorShapeModel constructor, string stateVar)
                {
                    if (type.IsValueTupleType)
                    {
                        return constructor.TotalArity switch
                        {
                            0 => $"default({type.Type.FullyQualifiedName})",
                            1 => $"new ({stateVar})",
                            _ => stateVar,
                        };
                    }

                    if (type.IsTupleType)
                    {
                        Debug.Assert(constructor.Parameters.Length > 0);
                        Debug.Assert(constructor.RequiredOrInitMembers.Length == 0);

                        if (constructor.Parameters.Length == 1)
                        {
                            return $"new ({stateVar})";
                        }

                        var sb = new StringBuilder();
                        int indentation = 0;
                        for (int i = 0; i < constructor.Parameters.Length; i++)
                        {
                            if (i % 7 == 0)
                            {
                                sb.Append("new (");
                                indentation++;
                            }

                            sb.Append($"{FormatCtorParameterExpr(constructor.Parameters[i])}, ");
                        }

                        sb.Length -= 2;
                        sb.Append(')', indentation);
                        return sb.ToString();
                    }

                    string objectInitializerExpr = (constructor.Parameters.Length, constructor.RequiredOrInitMembers.Length) switch
                    {
                        (0, 0) => $$"""{{FormatConstructorName(constructor)}}()""",
                        (1, 0) when constructor.OptionalMembers is [] => $$"""{{FormatConstructorName(constructor)}}({{stateVar}})""",
                        (0, 1) when constructor.OptionalMembers is [] => $$"""{{FormatConstructorName(constructor)}} { {{constructor.RequiredOrInitMembers[0].UnderlyingMemberName}} = {{stateVar}} }""",
                        (_, 0) => $$"""{{FormatConstructorName(constructor)}}({{FormatCtorArgumentsBody()}})""",
                        (0, _) => $$"""{{FormatConstructorName(constructor)}} { {{FormatInitializerBody()}} }""",
                        (_, _) => $$"""{{FormatConstructorName(constructor)}}({{FormatCtorArgumentsBody()}}) { {{FormatInitializerBody()}} }""",
                    };

                    return constructor.OptionalMembers.Length == 0
                        ? objectInitializerExpr
                        : $$"""{ var obj = {{objectInitializerExpr}}; {{FormatOptionalMemberAssignments()}}; return obj; }""";

                    string FormatCtorArgumentsBody() => string.Join(", ", constructor.Parameters.Select(p => FormatCtorParameterExpr(p)));
                    string FormatInitializerBody() => string.Join(", ", constructor.RequiredOrInitMembers.Select(p => $"{p.UnderlyingMemberName} = {FormatCtorParameterExpr(p)}"));
                    string FormatOptionalMemberAssignments() => string.Join("; ", constructor.OptionalMembers.Select(FormatOptionalMemberAssignment));
                    string FormatOptionalMemberAssignment(ConstructorParameterShapeModel parameter)
                    {
                        Debug.Assert(parameter.Kind is ParameterKind.OptionalMember);
                        int flagOffset = parameter.Position - constructor.Parameters.Length - constructor.RequiredOrInitMembers.Length;
                        Debug.Assert(flagOffset >= 0);
                        string conditionalExpr = constructor.OptionalMemberFlagsType is OptionalMemberFlagsType.BitArray
                            ? $"{stateVar}.Item{constructor.TotalArity + 1}[{flagOffset}]"
                            : $"({stateVar}.Item{constructor.TotalArity + 1} & (1 << {flagOffset})) != 0";

                        return $"if ({conditionalExpr}) obj.{parameter.UnderlyingMemberName} = {FormatCtorParameterExpr(parameter)}";
                    }

                    string FormatCtorParameterExpr(ConstructorParameterShapeModel parameter)
                    {
                        // Reserved for cases where we have Nullable<T> ctor parameters with [DisallowNull] annotation.
                        bool requiresSuppression = parameter is
                        {
                            ParameterType.SpecialType: SpecialType.System_Nullable_T,
                            IsNonNullable: true
                        };

                        return $"{stateVar}.Item{parameter.Position + 1}{(requiresSuppression ? "!" : "")}";
                    }
                }
            }

            static string FormatDefaultCtor(ObjectShapeModel type, ConstructorShapeModel constructor)
                => constructor.TotalArity switch
                {
                    0 when type.IsValueTupleType => $"static () => default({type.Type.FullyQualifiedName})",
                    0 => $"static () => {FormatConstructorName(constructor)}()",
                    _ => "null",
                };

            static string FormatConstructorName(ConstructorShapeModel constructor)
                => constructor.StaticFactoryName ?? $"new {constructor.DeclaringType.FullyQualifiedName}";
        }

        writer.Indentation--;
        writer.WriteLine("};");

        i = 0;
        foreach (ConstructorShapeModel constructor in type.Constructors)
        {
            if (constructor.TotalArity > 0)
            {
                writer.WriteLine();
                FormatConstructorParameterFactory(writer, type, FormatConstructorParameterFactoryName(type, i), constructor, argumentStateFQNs[i]);
            }

            i++;
        }

        static string FormatConstructorParameterFactoryName(TypeShapeModel type, int constructorIndex) =>
            $"CreateConstructorParameters_{type.Type.GeneratedPropertyName}_{constructorIndex}";
    }

    private static void FormatConstructorParameterFactory(SourceWriter writer, ObjectShapeModel type, string methodName, ConstructorShapeModel constructor, string constructorArgumentStateFQN)
    {
        writer.WriteLine($"private IEnumerable<IConstructorParameterShape> {methodName}() => new IConstructorParameterShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (ConstructorParameterShapeModel parameter in constructor.Parameters
                                                            .Concat(constructor.RequiredOrInitMembers)
                                                            .Concat(constructor.OptionalMembers))
        {
            if (i > 0)
                writer.WriteLine();

            writer.WriteLine($$"""
                new SourceGenConstructorParameterShape<{{constructorArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
                {
                    Position = {{parameter.Position}},
                    Name = {{FormatStringLiteral(parameter.Name)}},
                    ParameterType = {{parameter.ParameterType.GeneratedPropertyName}},
                    Kind = {{FormatParameterKind(parameter)}},
                    IsRequired = {{FormatBool(parameter.IsRequired)}},
                    IsNonNullable = {{FormatBool(parameter.IsNonNullable)}},
                    IsPublic = {{FormatBool(parameter.IsPublic)}},
                    HasDefaultValue = {{FormatBool(parameter.HasDefaultValue)}},
                    DefaultValue = {{FormatDefaultValueExpr(parameter)}},
                    Setter = static (ref {{constructorArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => {{FormatSetterBody(constructor, parameter)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, constructor, parameter)}},
                },
                """, trimNullAssignmentLines: true);

            i++;

            static string FormatAttributeProviderFunc(ObjectShapeModel type, ConstructorShapeModel constructor, ConstructorParameterShapeModel parameter)
            {
                if (type.IsTupleType || constructor.IsStaticFactory)
                {
                    return "null";
                }

                if (parameter.Kind is not ParameterKind.ConstructorParameter)
                {
                    return $"static () => typeof({parameter.DeclaringType.FullyQualifiedName}).GetMember({FormatStringLiteral(parameter.UnderlyingMemberName)}, {InstanceBindingFlagsConstMember})[0]";
                }

                string parameterTypes = constructor.Parameters.Length == 0
                    ? "Array.Empty<Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})?.GetParameters()[{parameter.Position}]";
            }

            static string FormatSetterBody(ConstructorShapeModel constructor, ConstructorParameterShapeModel parameter)
            {
                string assignValueExpr = constructor.TotalArity switch
                {
                    1 when constructor.OptionalMembers.Length == 0 => "state = value",
                    _ => $"state.Item{parameter.Position + 1} = value",
                };

                if (parameter.Kind is ParameterKind.OptionalMember)
                {
                    int flagOffset = parameter.Position - constructor.Parameters.Length - constructor.RequiredOrInitMembers.Length;
                    Debug.Assert(flagOffset >= 0);
                    string setFlagExpr = constructor.OptionalMemberFlagsType is OptionalMemberFlagsType.BitArray
                        ? $"state.Item{constructor.TotalArity + 1}[{flagOffset}] = true"
                        : $"state.Item{constructor.TotalArity + 1} |= 1 << {flagOffset}";

                    return $$"""{ {{assignValueExpr}}; {{setFlagExpr}}; }""";
                }

                return assignValueExpr;
            }

            static string FormatParameterKind(ConstructorParameterShapeModel parameter)
            {
                string identifier = parameter.Kind switch
                {
                    ParameterKind.ConstructorParameter => "ConstructorParameter",
                    ParameterKind.RequiredOrInitOnlyMember or
                    ParameterKind.OptionalMember => parameter.IsField ? "FieldInitializer" : "PropertyInitializer",
                    _ => throw new InvalidOperationException($"Unsupported parameter kind: {parameter.Kind}"),
                };

                return $"ConstructorParameterKind.{identifier}";
            }
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }

    private static string FormatDefaultValueExpr(ConstructorParameterShapeModel constructorParameter)
    {
        return constructorParameter switch
        {
            { DefaultValueExpr: string defaultValueExpr } => defaultValueExpr,
            { ParameterType.IsValueType: true } => "default",
            _ => "default!",
        };
    }

    private static string FormatConstructorArgumentStateFQN(ObjectShapeModel type, ConstructorShapeModel constructorModel)
    {
        if (type.IsValueTupleType && constructorModel.TotalArity > 1)
        {
            // For tuple types, just use the type as the argument state.
            return constructorModel.DeclaringType.FullyQualifiedName;
        }

        string? optionalParameterFlagTypeFQN = constructorModel.OptionalMemberFlagsType switch
        {
            OptionalMemberFlagsType.Byte => "byte",
            OptionalMemberFlagsType.UShort => "ushort",
            OptionalMemberFlagsType.UInt32 => "uint",
            OptionalMemberFlagsType.ULong => "ulong",
            OptionalMemberFlagsType.BitArray => BitArrayFQN,
            _ => null,
        };

        return (constructorModel.Parameters.Length, constructorModel.RequiredOrInitMembers.Length, optionalParameterFlagTypeFQN) switch
        {
            (0, 0, null) => "object?",
            (1, 0, null) => constructorModel.Parameters[0].ParameterType.FullyQualifiedName,
            (0, 1, null) => constructorModel.RequiredOrInitMembers[0].ParameterType.FullyQualifiedName,
            (_, _, null) => FormatTupleType(
                constructorModel.Parameters
                .Concat(constructorModel.RequiredOrInitMembers)
                .Select(p => p.ParameterType.FullyQualifiedName)),

            (_, _, { }) =>
                FormatTupleType(
                    constructorModel.Parameters
                    .Concat(constructorModel.RequiredOrInitMembers)
                    .Concat(constructorModel.OptionalMembers)
                    .Select(p => p.ParameterType.FullyQualifiedName)
                    .Append(optionalParameterFlagTypeFQN))
        };

        static string FormatTupleType(IEnumerable<string> parameterTypes)
            => $"({string.Join(", ", parameterTypes)})";
    }
}
