using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatObjectTypeShapeFactory(SourceWriter writer, string methodName, ObjectShapeModel objectShapeModel)
    {
        string? propertiesFactoryMethodName = objectShapeModel.Properties.Length > 0 ? $"CreateProperties_{objectShapeModel.Type.TypeIdentifier}" : null;
        string? constructorFactoryMethodName = objectShapeModel.Constructor != null ? $"CreateConstructor_{objectShapeModel.Type.TypeIdentifier}" : null;

        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{objectShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenObjectTypeShape<{{objectShapeModel.Type.FullyQualifiedName}}>
                {
                    Provider = this,
                    IsRecordType = {{FormatBool(objectShapeModel.IsRecordType)}},
                    IsTupleType = {{FormatBool(objectShapeModel.IsTupleType)}},
                    CreatePropertiesFunc = {{FormatNull(propertiesFactoryMethodName)}},
                    CreateConstructorFunc = {{FormatNull(constructorFactoryMethodName)}},
                };
            }
            """, trimNullAssignmentLines: true);

        if (propertiesFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatPropertyFactory(writer, propertiesFactoryMethodName, objectShapeModel);
        }

        if (constructorFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatConstructorFactory(writer, constructorFactoryMethodName, objectShapeModel, objectShapeModel.Constructor!);
        }

        FormatMemberAccessors(writer, objectShapeModel);
    }

    private static void FormatMemberAccessors(SourceWriter writer, ObjectShapeModel objectShapeModel)
    {
        foreach (PropertyShapeModel property in objectShapeModel.Properties)
        {
            if (property.IsField)
            {
                writer.WriteLine();
                FormatFieldAccessor(writer, property);
            }
            else
            {
                if (property is { EmitGetter: true, IsGetterAccessible: false })
                {
                    writer.WriteLine();
                    FormatPropertyGetterAccessor(writer, property);
                }

                if (property is { EmitSetter: true, IsSetterAccessible: false } || 
                    (objectShapeModel.Constructor is not null && property.IsInitOnly))
                {
                    writer.WriteLine();
                    FormatPropertySetterAccessor(writer, property);
                }
            }
        }

        if (objectShapeModel.Constructor is { IsAccessible: false } ctor)
        {
            writer.WriteLine();
            FormatConstructorAccessor(writer, ctor);
        }
    }
}
