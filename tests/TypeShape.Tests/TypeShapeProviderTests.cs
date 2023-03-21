using System.Reflection;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class TypeShapeProviderTests
{
    protected abstract ITypeShapeProvider Provider { get; }
    protected abstract bool SupportsNonPublicMembers { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ReturnsExpectedAttributeProviders<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        IType<T> shape = Provider.GetShape<T>()!;

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
                Assert.True(propertyInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                Assert.Equal(property.Name, propertyInfo.Name);
                Assert.Equal(property.PropertyType.Type, propertyInfo.PropertyType);
                Assert.True(!property.HasGetter || propertyInfo.CanRead);
                Assert.True(!property.HasSetter || propertyInfo.CanWrite);
            }
        }

        foreach (IConstructor constructor in shape.GetConstructors(nonPublic: SupportsNonPublicMembers))
        {
            ICustomAttributeProvider? attributeProvider = constructor.AttributeProvider;

            ParameterInfo[] parameters;
            if (attributeProvider is null)
            {
                Assert.True(typeof(T).IsValueType);
                parameters = Array.Empty<ParameterInfo>();
            }
            else
            {
                ConstructorInfo ctorInfo = Assert.IsAssignableFrom<ConstructorInfo>(attributeProvider);
                Assert.Equal(typeof(T), ctorInfo.DeclaringType);
                Assert.Equal(constructor.ParameterCount, constructor.ParameterCount);
                parameters = ctorInfo.GetParameters();
            }

            int i = 0;
            foreach (IConstructorParameter ctorParam in constructor.GetParameters())
            {
                if (i < parameters.Length)
                {
                    ParameterInfo actualParameter = parameters[i];
                    Assert.Equal(actualParameter.Position, ctorParam.Position);
                    Assert.Equal(actualParameter.ParameterType, ctorParam.ParameterType.Type);
                    Assert.Equal(actualParameter.Name, ctorParam.Name);
                    Assert.Equal(actualParameter.HasDefaultValue, ctorParam.HasDefaultValue);

                    ParameterInfo paramInfo = Assert.IsAssignableFrom<ParameterInfo>(ctorParam.AttributeProvider);
                    Assert.Equal(actualParameter.Position, paramInfo.Position);
                    Assert.Equal(actualParameter.Name, paramInfo.Name);
                    Assert.Equal(actualParameter.ParameterType, paramInfo.ParameterType);
                }
                else
                {
                    MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(ctorParam.AttributeProvider);

                    Assert.Equal(typeof(T), memberInfo.DeclaringType);
                    Assert.Equal(memberInfo.Name, ctorParam.Name);
                    Assert.False(ctorParam.HasDefaultValue);
                    Assert.Equal(i, ctorParam.Position);
                    Assert.True(memberInfo is PropertyInfo or FieldInfo);

                    if (memberInfo is PropertyInfo p)
                    {
                        Assert.Equal(p.PropertyType, ctorParam.ParameterType.Type);
                        Assert.NotNull(p.SetMethod);
                    }
                    else if (memberInfo is FieldInfo f)
                    {
                        Assert.Equal(f.FieldType, ctorParam.ParameterType.Type);
                        Assert.False(f.IsInitOnly);
                    }
                }

                i++;
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

