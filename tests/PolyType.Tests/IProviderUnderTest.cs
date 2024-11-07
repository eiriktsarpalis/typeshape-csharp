using System.Collections;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;

namespace PolyType.Tests;

public interface IProviderUnderTest
{
    ITypeShapeProvider? Provider { get; }
    ProviderKind Kind { get; }
    bool ResolvesNullableAnnotations { get; }
    ITypeShape ResolveShape(ITestCase testCase);
    ITypeShape<T> ResolveShape<T>(TestCase<T> testCase);
    ITypeShape<T> ResolveShape<T, TProvider>() where TProvider : IShapeable<T>;
    ITypeShape<T> ResolveShape<T>() where T : IShapeable<T> => ResolveShape<T, T>();
    ITypeShape<T> UncheckedResolveShape<T>();

    bool HasConstructor(ITestCase testCase) =>
        !(testCase.IsAbstract && !typeof(IEnumerable).IsAssignableFrom(testCase.Type)) &&
        !testCase.IsMultiDimensionalArray &&
        !testCase.HasOutConstructorParameters &&
        (!testCase.UsesSpanConstructor || Kind is not ProviderKind.ReflectionNoEmit);
}

public enum ProviderKind
{
    SourceGen,
    ReflectionNoEmit,
    ReflectionEmit
};

public sealed class SourceGenProviderUnderTest : IProviderUnderTest
{
    public static SourceGenProviderUnderTest Default { get; } = new();

    public ProviderKind Kind => ProviderKind.SourceGen;
    public bool ResolvesNullableAnnotations => true;
    public ITypeShapeProvider? Provider => null;
    public ITypeShape ResolveShape(ITestCase testCase) => testCase.DefaultShape;
    public ITypeShape<T> ResolveShape<T>(TestCase<T> testCase) => testCase.DefaultShape;
    public ITypeShape<T> ResolveShape<T, TProvider>() where TProvider : IShapeable<T> => TProvider.GetShape();
    public ITypeShape<T> UncheckedResolveShape<T>() => throw new NotSupportedException();
}

public sealed class RefectionProviderUnderTest(ReflectionTypeShapeProviderOptions options) : IProviderUnderTest
{
    public static RefectionProviderUnderTest Default { get; } = new(new() { UseReflectionEmit = true });
    public static RefectionProviderUnderTest NoEmit { get; } = new(new() { UseReflectionEmit = false });
    public static RefectionProviderUnderTest NoNullableAnnotations { get; } = new(new() { ResolveNullableAnnotations = false });

    public ProviderKind Kind => options.UseReflectionEmit ? ProviderKind.ReflectionEmit : ProviderKind.ReflectionNoEmit;
    public bool ResolvesNullableAnnotations => options.ResolveNullableAnnotations;
    public ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(options);
    public ITypeShape ResolveShape(ITestCase testCase) => Provider.Resolve(testCase.Type);
    public ITypeShape<T> ResolveShape<T>(TestCase<T> testCase) => Provider.Resolve<T>();
    public ITypeShape<T> ResolveShape<T, TProvider>() where TProvider : IShapeable<T> => Provider.Resolve<T>();
    public ITypeShape<T> UncheckedResolveShape<T>() => Provider.Resolve<T>();
}