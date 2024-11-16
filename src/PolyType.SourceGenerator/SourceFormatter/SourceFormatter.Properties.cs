using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatPropertyFactory(SourceWriter writer, string methodName, ObjectShapeModel type)
    {
        writer.WriteLine($"private global::PolyType.Abstractions.IPropertyShape[] {methodName}() => new global::PolyType.Abstractions.IPropertyShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (PropertyShapeModel property in type.Properties)
        {
            if (i++ > 0)
            {
                writer.WriteLine();
            }

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
                new global::PolyType.SourceGenModel.SourceGenPropertyShape<{{type.Type.FullyQualifiedName}}, {{property.PropertyType.FullyQualifiedName}}>
                {
                    Name = {{FormatStringLiteral(property.Name)}},
                    DeclaringType = (global::PolyType.Abstractions.IObjectTypeShape<{{type.Type.FullyQualifiedName}}>){{type.SourceIdentifier}},
                    PropertyType = {{GetShapeModel(property.PropertyType).SourceIdentifier}},
                    Getter = {{(property.EmitGetter ? $"static (ref {type.Type.FullyQualifiedName} obj) => {FormatGetterBody("obj", type, property)}{(suppressGetter ? "!" : "")}" : "null")}},
                    Setter = {{(property.EmitSetter ? $"static (ref {type.Type.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value) => {FormatSetterBody("obj", "value" + (suppressSetter ? "!" : ""), type, property)}" : "null")}},
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

            static string FormatGetterBody(string objParam, ObjectShapeModel declaringType, PropertyShapeModel property)
            {
                if (!property.IsGetterAccessible)
                {
                    string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
                    if (property.IsField)
                    {
                        string fieldAccessorName = GetFieldAccessorName(declaringType, property.UnderlyingMemberName);
                        return $"{fieldAccessorName}({refPrefix}{objParam})";
                    }
                    else
                    {
                        string propertyGetterAccessorName = GetPropertyGetterAccessorName(declaringType, property.UnderlyingMemberName);
                        return $"{propertyGetterAccessorName}({refPrefix}{objParam})";
                    }
                }

                return $"{objParam}.{RoslynHelpers.EscapeKeywordIdentifier(property.UnderlyingMemberName)}";
            }

            static string FormatSetterBody(string objParam, string valueParam, ObjectShapeModel declaringType, PropertyShapeModel property)
            {
                if (!property.IsSetterAccessible)
                {
                    string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
                    if (property.IsField)
                    {
                        string fieldAccessorName = GetFieldAccessorName(declaringType, property.UnderlyingMemberName);
                        return $"{fieldAccessorName}({refPrefix}{objParam}) = {valueParam}";
                    }
                    else
                    {
                        string propertySetterAccessorName = GetPropertySetterAccessorName(declaringType, property.UnderlyingMemberName);
                        return $"{propertySetterAccessorName}({refPrefix}{objParam}, {valueParam})";
                    }
                }

                return $"{objParam}.{RoslynHelpers.EscapeKeywordIdentifier(property.UnderlyingMemberName)} = {valueParam}";
            }
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }

    private static string GetFieldAccessorName(TypeShapeModel declaringType, string underlyingMemberName)
    {
        return $"__FieldAccessor_{declaringType.SourceIdentifier}_{underlyingMemberName}";
    }

    private static string GetPropertyGetterAccessorName(TypeShapeModel declaringType, string underlyingMemberName)
    {
        return $"__GetAccessor_{declaringType.SourceIdentifier}_{underlyingMemberName}";
    }

    private static string GetPropertySetterAccessorName(TypeShapeModel declaringType, string underlyingMemberName)
    {
        return $"__SetAccessor_{declaringType.SourceIdentifier}_{underlyingMemberName}";
    }

    private static void FormatFieldAccessor(SourceWriter writer, ObjectShapeModel declaringType, PropertyShapeModel property)
    {
        Debug.Assert(property.IsField);
        string accessorName = GetFieldAccessorName(declaringType, property.UnderlyingMemberName);
        string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";

        writer.WriteLine($"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = {FormatStringLiteral(property.UnderlyingMemberName)})]
            private static extern ref {property.PropertyType.FullyQualifiedName} {accessorName}({refPrefix}{property.DeclaringType.FullyQualifiedName} obj);
            """);
    }

    private static void FormatPropertyGetterAccessor(SourceWriter writer, ObjectShapeModel declaringType, PropertyShapeModel property)
    {
        Debug.Assert(!property.IsField);
        string accessorName = GetPropertyGetterAccessorName(declaringType, property.UnderlyingMemberName);
        string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
        string propertyGetter = "get_" + property.UnderlyingMemberName;

        if (property.IsGenericPropertyType)
        {
            writer.WriteLine($$"""
                // Workaround for https://github.com/dotnet/runtime/issues/89439
                private static global::System.Reflection.MethodInfo? __s_{{accessorName}}_MethodInfo;
                private static {{property.PropertyType.FullyQualifiedName}} {{accessorName}}({{refPrefix}}{{property.DeclaringType.FullyQualifiedName}} obj)
                {
                    global::System.Reflection.MethodInfo getter = __s_{{accessorName}}_MethodInfo ??= typeof({{property.DeclaringType.FullyQualifiedName}}).GetMethod({{FormatStringLiteral(propertyGetter)}}, {{InstanceBindingFlagsConstMember}})!;
                    return getter.Invoke(obj, null);
                }
                """);

            return;
        }

        writer.WriteLine($"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(propertyGetter)})]
            private static extern {property.PropertyType.FullyQualifiedName} {accessorName}({refPrefix}{property.DeclaringType.FullyQualifiedName} obj);
            """);
    }

    private static void FormatPropertySetterAccessor(SourceWriter writer, ObjectShapeModel declaringType, PropertyShapeModel property)
    {
        Debug.Assert(!property.IsField);
        string accessorName = GetPropertySetterAccessorName(declaringType, property.UnderlyingMemberName);
        string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
        string propertySetter = "set_" + property.UnderlyingMemberName;

        if (property.IsGenericPropertyType)
        {
            writer.WriteLine($$"""
                // Workaround for https://github.com/dotnet/runtime/issues/89439
                private static global::System.Reflection.MethodInfo? __s_{{accessorName}}_MethodInfo;
                private static void {{accessorName}}({{refPrefix}}{{property.DeclaringType.FullyQualifiedName}} obj, {{property.PropertyType.FullyQualifiedName}} value)
                {
                """);

            writer.Indentation++;
            writer.WriteLine($"global::System.Reflection.MethodInfo setter = __s_{accessorName}_MethodInfo ??= typeof({property.DeclaringType.FullyQualifiedName}).GetMethod({FormatStringLiteral(propertySetter)}, {InstanceBindingFlagsConstMember})!;");
            if (property.DeclaringType.IsValueType)
            {
                writer.WriteLine($$"""
                    object boxed = obj;
                    setter.Invoke(boxed, new object[] { value });
                    obj = ({{property.DeclaringType.FullyQualifiedName}})boxed;
                    """);
            }
            else
            {
                writer.WriteLine("setter.Invoke(obj, new object[] { value });");
            }

            writer.Indentation--;
            writer.WriteLine('}');
            return;
        }

        writer.WriteLine($"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(propertySetter)})]
            private static extern void {accessorName}({refPrefix}{property.DeclaringType.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value);
            """);
    }
}
