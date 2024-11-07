using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatDictionaryTypeShapeFactory(SourceWriter writer, string methodName, DictionaryShapeModel dictionaryShapeModel)
    {
        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenDictionaryTypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}, {{dictionaryShapeModel.KeyType.FullyQualifiedName}}, {{dictionaryShapeModel.ValueType.FullyQualifiedName}}>
                {
                    KeyType = {{dictionaryShapeModel.KeyType.GeneratedPropertyName}},
                    ValueType = {{dictionaryShapeModel.ValueType.GeneratedPropertyName}},
                    GetDictionaryFunc = {{FormatGetDictionaryFunc(dictionaryShapeModel)}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(dictionaryShapeModel.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(dictionaryShapeModel)}},
                    AddKeyValuePairFunc = {{FormatAddKeyValuePairFunc(dictionaryShapeModel)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(dictionaryShapeModel)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(dictionaryShapeModel)}},
                    Provider = this,
                };
            }
            """, trimNullAssignmentLines: true);

        static string FormatGetDictionaryFunc(DictionaryShapeModel dictionaryType)
        {
            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            return dictionaryType.Kind switch
            {
                DictionaryKind.IReadOnlyDictionaryOfKV => $"static obj => obj{suppressSuffix}",
                DictionaryKind.IDictionaryOfKV => $"static obj => global::PolyType.SourceGenModel.CollectionHelpers.AsReadOnlyDictionary<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(obj{suppressSuffix})",
                DictionaryKind.IDictionary => $"static obj => global::PolyType.SourceGenModel.CollectionHelpers.AsReadOnlyDictionary(obj{suppressSuffix})!",
                _ => throw new ArgumentException(dictionaryType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            return dictionaryType.ConstructionStrategy is CollectionConstructionStrategy.Mutable
                ? $"static () => new {dictionaryType.ImplementationTypeFQN ?? dictionaryType.Type.FullyQualifiedName}()"
                : "null";
        }

        static string FormatAddKeyValuePairFunc(DictionaryShapeModel dictionaryType)
        {
            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            return dictionaryType switch
            {
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: null }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: { } implementationTypeFQN }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => (({implementationTypeFQN})dict)[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                _ => "null",
            };
        }

        static string FormatEnumerableConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory, IsTupleEnumerableFactory: false } => $"static values => {factory}(values{suppressSuffix})",
                { StaticFactoryMethod: string factory } => $"static values => {factory}(global::System.Linq.Enumerable.Select(values, kvp => new global::System.Tuple<{dictionaryType.KeyType.FullyQualifiedName},{dictionaryType.ValueType.FullyQualifiedName}>(kvp.Key, kvp.Value)))",
                _ => $"static values => new {dictionaryType.Type.FullyQualifiedName}(values{suppressSuffix})",
            };
        }

        static string FormatSpanConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            string valuesExpr = dictionaryType.CtorRequiresDictionaryConversion ? $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary(values{suppressSuffix})" : $"values{suppressSuffix}";
            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory } => $"static values => {factory}({valuesExpr})",
                _ => $"static values => new {dictionaryType.Type.FullyQualifiedName}({valuesExpr})",
            };
        }
    }
}
