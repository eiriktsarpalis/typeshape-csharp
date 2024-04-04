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
}
