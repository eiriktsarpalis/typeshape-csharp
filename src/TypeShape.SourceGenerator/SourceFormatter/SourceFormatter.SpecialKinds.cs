using System;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatEnumTypeFactory(SourceWriter writer, string methodName, EnumTypeModel enumType)
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

    private static void FormatNullableTypeFactory(SourceWriter writer, string methodName, NullableTypeModel nullableType)
    {
        writer.WriteLine($$"""
            private INullableShape {{methodName}}()
            {
                return new SourceGenNullableShape<{{nullableType.ElementType.FullyQualifiedName}}>
                {
                    Type = {{nullableType.Type.GeneratedPropertyName}},
                    ElementType = {{nullableType.ElementType.GeneratedPropertyName}},
                };
            }
            """);
    }

    private static void FormatEnumerableTypeFactory(SourceWriter writer, string methodName, EnumerableTypeModel enumerableType)
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
            """);

        static string FormatGetEnumerableFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType.Kind switch
            {
                EnumerableKind.IEnumerableOfT or
                EnumerableKind.ArrayOfT => "static obj => obj",
                EnumerableKind.IEnumerable => "static obj => global::System.Linq.Enumerable.Cast<object>(obj)",
                EnumerableKind.MultiDimensionalArrayOfT => $"static obj => global::System.Linq.Enumerable.Cast<{enumerableType.ElementType.FullyQualifiedName}>(obj)",
                _ => throw new ArgumentException(enumerableType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType.ConstructionStrategy is CollectionConstructionStrategy.Mutable
                ? $"static () => new {enumerableType.Type.FullyQualifiedName}()"
                : "null";
        }

        static string FormatAddElementFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType.AddElementMethod is { } addMethod
                ? $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => obj.{addMethod}(value)"
                : "null";
        }

        static string FormatSpanConstructorFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType switch
            {
                { Kind: EnumerableKind.ArrayOfT } => $"static values => values.ToArray()",
                { ConstructionStrategy: CollectionConstructionStrategy.Span } => $"static values => {enumerableType.SpanFactoryMethod}(values)",
                _ => "null",
            };
        }

        static string FormatEnumerableConstructorFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType.ConstructionStrategy is CollectionConstructionStrategy.Enumerable
                ? $"static values => new {enumerableType.Type.FullyQualifiedName}(values)"
                : "null";
        }
    }

    private static void FormatDictionaryTypeFactory(SourceWriter writer, string methodName, DictionaryTypeModel dictionaryType)
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
            """);

        static string FormatGetDictionaryFunc(DictionaryTypeModel dictionaryType)
        {
            return dictionaryType.Kind switch
            {
                DictionaryKind.IReadOnlyDictionaryOfKV => "static obj => obj",
                DictionaryKind.IDictionaryOfKV => $"static obj => CollectionHelpers.AsReadOnlyDictionary<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(obj)",
                DictionaryKind.IDictionary => "static obj => CollectionHelpers.AsReadOnlyDictionary(obj)!",
                _ => throw new ArgumentException(dictionaryType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(DictionaryTypeModel enumerableType)
        {
            return enumerableType.ConstructionStrategy is CollectionConstructionStrategy.Mutable
                ? $"static () => new {enumerableType.Type.FullyQualifiedName}()"
                : "null";
        }

        static string FormatAddKeyValuePairFunc(DictionaryTypeModel enumerableType)
        {
            return enumerableType switch
            {
                { HasSettableIndexer: true } => $"static (ref {enumerableType.Type.FullyQualifiedName} dict, KeyValuePair<{enumerableType.KeyType.FullyQualifiedName}, {enumerableType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key] = kvp.Value",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable } => $"static (ref {enumerableType.Type.FullyQualifiedName} dict, KeyValuePair<object, object> kvp) => dict.Add(kvp.Key, kvp.Value)",
                _ => "null",
            };
        }

        static string FormatEnumerableConstructorFunc(DictionaryTypeModel enumerableType)
        {
            return enumerableType switch
            {
                { EnumerableFactoryMethod: string factory } => $"static values => {factory}(values)",
                { ConstructionStrategy: CollectionConstructionStrategy.Enumerable } => $"static values => new {enumerableType.Type.FullyQualifiedName}(values)",
                _ => "null",
            };
        }

        static string FormatSpanConstructorFunc(DictionaryTypeModel enumerableType)
        {
            return enumerableType switch
            {
                { SpanFactoryMethod: string factory } => $"static values => {factory}(values)",
                _ => "null",
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
