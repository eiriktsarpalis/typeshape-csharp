using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatPropertyFactory(SourceWriter writer, string methodName, TypeModel type)
    {
        Debug.Assert(type.Properties.Count > 0);

        writer.WriteLine($"private global::System.Collections.Generic.IEnumerable<global::TypeShape.IPropertyShape> {methodName}()");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (PropertyModel property in type.Properties)
        {
            if (i++ > 0)
                writer.WriteLine();

            bool isNullableGetter = property.GetterNullableAnnotation is NullableAnnotation.Annotated;
            writer.WriteLine($$"""
                yield return new global::TypeShape.SourceGenModel.SourceGenPropertyShape<{{type.Id.FullyQualifiedName}}, {{property.PropertyType.FullyQualifiedName}}>
                {
                    Name = "{{property.Name}}",
                    DeclaringType = {{type.Id.GeneratedPropertyName}},
                    PropertyType = {{property.PropertyType.GeneratedPropertyName}},
                    Getter = {{(property.EmitGetter ? $"static (ref {type.Id.FullyQualifiedName} obj) => obj.{property.UnderlyingMemberName}{(isNullableGetter ? "!" : "")}" : "null")}},
                    Setter = {{(property.EmitSetter ? $"static (ref {type.Id.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value) => obj.{property.UnderlyingMemberName} = value" : "null")}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, property)}},
                    IsField = {{FormatBool(property.IsField)}},
                    IsGetterNonNullableReferenceType = {{FormatBool(property.GetterNullableAnnotation is NullableAnnotation.NotAnnotated)}},
                    IsSetterNonNullableReferenceType = {{FormatBool(property.SetterNullableAnnotation is NullableAnnotation.NotAnnotated)}},
                };
                """);

            static string FormatAttributeProviderFunc(TypeModel type, PropertyModel property)
            {
                if (type.IsValueTupleType || type.IsClassTupleType)
                {
                    return "null";
                }

                TypeId declaringType = property.DeclaringInterfaceType ?? property.DeclaringType;
                return property.IsField
                    ? $"static () => typeof({declaringType.FullyQualifiedName}).GetField({FormatStringLiteral(property.Name)}, {InstanceBindingFlagsConstMember})"
                    : $"static () => typeof({declaringType.FullyQualifiedName}).GetProperty({FormatStringLiteral(property.Name)}, {InstanceBindingFlagsConstMember})";
            }
        }

        writer.Indentation--;
        writer.WriteLine('}');
    }
}
