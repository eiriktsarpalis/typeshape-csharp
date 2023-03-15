using System;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatEnumTypeFactory(SourceWriter writer, string methodName, EnumTypeModel enumType)
    {
        writer.WriteLine($$"""
            private global::TypeShape.IEnumType {{methodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenEnumType<{{enumType.Type.FullyQualifiedName}}, {{enumType.UnderlyingType.FullyQualifiedName}}>
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
            private global::TypeShape.INullableType {{methodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenNullableType<{{nullableType.ElementType.FullyQualifiedName}}>
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
            private global::TypeShape.IEnumerableType {{methodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenEnumerableType<{{enumerableType.Type.FullyQualifiedName}}, {{enumerableType.ElementType.FullyQualifiedName}}>
                {
                    Type = {{enumerableType.Type.GeneratedPropertyName}},
                    ElementType = {{enumerableType.ElementType.GeneratedPropertyName}},
                    GetEnumerableFunc = {{FormatGetEnumerableFunc(enumerableType)}},
                    AddElementFunc = {{FormatAddElementFunc(enumerableType)}},
                };
            }
            """);

        static string FormatGetEnumerableFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType.Kind switch
            {
                EnumerableKind.ArrayOfT or
                EnumerableKind.IEnumerableOfT or
                EnumerableKind.ICollectionOfT => "static obj => obj",
                EnumerableKind.IEnumerable or
                EnumerableKind.IList => "static obj => global::System.Linq.Enumerable.Cast<object>(obj)",
                _ => throw new ArgumentException(enumerableType.Kind.ToString()),
            };
        }

        static string FormatAddElementFunc(EnumerableTypeModel enumerableType)
        {
            return enumerableType switch
            {
                { AddElementMethod: string addMethod } => $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => obj.{addMethod}(value)",
                { Kind: EnumerableKind.ICollectionOfT } => $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => ((global::System.Collections.Generic.ICollection<{enumerableType.ElementType.FullyQualifiedName}>)obj).Add(value)",
                { Kind: EnumerableKind.IList } => $"static (ref {enumerableType.Type.FullyQualifiedName} obj, object? value) => ((global::System.Collections.IList)obj).Add(value)",
                _ => "null",
            };
        }
    }

    private static void FormatDictionaryTypeFactory(SourceWriter writer, string methodName, DictionaryTypeModel dictionaryType)
    {
        writer.WriteLine($$"""
            private global::TypeShape.IDictionaryType {{methodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenDictionaryType<{{dictionaryType.Type.FullyQualifiedName}}, {{dictionaryType.KeyType.FullyQualifiedName}}, {{dictionaryType.ValueType.FullyQualifiedName}}>
                {
                    Type = {{dictionaryType.Type.GeneratedPropertyName}},
                    KeyType = {{dictionaryType.KeyType.GeneratedPropertyName}},
                    ValueType = {{dictionaryType.ValueType.GeneratedPropertyName}},
                    GetDictionaryFunc = {{FormatGetDictionaryFunc(dictionaryType)}},
                    AddKeyValuePairFunc = {{FormatKeyValuePairFunc(dictionaryType)}},
                };
            }
            """);

        static string FormatGetDictionaryFunc(DictionaryTypeModel dictionaryType)
        {
            return dictionaryType.Kind switch
            {
                DictionaryKind.IReadOnlyDictionaryOfKV => "static obj => obj",
                DictionaryKind.IDictionaryOfKV => $"static obj => global::TypeShape.SourceGenModel.CollectionHelpers.AsReadOnlyDictionary<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(obj)",
                DictionaryKind.IDictionary => "static obj => global::TypeShape.SourceGenModel.CollectionHelpers.AsReadOnlyDictionary(obj)",
                _ => throw new ArgumentException(dictionaryType.Kind.ToString()),
            };
        }

        static string FormatKeyValuePairFunc(DictionaryTypeModel enumerableType)
        {
            return enumerableType switch
            {
                { HasSettableIndexer: true } or
                { Kind: DictionaryKind.IDictionaryOfKV } => $"static (ref {enumerableType.Type.FullyQualifiedName} dict, KeyValuePair<{enumerableType.KeyType.FullyQualifiedName}, {enumerableType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key] = kvp.Value",
                { Kind: DictionaryKind.IDictionary } => $"static (ref {enumerableType.Type.FullyQualifiedName} dict, KeyValuePair<object, object?> kvp) => dict[kvp.Key] = kvp.Value",
                _ => "null",
            };
        }
    }
}
