using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatConstructorFactory(SourceWriter writer, string methodName, TypeModel type)
    {
        Debug.Assert(type.Constructors.Length > 0);

        writer.WriteLine($"private IEnumerable<IConstructorShape> {methodName}() => new IConstructorShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        var argumentStateFQNs = new List<string>();
        foreach (ConstructorModel constructor in type.Constructors)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            string constructorArgumentStateFQN = FormatConstructorArgumentStateFQN(type, constructor);
            argumentStateFQNs.Add(constructorArgumentStateFQN);

            writer.WriteLine($$"""
                new SourceGenConstructorShape<{{type.Id.FullyQualifiedName}}, {{constructorArgumentStateFQN}}>
                {
                    DeclaringType = {{type.Id.GeneratedPropertyName}},
                    ParameterCount = {{constructor.TotalArity}},
                    GetParametersFunc = {{(constructor.TotalArity == 0 ? "null" : FormatConstructorParameterFactoryName(type, i))}},
                    DefaultConstructorFunc = {{FormatDefaultCtor(type, constructor)}},
                    ArgumentStateConstructorFunc = static () => {{FormatArgumentStateCtorExpr(constructor, constructorArgumentStateFQN)}},
                    ParameterizedConstructorFunc = static (in {{constructorArgumentStateFQN}} state) => {{FormatParameterizedCtorExpr(type, constructor, "state")}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, constructor)}},
                },
                """);

            i++;

            static string FormatAttributeProviderFunc(TypeModel type, ConstructorModel constructor)
            {
                if (type.IsValueTupleType || type.IsClassTupleType || constructor.IsStaticFactory)
                {
                    return "null";
                }

                string parameterTypes = constructor.Parameters.Length == 0
                    ? "Array.Empty<Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})";
            }

            static string FormatArgumentStateCtorExpr(ConstructorModel constructor, string constructorArgumentStateFQN)
                => (constructor.Parameters.Length, constructor.MemberInitializers.Length) switch
                {
                    (0, 0) => "null!",
                    (1, 0) => FormatDefaultValueExpr(constructor.Parameters[0]),
                    (0, 1) => FormatDefaultValueExpr(constructor.MemberInitializers[0]),
                    _ when (!constructor.Parameters.Any(p => p.HasDefaultValue)) => $"default({constructorArgumentStateFQN})",
                    _ => $"({string.Join(", ", constructor.Parameters.Concat(constructor.MemberInitializers).Select(FormatDefaultValueExpr))})"
                };

            static string FormatParameterizedCtorExpr(TypeModel type, ConstructorModel constructor, string stateVar)
            {
                if (type.IsValueTupleType)
                {
                    return constructor.TotalArity switch
                    {
                        0 => $"default({type.Id.FullyQualifiedName})",
                        1 => $"new ({stateVar})",
                        _ => stateVar,
                    };
                }

                if (type.IsClassTupleType)
                {
                    Debug.Assert(constructor.Parameters.Length > 0);
                    Debug.Assert(constructor.MemberInitializers.Length == 0);

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

                return (constructor.Parameters.Length, constructor.MemberInitializers.Length) switch
                {
                    (0, 0) => $$"""{{FormatConstructorName(constructor)}}()""",
                    (1, 0) => $$"""{{FormatConstructorName(constructor)}}({{stateVar}})""",
                    (0, 1) => $$"""{{FormatConstructorName(constructor)}} { {{constructor.MemberInitializers[0].Name}} = {{stateVar}} }""",
                    (_, 0) => $$"""{{FormatConstructorName(constructor)}}({{FormatCtorArgumentsBody()}})""",
                    (0, _) => $$"""{{FormatConstructorName(constructor)}} { {{FormatInitializerBody()}} }""",
                    (_, _) => $$"""{{FormatConstructorName(constructor)}}({{FormatCtorArgumentsBody()}}) { {{FormatInitializerBody()}} }""",
                };

                string FormatCtorArgumentsBody() => string.Join(", ", constructor.Parameters.Select(p => FormatCtorParameterExpr(p)));
                string FormatInitializerBody() => string.Join(", ", constructor.MemberInitializers.Select(p => $"{p.Name} = {FormatCtorParameterExpr(p)}"));
                string FormatCtorParameterExpr(ConstructorParameterModel parameter)
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

            static string FormatDefaultCtor(TypeModel type, ConstructorModel constructor)
                => constructor.TotalArity switch
                {
                    0 when (type.IsValueTupleType) => $"static () => default({type.Id.FullyQualifiedName})",
                    0 => $"static () => {FormatConstructorName(constructor)}()",
                    _ => "null",
                };

            static string FormatConstructorName(ConstructorModel constructor)
                => constructor.StaticFactoryName ?? $"new {constructor.DeclaringType.FullyQualifiedName}";
        }

        writer.Indentation--;
        writer.WriteLine("};");

        i = 0;
        foreach (ConstructorModel constructor in type.Constructors)
        {
            if (constructor.TotalArity > 0)
            {
                writer.WriteLine();
                FormatConstructorParameterFactory(writer, type, FormatConstructorParameterFactoryName(type, i), constructor, argumentStateFQNs[i]);
            }

            i++;
        }

        static string FormatConstructorParameterFactoryName(TypeModel type, int constructorIndex) =>
            $"CreateConstructorParameters_{type.Id.GeneratedPropertyName}_{constructorIndex}";
    }

    private static void FormatConstructorParameterFactory(SourceWriter writer, TypeModel type, string methodName, ConstructorModel constructor, string constructorArgumentStateFQN)
    {
        writer.WriteLine($"private IEnumerable<IConstructorParameterShape> {methodName}() => new IConstructorParameterShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (ConstructorParameterModel parameter in constructor.Parameters.Concat(constructor.MemberInitializers))
        {
            if (i > 0)
                writer.WriteLine();

            writer.WriteLine($$"""
                new SourceGenConstructorParameterShape<{{constructorArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
                {
                    Position = {{parameter.Position}},
                    Name = "{{parameter.Name}}",
                    ParameterType = {{parameter.ParameterType.GeneratedPropertyName}},
                    IsRequired = {{FormatBool(parameter.IsRequired)}},
                    IsNonNullable = {{FormatBool(parameter.IsNonNullable)}},
                    HasDefaultValue = {{FormatBool(parameter.HasDefaultValue)}},
                    DefaultValue = {{FormatDefaultValueExpr(parameter)}},
                    Setter = static (ref {{constructorArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => {{FormatSetterBody(constructor, parameter)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, constructor, parameter)}},
                },
                """);

            i++;

            static string FormatAttributeProviderFunc(TypeModel type, ConstructorModel constructor, ConstructorParameterModel parameter)
            {
                if (type.IsValueTupleType || type.IsClassTupleType || constructor.IsStaticFactory)
                {
                    return "null";
                }

                if (parameter.IsMemberInitializer)
                {
                    return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetMember({FormatStringLiteral(parameter.Name)}, {InstanceBindingFlagsConstMember})[0]";
                }

                string parameterTypes = constructor.Parameters.Length == 0
                    ? "Array.Empty<Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})?.GetParameters()[{parameter.Position}]";
            }

            static string FormatSetterBody(ConstructorModel constructor, ConstructorParameterModel parameter)
                => constructor.TotalArity switch
                {
                    1 => "state = value",
                    _ => $"state.Item{parameter.Position + 1} = value",
                };
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }

    private static string FormatDefaultValueExpr(ConstructorParameterModel constructorParameter)
    {
        if (!constructorParameter.HasDefaultValue)
        {
            return $"default({constructorParameter.ParameterType.FullyQualifiedName}){(constructorParameter.ParameterType.IsValueType ? "" : "!")}";
        }

        string literalExpr = constructorParameter.DefaultValue switch
        {
            null => "null!",
            false => "false",
            true => "true",

            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),

            double.NaN => "double.NaN",
            double.NegativeInfinity => "double.NegativeInfinity",
            double.PositiveInfinity => "double.PositiveInfinity",
            double d => $"{d.ToString("G17", CultureInfo.InvariantCulture)}d",

            float.NaN => "float.NaN",
            float.NegativeInfinity => "float.NegativeInfinity",
            float.PositiveInfinity => "float.PositiveInfinity",
            float f => $"{f.ToString("G9", CultureInfo.InvariantCulture)}f",

            decimal d => $"{d.ToString(CultureInfo.InvariantCulture)}m",

            // Must be one of the other numeric types or an enum
            object num => Convert.ToString(num, CultureInfo.InvariantCulture),
        };

        return constructorParameter.DefaultValueRequiresCast 
            ? $"({constructorParameter.ParameterType.FullyQualifiedName}){literalExpr}" 
            : literalExpr;
    }

    private static string FormatConstructorArgumentStateFQN(TypeModel type, ConstructorModel constructorModel)
    {
        if (type.IsValueTupleType && constructorModel.TotalArity > 1)
        {
            // For tuple types, just use the type as the argument state.
            return constructorModel.DeclaringType.FullyQualifiedName;
        }

        return (constructorModel.Parameters.Length, constructorModel.MemberInitializers.Length) switch
        {
            (0, 0) => "object?",
            (1, 0) => constructorModel.Parameters[0].ParameterType.FullyQualifiedName,
            (0, 1) => constructorModel.MemberInitializers[0].ParameterType.FullyQualifiedName,
            _ => $"({string.Join(", ", constructorModel.Parameters.Concat(constructorModel.MemberInitializers).Select(p => p.ParameterType.FullyQualifiedName))})",
        };
    }
}
