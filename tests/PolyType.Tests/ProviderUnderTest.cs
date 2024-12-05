using System.Collections;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using PolyType.SourceGenModel;
using Xunit;

namespace PolyType.Tests;

public abstract class ProviderUnderTest
{
    public abstract ProviderKind Kind { get; }
    public abstract bool ResolvesNullableAnnotations { get; }
    public abstract ITypeShapeProvider Provider { get; }
    public abstract ITypeShape ResolveShape(ITestCase testCase);

    public ITypeShape<T> ResolveShape<T>(TestCase<T> testCase) =>
        (ITypeShape<T>)ResolveShape((ITestCase)testCase);

    public bool HasConstructor(ITestCase testCase) =>
        !(testCase.IsAbstract && !typeof(IEnumerable).IsAssignableFrom(testCase.Type)) &&
        !testCase.IsMultiDimensionalArray &&
        !testCase.HasOutConstructorParameters &&
        (!testCase.UsesSpanConstructor || Kind is not ProviderKind.ReflectionNoEmit);
}

public enum ProviderKind
{
    UnRecognized,
    SourceGen,
    ReflectionNoEmit,
    ReflectionEmit
};

public sealed class SourceGenProviderUnderTest(SourceGenTypeShapeProvider sourceGenProvider) : ProviderUnderTest
{
    public static SourceGenProviderUnderTest Default { get; } = new(Witness.ShapeProvider);

    public override ProviderKind Kind => ProviderKind.SourceGen;
    public override bool ResolvesNullableAnnotations => true;
    public override ITypeShapeProvider Provider => sourceGenProvider;
    public override ITypeShape ResolveShape(ITestCase testCase) => testCase.DefaultShape;
}

public sealed class RefectionProviderUnderTest(ReflectionTypeShapeProviderOptions options) : ProviderUnderTest
{
    public static RefectionProviderUnderTest Emit { get; } = new(new() { UseReflectionEmit = true });
    public static RefectionProviderUnderTest NoEmit { get; } = new(new() { UseReflectionEmit = false });

    public override ITypeShapeProvider Provider { get; } = ReflectionTypeShapeProvider.Create(options);
    public override ProviderKind Kind => options.UseReflectionEmit ? ProviderKind.ReflectionEmit : ProviderKind.ReflectionNoEmit;
    public override bool ResolvesNullableAnnotations => ReflectionHelpers.IsNullabilityInfoContextSupported;
    public override ITypeShape ResolveShape(ITestCase testCase) => Provider.Resolve(testCase.Type);
}