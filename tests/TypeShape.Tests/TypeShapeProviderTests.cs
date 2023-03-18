using System.Reflection;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class TypeShapeProviderTests
{
    protected abstract ITypeShapeProvider Provider { get; }
    protected abstract bool SupportsNonPublicMembers { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void ReturnsExpectedAttributeProviders<T>(T value)
    {
        IType<T> shape = Provider.GetShape<T>()!;
        _ = value; // not used here

        Assert.Equal(typeof(T), shape.AttributeProvider);

        foreach (IProperty property in shape.GetProperties(nonPublic: SupportsNonPublicMembers, includeFields: true))
        {
            ICustomAttributeProvider? attributeProvider = property.AttributeProvider;
            Assert.NotNull(attributeProvider);

            if (property.IsField)
            {
                FieldInfo fieldInfo = Assert.IsAssignableFrom<FieldInfo>(attributeProvider);
                Assert.Equal(typeof(T), fieldInfo.ReflectedType);
                Assert.Equal(property.Name, fieldInfo.Name);
                Assert.Equal(property.PropertyType.Type, fieldInfo.FieldType);
            }
            else
            {
                PropertyInfo propertyInfo = Assert.IsAssignableFrom<PropertyInfo>(attributeProvider);
                Assert.Equal(typeof(T), propertyInfo.ReflectedType);
                Assert.Equal(property.Name, propertyInfo.Name);
                Assert.Equal(property.PropertyType.Type, propertyInfo.PropertyType);
                Assert.True(!property.HasGetter || propertyInfo.CanRead);
                Assert.True(!property.HasSetter || propertyInfo.CanWrite);
            }
        }

        foreach (IConstructor constructor in shape.GetConstructors(nonPublic: SupportsNonPublicMembers))
        {
            ICustomAttributeProvider? attributeProvider = constructor.AttributeProvider;
            
            if (attributeProvider is null)
            {
                Assert.True(typeof(T).IsValueType);
                Assert.Equal(0, constructor.ParameterCount);
                Assert.Empty(constructor.GetParameters());
                continue;
            }

            ConstructorInfo ctorInfo = Assert.IsAssignableFrom<ConstructorInfo>(attributeProvider);
            Assert.Equal(typeof(T), ctorInfo.DeclaringType);
            Assert.Equal(constructor.ParameterCount, constructor.ParameterCount);

            ParameterInfo[] parameters = ctorInfo.GetParameters();
            foreach ((IConstructorParameter ctorParam, ParameterInfo paramInfo) in constructor.GetParameters().Zip(parameters))
            {
                Assert.Equal(ctorParam.ParameterType.Type, paramInfo.ParameterType);
                Assert.Equal(paramInfo, ctorParam.AttributeProvider);
            }
        }
    }
}

public sealed class TypeShapeProviderTests_Reflection : TypeShapeProviderTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
    protected override bool SupportsNonPublicMembers => true;
}

public sealed class TypeShapeProviderTests_ReflectionEmit : TypeShapeProviderTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
    protected override bool SupportsNonPublicMembers => true;
}

public sealed class TypeShapeProviderTests_SourceGen : TypeShapeProviderTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
    protected override bool SupportsNonPublicMembers => false;
}

