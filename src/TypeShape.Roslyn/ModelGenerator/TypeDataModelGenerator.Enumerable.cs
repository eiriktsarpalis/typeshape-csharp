using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;
using TypeShape.Roslyn.Helpers;

namespace TypeShape.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapEnumerable(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = default;

        if (type.SpecialType is SpecialType.System_String)
        {
            // Do not treat string as IEnumerable<char>.
            return false;
        }

        int rank = 1;
        EnumerableKind kind = EnumerableKind.None;
        CollectionConstructionStrategy constructionStrategy = CollectionConstructionStrategy.None;
        ITypeSymbol? elementType = null;
        IMethodSymbol? addElementMethod = null;
        IMethodSymbol? spanCtor = null;
        INamedTypeSymbol? implementationType = null;
        IMethodSymbol? enumerableCtor = null;

        if (type is IArrayTypeSymbol array)
        {
            elementType = array.ElementType;

            if (array.Rank == 1)
            {
                kind = EnumerableKind.ArrayOfT;
            }
            else
            {
                kind = EnumerableKind.MultiDimensionalArrayOfT;
                rank = array.Rank;
            }
        }
        else if (type is not INamedTypeSymbol namedType)
        {
            // Type is not a named type
            return false;
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.SpanOfT))
        {
            kind = EnumerableKind.SpanOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT))
        {
            kind = EnumerableKind.ReadOnlySpanOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.MemoryOfT))
        {
            kind = EnumerableKind.MemoryOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ReadOnlyMemoryOfT))
        {
            kind = EnumerableKind.ReadOnlyMemoryOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (type.GetCompatibleGenericBaseType(KnownSymbols.IEnumerableOfT) is { } enumerableOfT)
        {
            kind = EnumerableKind.IEnumerableOfT;
            elementType = enumerableOfT.TypeArguments[0];

            if (namedType.TryGetCollectionBuilderAttribute(elementType, out IMethodSymbol? builderMethod))
            {
                constructionStrategy = CollectionConstructionStrategy.Span;
                spanCtor = builderMethod;
            }
            else if (GetImmutableCollectionFactory(namedType) is IMethodSymbol factoryMethod)
            {
                // Must be run before mutable collection checks since ImmutableArray
                // also has a default constructor and an Add method.
                constructionStrategy = CollectionConstructionStrategy.Enumerable;
                enumerableCtor = factoryMethod;
            }
            else if (namedType.Constructors.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleSymbol(ctor)) &&
                TryGetAddMethod(type, elementType, out addElementMethod))
            {
                constructionStrategy = CollectionConstructionStrategy.Mutable;
            }
            else if (namedType.Constructors.Any(ctor =>
                IsAccessibleSymbol(ctor) &&
                ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
                SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT) &&
                SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType)))
            {
                constructionStrategy = CollectionConstructionStrategy.Span;
            }
            else if (namedType.Constructors.Any(ctor =>
                IsAccessibleSymbol(ctor) &&
                ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
                parameterType.ConstructedFrom.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T &&
                SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType)))
            {
                constructionStrategy = CollectionConstructionStrategy.Enumerable;
            }
            else if (namedType.TypeKind is TypeKind.Interface)
            {
                INamedTypeSymbol listOfT = KnownSymbols.ListOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(listOfT))
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    constructionStrategy = CollectionConstructionStrategy.Mutable;
                    implementationType = listOfT;
                    addElementMethod = listOfT.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .First(m =>
                            m.Parameters.Length == 1 &&
                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType));
                }

                INamedTypeSymbol hashSetOfT = KnownSymbols.HashSetOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(hashSetOfT))
                {
                    // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                    constructionStrategy = CollectionConstructionStrategy.Mutable;
                    implementationType = hashSetOfT;
                    addElementMethod = hashSetOfT.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .First(m =>
                            m.Parameters.Length == 1 &&
                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType));
                }
            }
        }
        else if (KnownSymbols.IEnumerable.IsAssignableFrom(type))
        {
            elementType = KnownSymbols.Compilation.ObjectType;
            kind = EnumerableKind.IEnumerable;

            if (namedType.Constructors.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleSymbol(ctor)) &&
                TryGetAddMethod(type, elementType, out addElementMethod))
            {
                constructionStrategy = CollectionConstructionStrategy.Mutable;
            }
            else if (type.IsAssignableFrom(KnownSymbols.IList))
            {
                // Handle construction of IList, ICollection and IEnumerable interfaces using List<object?>
                INamedTypeSymbol listOfObject = KnownSymbols.ListOfT!.Construct(elementType);
                constructionStrategy = CollectionConstructionStrategy.Mutable;
                implementationType = listOfObject;
                addElementMethod = listOfObject.GetMembers("Add")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m =>
                        m.Parameters.Length == 1 &&
                        SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType));
            }
        }
        else
        {
            // Type is not IEnumerable
            return false;
        }

        if ((status = IncludeNestedType(elementType, ref ctx)) != TypeDataModelGenerationStatus.Success)
        {
            // Return true to indicate that the type is an unsupported enumerable type
            return true;
        }

        model = new EnumerableDataModel
        {
            Type = type,
            ElementType = elementType,
            EnumerableKind = kind,
            ConstructionStrategy = constructionStrategy,
            SpanFactory = spanCtor,
            AddElementMethod = addElementMethod,
            EnumerableFactory = enumerableCtor,
            ImplementationType = implementationType,
            Rank = rank,
        };

        return true;

        bool TryGetAddMethod(ITypeSymbol type, ITypeSymbol elementType, [NotNullWhen(true)] out IMethodSymbol? result)
        {
            result = type.GetAllMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(method =>
                    method is { IsStatic: false, Name: "Add" or "Enqueue" or "Push", Parameters: [{ Type: ITypeSymbol parameterType }] } &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType) &&
                    IsAccessibleSymbol(method));

            return result != null;
        }

        IMethodSymbol? GetImmutableCollectionFactory(INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableArray))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableList))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableQueue))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableQueue")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableStack))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableStack")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableHashSet))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedSet))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedSet")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            return null;
        }
    }
}
