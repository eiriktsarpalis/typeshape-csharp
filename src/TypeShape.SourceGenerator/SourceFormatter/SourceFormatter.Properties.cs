using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatPropertyFactory(SourceWriter writer, string methodName, TypeModel type)
    {
        Debug.Assert(type.Properties.Length > 0);

        writer.WriteLine($"private IEnumerable<IPropertyShape> {methodName}() => new IPropertyShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (PropertyModel property in type.Properties)
        {
            if (i++ > 0)
                writer.WriteLine();

            // Suppress property getters that are nullable, but are not Nullable<T>
            bool suppressGetter = property is
            {
                PropertyType.SpecialType: not SpecialType.System_Nullable_T,
                IsGetterNonNullable: false
            };

            // Suppress non-nullable Nullable<T> property setters (i.e. setters with [DisallowNull] annotation)
            bool suppressSetter = property is 
            { 
                PropertyType.SpecialType: SpecialType.System_Nullable_T,
                IsSetterNonNullable: true,
            };

            writer.WriteLine($$"""
                new SourceGenPropertyShape<{{type.Id.FullyQualifiedName}}, {{property.PropertyType.FullyQualifiedName}}>
                {
                    Name = "{{property.Name}}",
                    DeclaringType = {{type.Id.GeneratedPropertyName}},
                    PropertyType = {{property.PropertyType.GeneratedPropertyName}},
                    Getter = {{(property.EmitGetter ? $"static (ref {type.Id.FullyQualifiedName} obj) => obj.{property.UnderlyingMemberName}{(suppressGetter ? "!" : "")}" : "null")}},
                    Setter = {{(property.EmitSetter ? $"static (ref {type.Id.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value) => obj.{property.UnderlyingMemberName} = value{(suppressSetter ? "!" : "")}" : "null")}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, property)}},
                    IsField = {{FormatBool(property.IsField)}},
                    IsGetterNonNullable = {{FormatBool(property.IsGetterNonNullable)}},
                    IsSetterNonNullable = {{FormatBool(property.IsSetterNonNullable)}},
                },
                """, trimNullAssignmentLines: true);

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
        writer.WriteLine("};");
    }
}
