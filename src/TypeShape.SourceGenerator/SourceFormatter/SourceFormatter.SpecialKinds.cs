using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatEnumTypeFactory(SourceWriter writer, string methodName, EnumShapeModel enumType)
    {
        writer.WriteLine($$"""
            private IEnumShape {{methodName}}()
            {
                return new SourceGenEnumShape<{{enumType.Type.FullyQualifiedName}}, {{enumType.UnderlyingType.FullyQualifiedName}}>
                {
                    Type = {{enumType.Type.GeneratedPropertyName}},
                    UnderlyingType = {{enumType.UnderlyingType.GeneratedPropertyName}},
                };
            }
            """);
    }

    private static void FormatNullableTypeFactory(SourceWriter writer, string methodName, NullableShapeModel nullableType)
    {
        writer.WriteLine($$"""
            private INullableShape {{methodName}}()
            {
                return new SourceGenNullableShape<{{nullableType.ElementType.FullyQualifiedName}}>
                {
                    ElementType = {{nullableType.ElementType.GeneratedPropertyName}},
                };
            }
            """);
    }

    private static void FormatEnumerableTypeFactory(SourceWriter writer, string methodName, EnumerableShapeModel enumerableType)
    {
        writer.WriteLine($$"""
            private IEnumerableShape {{methodName}}()
            {
                return new SourceGenEnumerableShape<{{enumerableType.Type.FullyQualifiedName}}, {{enumerableType.ElementType.FullyQualifiedName}}>
                {
                    Type = {{enumerableType.Type.GeneratedPropertyName}},
                    ElementType = {{enumerableType.ElementType.GeneratedPropertyName}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(enumerableType.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(enumerableType)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(enumerableType)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(enumerableType)}},
                    GetEnumerableFunc = {{FormatGetEnumerableFunc(enumerableType)}},
                    AddElementFunc = {{FormatAddElementFunc(enumerableType)}},
                    Rank = {{enumerableType.Rank}},
               };
            }
            """, trimNullAssignmentLines: true);

        static string FormatGetEnumerableFunc(EnumerableShapeModel enumerableType)
        {
            return enumerableType.Kind switch
            {
                EnumerableKind.IEnumerableOfT or
                EnumerableKind.ArrayOfT => "static obj => obj",
                EnumerableKind.MemoryOfT => $"static obj => global::System.Runtime.InteropServices.MemoryMarshal.ToEnumerable((ReadOnlyMemory<{enumerableType.ElementType.FullyQualifiedName}>)obj)",
                EnumerableKind.ReadOnlyMemoryOfT => $"static obj => global::System.Runtime.InteropServices.MemoryMarshal.ToEnumerable(obj)",
                EnumerableKind.IEnumerable => "static obj => global::System.Linq.Enumerable.Cast<object>(obj)",
                EnumerableKind.MultiDimensionalArrayOfT => $"static obj => global::System.Linq.Enumerable.Cast<{enumerableType.ElementType.FullyQualifiedName}>(obj)",
                _ => throw new ArgumentException(enumerableType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(EnumerableShapeModel enumerableType)
        {
            return enumerableType.ConstructionStrategy is CollectionConstructionStrategy.Mutable
                ? $"static () => new {enumerableType.ImplementationTypeFQN ?? enumerableType.Type.FullyQualifiedName}()"
                : "null";
        }

        static string FormatAddElementFunc(EnumerableShapeModel enumerableType)
        {
            return enumerableType switch
            {
                { AddElementMethod: { } addMethod, ImplementationTypeFQN: null } => 
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => obj.{addMethod}(value)",
                { AddElementMethod: { } addMethod, ImplementationTypeFQN: { } implTypeFQN } => 
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => (({implTypeFQN})obj).{addMethod}(value)",
                _ => "null",
            };
        }

        static string FormatSpanConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string valuesExpr = enumerableType.CtorRequiresListConversion ? "CollectionHelpers.CreateList(values)" : "values";
            return enumerableType switch
            {
                { Kind: EnumerableKind.ArrayOfT or EnumerableKind.ReadOnlyMemoryOfT or EnumerableKind.MemoryOfT } => $"static values => values.ToArray()",
                { StaticFactoryMethod: string spanFactory } => $"static values => {spanFactory}({valuesExpr})",
                _ => $"static values => new {enumerableType.Type.FullyQualifiedName}({valuesExpr})",
            };
        }

        static string FormatEnumerableConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
            {
                return "null";
            }

            return enumerableType switch
            {
                { StaticFactoryMethod: { } enumerableFactory } => $"static values => {enumerableFactory}(values)",
                _ => $"static values => new {enumerableType.Type.FullyQualifiedName}(values)",
            };
        }
    }

    private static void FormatDictionaryTypeFactory(SourceWriter writer, string methodName, DictionaryShapeModel dictionaryType)
    {
        writer.WriteLine($$"""
            private IDictionaryShape {{methodName}}()
            {
                return new SourceGenDictionaryShape<{{dictionaryType.Type.FullyQualifiedName}}, {{dictionaryType.KeyType.FullyQualifiedName}}, {{dictionaryType.ValueType.FullyQualifiedName}}>
                {
                    Type = {{dictionaryType.Type.GeneratedPropertyName}},
                    KeyType = {{dictionaryType.KeyType.GeneratedPropertyName}},
                    ValueType = {{dictionaryType.ValueType.GeneratedPropertyName}},
                    GetDictionaryFunc = {{FormatGetDictionaryFunc(dictionaryType)}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(dictionaryType.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(dictionaryType)}},
                    AddKeyValuePairFunc = {{FormatAddKeyValuePairFunc(dictionaryType)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(dictionaryType)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(dictionaryType)}},
                };
            }
            """, trimNullAssignmentLines: true);

        static string FormatGetDictionaryFunc(DictionaryShapeModel dictionaryType)
        {
            return dictionaryType.Kind switch
            {
                DictionaryKind.IReadOnlyDictionaryOfKV => "static obj => obj",
                DictionaryKind.IDictionaryOfKV => $"static obj => CollectionHelpers.AsReadOnlyDictionary<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(obj)",
                DictionaryKind.IDictionary => "static obj => CollectionHelpers.AsReadOnlyDictionary(obj)!",
                _ => throw new ArgumentException(dictionaryType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(DictionaryShapeModel enumerableType)
        {
            return enumerableType.ConstructionStrategy is CollectionConstructionStrategy.Mutable
                ? $"static () => new {enumerableType.ImplementationTypeFQN ?? enumerableType.Type.FullyQualifiedName}()"
                : "null";
        }

        static string FormatAddKeyValuePairFunc(DictionaryShapeModel enumerableType)
        {
            return enumerableType switch
            {
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: null }
                    => $"static (ref {enumerableType.Type.FullyQualifiedName} dict, KeyValuePair<{enumerableType.KeyType.FullyQualifiedName}, {enumerableType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key] = kvp.Value",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: { } implementationTypeFQN }
                    => $"static (ref {enumerableType.Type.FullyQualifiedName} dict, KeyValuePair<{enumerableType.KeyType.FullyQualifiedName}, {enumerableType.ValueType.FullyQualifiedName}> kvp) => (({implementationTypeFQN})dict)[kvp.Key] = kvp.Value",
                _ => "null",
            };
        }

        static string FormatEnumerableConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
            {
                return "null";
            }

            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory } => $"static values => {factory}(values)",
                _ => $"static values => new {dictionaryType.Type.FullyQualifiedName}(values)",
            };
        }

        static string FormatSpanConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string valuesExpr = dictionaryType.CtorRequiresDictionaryConversion ? "CollectionHelpers.CreateDictionary(values)" : "values";
            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory } => $"static values => {factory}({valuesExpr})",
                _ => $"static values => new {dictionaryType.Type.FullyQualifiedName}({valuesExpr})",
            };
        }
    }

    private static string FormatCollectionConstructionStrategy(CollectionConstructionStrategy strategy)
    {
        string identifier = strategy switch
        {
            CollectionConstructionStrategy.None => "None",
            CollectionConstructionStrategy.Mutable => "Mutable",
            CollectionConstructionStrategy.Enumerable => "Enumerable",
            CollectionConstructionStrategy.Span => "Span",
            _ => throw new ArgumentException(strategy.ToString()),
        };

        return $"CollectionConstructionStrategy." + identifier;
    }
}
