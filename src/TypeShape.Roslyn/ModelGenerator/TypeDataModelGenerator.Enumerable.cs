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
        CollectionModelConstructionStrategy constructionStrategy = CollectionModelConstructionStrategy.None;
        ITypeSymbol? elementType = null;
        IMethodSymbol? addElementMethod = null;
        //INamedTypeSymbol? implementationType = null;
        IMethodSymbol? factoryMethod = null;

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
                constructionStrategy = CollectionModelConstructionStrategy.Span;
                factoryMethod = builderMethod;
            }
            else if (GetImmutableCollectionFactory(namedType) is IMethodSymbol factory)
            {
                // Must be run before mutable collection checks since ImmutableArray
                // also has a default constructor and an Add method.
                constructionStrategy = CollectionModelConstructionStrategy.List;
                factoryMethod = factory;
            }
            else if (namedType.Constructors.FirstOrDefault(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleSymbol(ctor)) is { } ctor &&
                TryGetAddMethod(type, elementType, out addElementMethod))
            {
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = ctor;
            }
            else if (namedType.Constructors.FirstOrDefault(ctor =>
                IsAccessibleSymbol(ctor) &&
                ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
                SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT) &&
                SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType)) is IMethodSymbol ctor2)
            {
                constructionStrategy = CollectionModelConstructionStrategy.Span;
                factoryMethod = ctor2;
            }
            else if (namedType.Constructors.FirstOrDefault(ctor =>
                IsAccessibleSymbol(ctor) &&
                ctor.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                KnownSymbols.ListOfT?.GetCompatibleGenericBaseType(parameterType.ConstructedFrom) != null &&
                SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType)) is IMethodSymbol ctor3)
            {
                // Type exposes a constructor that accepts a subtype of List<T>
                constructionStrategy = CollectionModelConstructionStrategy.List;
                factoryMethod = ctor3;
            }
            else if (namedType.TypeKind is TypeKind.Interface)
            {
                INamedTypeSymbol listOfT = KnownSymbols.ListOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(listOfT))
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                    factoryMethod = listOfT.Constructors.First(c => c.Parameters.IsEmpty);
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
                    constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                    factoryMethod = hashSetOfT.Constructors.First(c => c.Parameters.IsEmpty);
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

            if (namedType.Constructors.FirstOrDefault(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleSymbol(ctor)) is { } ctor &&
                TryGetAddMethod(type, elementType, out addElementMethod))
            {
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = ctor;
            }
            else if (type.IsAssignableFrom(KnownSymbols.IList))
            {
                // Handle construction of IList, ICollection and IEnumerable interfaces using List<object?>
                INamedTypeSymbol listOfObject = KnownSymbols.ListOfT!.Construct(elementType);
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = listOfObject.Constructors.First(c => c.Parameters.IsEmpty);
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
            AddElementMethod = addElementMethod,
            FactoryMethod = factoryMethod,
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
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableList))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableQueue))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableQueue")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableStack))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableStack")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableHashSet))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedSet))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedSet")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            return null;
        }
    }
}
