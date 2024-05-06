using TypeShape.Abstractions;
using TypeShape.ReflectionProvider;

namespace TypeShape.Tests;

public interface IProviderUnderTest
{
    ITypeShapeProvider? Provider { get; }
    ProviderKind Kind { get; }
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
    public ITypeShapeProvider? Provider => null;
    public ITypeShape<T> GetShape<T, TProvider>() where TProvider : ITypeShapeProvider<T> => TProvider.GetShape();
    public ITypeShape<T> UncheckedGetShape<T>() => throw new NotSupportedException();
}

public sealed class RefectionProviderUnderTest(bool useReflectionEmit) : IProviderUnderTest
{
    public static RefectionProviderUnderTest Default { get; } = new(true);
    public static RefectionProviderUnderTest NoEmit { get; } = new(false);

    public ProviderKind Kind => useReflectionEmit ? ProviderKind.ReflectionEmit : ProviderKind.Reflection;
    public ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit);
    public ITypeShape<T> GetShape<T, TProvider>() where TProvider : ITypeShapeProvider<T> => Provider.Resolve<T>();
    public ITypeShape<T> UncheckedGetShape<T>() => Provider.Resolve<T>();
}