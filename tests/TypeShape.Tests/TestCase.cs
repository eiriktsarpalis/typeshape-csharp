using System.Collections;
using System.Runtime.CompilerServices;
using TypeShape.Abstractions;
using TypeShape.ReflectionProvider;

namespace TypeShape.Tests;

public static class TestCase
{
    public static TestCase<T> Create<T>(
        T? value,
        string? expectedEncoding = null,
        T?[]? additionalValues = null,
        bool hasRefConstructorParameters = false,
        bool hasOutConstructorParameters = false,
        bool isLossyRoundtrip = false,
        bool usesSpanCtor = false,
        bool isStack = false)
        where T : IShapeable<T> =>
        
        new TestCase<T, T>(value) 
        { 
            ExpectedEncoding = expectedEncoding,
            AdditionalValues = additionalValues, 
            HasRefConstructorParameters = hasRefConstructorParameters,
            HasOutConstructorParameters = hasOutConstructorParameters,
            IsLossyRoundtrip = isLossyRoundtrip, 
            UsesSpanConstructor = usesSpanCtor,
            IsStack = isStack,
        };

    public static TestCase<T> Create<TProvider, T>(
        TProvider? _,
        T? value,
        string? expectedEncoding = null,
        T?[]? additionalValues = null,
        bool isLossyRoundtrip = false,
        bool hasRefConstructorParameters = false,
        bool hasOutConstructorParameters = false,
        bool usesSpanCtor = false,
        bool isStack = false)
        where TProvider : IShapeable<T> =>
    
        new TestCase<T, TProvider>(value) 
        {
            ExpectedEncoding = expectedEncoding,
            AdditionalValues = additionalValues,
            HasRefConstructorParameters = hasRefConstructorParameters,
            HasOutConstructorParameters = hasOutConstructorParameters,
            IsLossyRoundtrip = isLossyRoundtrip, 
            UsesSpanConstructor = usesSpanCtor,
            IsStack = isStack,
        };
}

public sealed record TestCase<T, TProvider>(T? Value) : TestCase<T>(Value)
    where TProvider : IShapeable<T>
{
    public override ITypeShape<T> GetShape(IProviderUnderTest provider) =>
        provider.GetShape<T, TProvider>();
}
    
public abstract record TestCase<T>(T? Value) : ITestCase
{
    public T?[]? AdditionalValues { get; init; }
    public string? ExpectedEncoding { get; init; }
    public bool IsStack { get; init; }
    public bool IsLossyRoundtrip { get; init; }
    public bool HasRefConstructorParameters { get; init; }
    public bool HasOutConstructorParameters { get; init; }
    public bool UsesSpanConstructor { get; init; }

    public abstract ITypeShape<T> GetShape(IProviderUnderTest provider);

    public Type Type => typeof(T);
    object? ITestCase.Value => Value;
    ITypeShape ITestCase.GetShape(IProviderUnderTest provider) => GetShape(provider);
    
    public bool HasConstructors(IProviderUnderTest provider) =>
        !(IsAbstract && !typeof(IEnumerable).IsAssignableFrom(typeof(T))) &&
        !IsMultiDimensionalArray &&
        !HasOutConstructorParameters &&
        (!UsesSpanConstructor || provider.Kind is not ProviderKind.Reflection);

    public bool IsNullable => default(T) is null;
    public bool IsEquatable => 
        typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) &&
        !typeof(T).IsImmutableArray() &&
        !typeof(T).IsMemoryType(out _, out _) &&
        !typeof(T).IsRecordType();
    
    public bool IsTuple => typeof(ITuple).IsAssignableFrom(typeof(T));
    public bool IsLongTuple => IsTuple && typeof(T).GetMember("Rest").Any();
    public bool IsMultiDimensionalArray => typeof(T).IsArray && typeof(T).GetArrayRank() != 1;
    public bool IsAbstract => typeof(T).IsAbstract || typeof(T).IsInterface;

    IEnumerable<ITestCase> ITestCase.ExpandCases()
    {
        yield return this with { AdditionalValues = [] };
        
        if (default(T) is null && Value is not null)
        {
            yield return this with { Value = default, AdditionalValues = [] };
        }

        if (ExpectedEncoding != null && AdditionalValues != null)
        {
            throw new InvalidOperationException("Cannot combine expected encodings with additional values!");
        }
        
        foreach (T? additionalValue in AdditionalValues ?? [])
        {
            yield return this with { Value = additionalValue, AdditionalValues = [] };
        }
    }
}

public interface ITestCase
{
    Type Type { get; }
    object? Value { get; }
    string? ExpectedEncoding { get; }
    ITypeShape GetShape(IProviderUnderTest providerUnderTest);
    IEnumerable<ITestCase> ExpandCases();
}