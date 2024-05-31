using TypeShape.Abstractions;
using TypeShape.ReflectionProvider;

namespace TypeShape.Tests;

public interface IProviderUnderTest
{
    ITypeShapeProvider? Provider { get; }
    ProviderKind Kind { get; }
    bool ResolvesNullableAnnotations { get; }
    ITypeShape<T> GetShape<T, TProvider>() where TProvider : ITypeShapeProvider<T>;
    ITypeShape<T> GetShape<T>() where T : ITypeShapeProvider<T> => GetShape<T, T>();
    ITypeShape<T> UncheckedGetShape<T>();
}

public enum ProviderKind
{
    SourceGen,
    Reflection,
    ReflectionEmit
};

public sealed class SourceGenProviderUnderTest : IProviderUnderTest
{
    public static SourceGenProviderUnderTest Default { get; } = new();

    public ProviderKind Kind => ProviderKind.SourceGen;
    public bool ResolvesNullableAnnotations => true;
    public ITypeShapeProvider? Provider => null;
    public ITypeShape<T> GetShape<T, TProvider>() where TProvider : ITypeShapeProvider<T> => TProvider.GetShape();
    public ITypeShape<T> UncheckedGetShape<T>() => throw new NotSupportedException();
}

public sealed class RefectionProviderUnderTest(ReflectionTypeShapeProviderOptions options) : IProviderUnderTest
{
    public static RefectionProviderUnderTest Default { get; } = new(new() { UseReflectionEmit = true });
    public static RefectionProviderUnderTest NoEmit { get; } = new(new() { UseReflectionEmit = false });
    public static RefectionProviderUnderTest NoNullableAnnotations { get; } = new(new() { ResolveNullableAnnotations = false });

    public ProviderKind Kind => options.UseReflectionEmit ? ProviderKind.ReflectionEmit : ProviderKind.Reflection;
    public bool ResolvesNullableAnnotations => options.ResolveNullableAnnotations;
    public ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(options);
    public ITypeShape<T> GetShape<T, TProvider>() where TProvider : ITypeShapeProvider<T> => Provider.Resolve<T>();
    public ITypeShape<T> UncheckedGetShape<T>() => Provider.Resolve<T>();
}