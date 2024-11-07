using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PolyType.Roslyn;

/// <summary>
/// Defines an immutable struct encapsulating a snapshot of the current generation state.
/// Used for rolling back the state whenever an unsupported type is encountered in the graph.
/// </summary>
public readonly ref struct TypeDataModelGenerationContext
{
    internal TypeDataModelGenerationContext(ImmutableStack<ITypeSymbol> stack, ImmutableDictionary<ITypeSymbol, TypeDataModel> generatedModels)
    {
        Stack = stack;
        GeneratedModels = generatedModels;
    }

    internal ImmutableStack<ITypeSymbol> Stack { get; }
    internal ImmutableDictionary<ITypeSymbol, TypeDataModel> GeneratedModels { get; }

    internal TypeDataModelGenerationContext Push(ITypeSymbol type) => new(Stack.Push(type), GeneratedModels);
    internal TypeDataModelGenerationContext Commit(TypeDataModel type)
    {
        Debug.Assert(GeneratedModels.KeyComparer.Equals(type.Type, Stack.Peek()));
        return new(Stack.Pop(), GeneratedModels.Add(type.Type, type));
    }
}