using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatDictionaryTypeShapeFactory(SourceWriter writer, string methodName, DictionaryShapeModel dictionaryShapeModel)
    {
        writer.WriteLine($$"""
            private ITypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new SourceGenDictionaryTypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}, {{dictionaryShapeModel.KeyType.FullyQualifiedName}}, {{dictionaryShapeModel.ValueType.FullyQualifiedName}}>
                {
                    Provider = this,
                    KeyType = {{dictionaryShapeModel.KeyType.GeneratedPropertyName}},
                    ValueType = {{dictionaryShapeModel.ValueType.GeneratedPropertyName}},
                    GetDictionaryFunc = {{FormatGetDictionaryFunc(dictionaryShapeModel)}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(dictionaryShapeModel.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(dictionaryShapeModel)}},
                    AddKeyValuePairFunc = {{FormatAddKeyValuePairFunc(dictionaryShapeModel)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(dictionaryShapeModel)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(dictionaryShapeModel)}},
                };
            }
            """, trimNullAssignmentLines: true);

        static string FormatGetDictionaryFunc(DictionaryShapeModel dictionaryType)
        {
            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            return dictionaryType.Kind switch
            {
                DictionaryKind.IReadOnlyDictionaryOfKV => $"static obj => obj{suppressSuffix}",
                DictionaryKind.IDictionaryOfKV => $"static obj => CollectionHelpers.AsReadOnlyDictionary<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(obj{suppressSuffix})",
                DictionaryKind.IDictionary => $"static obj => CollectionHelpers.AsReadOnlyDictionary(obj{suppressSuffix})!",
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
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: { } implementationTypeFQN }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => (({implementationTypeFQN})dict)[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
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
                { StaticFactoryMethod: string factory } => $"static values => {factory}(values{suppressSuffix})",
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
            string valuesExpr = dictionaryType.CtorRequiresDictionaryConversion ? $"CollectionHelpers.CreateDictionary(values{suppressSuffix})" : $"values{suppressSuffix}";
            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory } => $"static values => {factory}({valuesExpr})",
                _ => $"static values => new {dictionaryType.Type.FullyQualifiedName}({valuesExpr})",
            };
        }
    }
}
