using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Reflection;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatPropertyFactory(SourceWriter writer, string methodName, ObjectShapeModel type)
    {
        writer.WriteLine($"private global::TypeShape.Abstractions.IPropertyShape[] {methodName}() => new global::TypeShape.Abstractions.IPropertyShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (PropertyShapeModel property in type.Properties)
        {
            if (i++ > 0)
                writer.WriteLine();

            // Suppress property getters that are nullable, but are not Nullable<T>
            bool suppressGetter = property.PropertyTypeContainsNullabilityAnnotations || property is
            {
                PropertyType.SpecialType: not SpecialType.System_Nullable_T,
                IsGetterNonNullable: false
            };

            // Suppress non-nullable Nullable<T> property setters (i.e. setters with [DisallowNull] annotation)
            bool suppressSetter = property.PropertyTypeContainsNullabilityAnnotations || property is 
            { 
                PropertyType.SpecialType: SpecialType.System_Nullable_T,
                IsSetterNonNullable: true,
            };

            writer.WriteLine($$"""
                new global::TypeShape.SourceGenModel.SourceGenPropertyShape<{{type.Type.FullyQualifiedName}}, {{property.PropertyType.FullyQualifiedName}}>
                {
                    Name = {{FormatStringLiteral(property.Name)}},
                    DeclaringType = (global::TypeShape.Abstractions.IObjectTypeShape<{{type.Type.FullyQualifiedName}}>){{type.Type.GeneratedPropertyName}},
                    PropertyType = {{property.PropertyType.GeneratedPropertyName}},
                    Getter = {{(property.EmitGetter ? $"static (ref {type.Type.FullyQualifiedName} obj) => obj.{property.UnderlyingMemberName}{(suppressGetter ? "!" : "")}" : "null")}},
                    Setter = {{(property.EmitSetter ? $"static (ref {type.Type.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value) => obj.{property.UnderlyingMemberName} = value{(suppressSetter ? "!" : "")}" : "null")}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, property)}},
                    IsField = {{FormatBool(property.IsField)}},
                    IsGetterPublic = {{FormatBool(property.IsGetterPublic)}},
                    IsSetterPublic = {{FormatBool(property.IsSetterPublic)}},
                    IsGetterNonNullable = {{FormatBool(property.IsGetterNonNullable)}},
                    IsSetterNonNullable = {{FormatBool(property.IsSetterNonNullable)}},
                },
                """, trimNullAssignmentLines: true);

            static string FormatAttributeProviderFunc(ObjectShapeModel type, PropertyShapeModel property)
            {
                if (type.IsTupleType)
                {
                    return "null";
                }

                return property.IsField
                    ? $$"""static () => typeof({{property.DeclaringType.FullyQualifiedName}}).GetField({{FormatStringLiteral(property.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}})"""
                    : $$"""static () => typeof({{property.DeclaringType.FullyQualifiedName}}).GetProperty({{FormatStringLiteral(property.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}}, null, typeof({{property.PropertyType.FullyQualifiedName}}), [], null)""";
            }
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }
}
