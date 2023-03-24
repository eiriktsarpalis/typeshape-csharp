using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatPropertyFactory(SourceWriter writer, string methodName, TypeModel type)
    {
        Debug.Assert(type.Properties.Count > 0);

        writer.WriteLine($"private global::System.Collections.Generic.IEnumerable<global::TypeShape.IProperty> {methodName}()");
        writer.WriteStartBlock();

        int i = 0;
        foreach (PropertyModel property in type.Properties)
        {
            if (i++ > 0)
                writer.WriteLine();

            writer.WriteLine($$"""
                yield return new global::TypeShape.SourceGenModel.SourceGenProperty<{{type.Id.FullyQualifiedName}}, {{property.PropertyType.FullyQualifiedName}}>
                {
                    Name = "{{property.Name}}",
                    DeclaringType = {{type.Id.GeneratedPropertyName}},
                    PropertyType = {{property.PropertyType.GeneratedPropertyName}},
                    Getter = {{(property.EmitGetter ? $"static (ref {type.Id.FullyQualifiedName} obj) => obj.{property.Name}" : "null")}},
                    Setter = {{(property.EmitSetter ? $"static (ref {type.Id.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value) => obj.{property.Name} = value" : "null")}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, property)}},
                    IsField = {{FormatBool(property.IsField)}},
                };
                """);

            static string FormatAttributeProviderFunc(TypeModel type, PropertyModel property)
            {
                if (type.IsTupleType)
                {
                    return "null";
                }

                TypeId declaringType = property.DeclaringInterfaceType ?? property.DeclaringType;
                return property.IsField
                    ? $"static () => typeof({declaringType.FullyQualifiedName}).GetField(\"{property.Name}\", {InstanceBindingFlagsConstMember})"
                    : $"static () => typeof({declaringType.FullyQualifiedName}).GetProperty(\"{property.Name}\", {InstanceBindingFlagsConstMember})";
            }
        }

        writer.WriteEndBlock();

    }
}
