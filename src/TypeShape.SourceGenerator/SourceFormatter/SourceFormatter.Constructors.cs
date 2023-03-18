using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatConstructorFactory(SourceWriter writer, string methodName, TypeModel type)
    {
        Debug.Assert(type.Constructors.Count > 0);

        writer.WriteLine($"private global::System.Collections.Generic.IEnumerable<global::TypeShape.IConstructor> {methodName}()");
        writer.WriteStartBlock();

        int i = 0;
        foreach (ConstructorModel constructor in type.Constructors)
        {
            if (i > 0)
                writer.WriteLine();

            writer.WriteLine($$"""
                yield return new global::TypeShape.SourceGenModel.SourceGenConstructor<{{type.Id.FullyQualifiedName}}, {{constructor.ConstructorArgumentStateFQN}}>
                {
                    DeclaringType = {{constructor.DeclaringType.GeneratedPropertyName}},
                    ParameterCount = {{constructor.Parameters.Count}},
                    GetParametersFunc = {{(constructor.Parameters.Count == 0 ? "null" : FormatConstructorParameterFactoryName(type, i))}},
                    DefaultConstructorFunc = {{FormatDefaultCtor(constructor)}},
                    ArgumentStateConstructorFunc = static () => {{FormatArgumentStateCtorExpr(constructor)}},
                    ParameterizedConstructorFunc = static state => new {{type.Id.FullyQualifiedName}}({{FormatParameterizedCtorArgumentsExpr(constructor)}}),
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(constructor)}},
                };
                """);

            i++;

            static string FormatAttributeProviderFunc(ConstructorModel constructor)
            {
                string parameterTypes = constructor.Parameters.Count == 0
                    ? "Array.Empty<Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})";
            }

            static string FormatArgumentStateCtorExpr(ConstructorModel constructor)
                => constructor.Parameters.Count switch
                {
                    0 => "null",
                    1 => FormatDefaultValueExpr(constructor.Parameters[0]),
                    _ => $"({string.Join(", ", constructor.Parameters.Select(FormatDefaultValueExpr))})"
                };

            static string FormatParameterizedCtorArgumentsExpr(ConstructorModel constructor)
                => constructor.Parameters.Count switch
                {
                    0 => "",
                    1 => "state",
                    _ => string.Join(", ", constructor.Parameters.Select(p => $"state.Item{p.Position + 1}")),
                };

            static string FormatDefaultCtor(ConstructorModel constructor)
                => constructor.Parameters.Count switch
                {
                    0 => $"static () => new {constructor.DeclaringType.FullyQualifiedName}()",
                    _ => "null",
                };
        }

        writer.WriteEndBlock();

        i = 0;
        foreach (ConstructorModel constructor in type.Constructors)
        {
            if (constructor.Parameters.Count > 0)
            {
                writer.WriteLine();
                FormatConstructorParameterFactory(writer, FormatConstructorParameterFactoryName(type, i), constructor);
            }

            i++;
        }

        static string FormatConstructorParameterFactoryName(TypeModel type, int constructorIndex) =>
            $"CreateConstructorParameters_{type.Id.GeneratedPropertyName}_{constructorIndex}";
    }

    private static void FormatConstructorParameterFactory(SourceWriter writer, string methodName, ConstructorModel constructor)
    {
        writer.WriteLine($"private global::System.Collections.Generic.IEnumerable<global::TypeShape.IConstructorParameter> {methodName}()");
        writer.WriteStartBlock();

        int i = 0;
        foreach (ConstructorParameterModel parameter in constructor.Parameters)
        {
            if (i > 0)
                writer.WriteLine();

            writer.WriteLine($$"""
                yield return new global::TypeShape.SourceGenModel.SourceGenConstructorParameter<{{constructor.ConstructorArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
                {
                    Position = {{parameter.Position}},
                    Name = "{{parameter.Name}}",
                    ParameterType = {{parameter.ParameterType.GeneratedPropertyName}},
                    HasDefaultValue = {{FormatBool(parameter.HasDefaultValue)}},
                    DefaultValue = {{FormatDefaultValueExpr(parameter)}},
                    Setter = static (ref {{constructor.ConstructorArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => state = {{FormatSetterBody(constructor, parameter)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(constructor, parameter)}},
                };
                """);

            i++;

            static string FormatAttributeProviderFunc(ConstructorModel constructor, ConstructorParameterModel parameter)
            {
                string parameterTypes = constructor.Parameters.Count == 0
                    ? "Array.Empty<Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})?.GetParameters()[{parameter.Position}]";
            }

            static string FormatSetterBody(ConstructorModel constructor, ConstructorParameterModel parameter)
                => constructor.Parameters.Count switch
                {
                    1 => "value",
                    _ => $"({string.Join(", ", constructor.Parameters.Select(p => p.Position == parameter.Position ? "value" : $"state.Item{p.Position + 1}"))})",
                };
        }

        writer.WriteEndBlock();
    }

    private static string FormatDefaultValueExpr(ConstructorParameterModel constructorParameter)
    {
        if (!constructorParameter.HasDefaultValue)
        {
            return $"default({constructorParameter.ParameterType.FullyQualifiedName})";
        }

        string literalExpr = constructorParameter.DefaultValue switch
        {
            null => "null",
            false => "false",
            true => "true",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            float f => $"{f}f",
            decimal d => $"{d}M",
            object val => val.ToString(),
        };

        return constructorParameter.DefaultValueRequiresCast 
            ? $"({constructorParameter.ParameterType.FullyQualifiedName}){literalExpr}" 
            : literalExpr;
    }
}
