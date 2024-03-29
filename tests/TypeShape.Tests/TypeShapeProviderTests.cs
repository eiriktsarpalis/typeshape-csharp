﻿using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TypeShape.Applications.RandomGenerator;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class TypeShapeProviderTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeShapeReportsExpectedInfo<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();

        Assert.NotNull(shape);
        Assert.Same(Provider, shape.Provider);
        Assert.Equal(typeof(T), shape.Type);
        Assert.Equal(typeof(T), shape.AttributeProvider);
        Assert.Equal(typeof(T).IsRecord(), shape.IsRecord);

        TypeKind expectedKind = GetExpectedTypeKind(testCase.Value);
        Assert.Equal(expectedKind, shape.Kind);

        static TypeKind GetExpectedTypeKind(T value)
        {
            if (typeof(T).IsEnum)
            {
                return TypeKind.Enum;
            }
            else if (typeof(T).IsValueType && default(T) is null)
            {
                return TypeKind.Nullable;
            }

            if (value is IEnumerable && value is not string)
            {
                return typeof(T).GetDictionaryKeyValueTypes() != null
                    ? TypeKind.Dictionary
                    : TypeKind.Enumerable;
            }

            if (typeof(T).IsMemoryType(out _, out _))
            {
                return TypeKind.Enumerable;
            }

            if (IsSimpleValue())
            {
                return TypeKind.None;
            }

            return TypeKind.Object;
        }

        static bool IsSimpleValue()
        {
            Type type = typeof(T);
            return type.IsPrimitive ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(Int128) ||
                type == typeof(UInt128) ||
                type == typeof(Half) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(DateOnly) ||
                type == typeof(TimeSpan) ||
                type == typeof(TimeOnly) ||
                type == typeof(Guid) ||
                type == typeof(Version) ||
                type == typeof(Uri) ||
                type == typeof(Rune) ||
                typeof(MemberInfo).IsAssignableFrom(type) ||
                typeof(Delegate).IsAssignableFrom(type);
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetProperties<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        var visitor = new PropertyTestVisitor();
        foreach (IPropertyShape property in shape.GetProperties())
        {
            Assert.Equal(typeof(T), property.DeclaringType.Type);
            property.Accept(visitor, testCase.Value);
        }
    }

    private sealed class PropertyTestVisitor : TypeShapeVisitor
    {
        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            TDeclaringType obj = (TDeclaringType)state!;
            TPropertyType propertyType = default!;

            if (property.HasGetter)
            {
                var getter = property.GetGetter();
                propertyType = getter(ref obj);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => property.GetGetter());
            }

            if (property.HasSetter)
            {
                var setter = property.GetSetter();
                setter(ref obj, propertyType);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => property.GetSetter());
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetConstructors<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        var visitor = new ConstructorTestVisitor();
        foreach (IConstructorShape ctor in shape.GetConstructors())
        {
            Assert.Equal(typeof(T), ctor.DeclaringType.Type);
            ctor.Accept(visitor, typeof(T));
        }
    }

    private sealed class ConstructorTestVisitor : TypeShapeVisitor
    {
        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var expectedType = (Type)state!;
            Assert.Equal(typeof(TDeclaringType), expectedType);

            int parameterCount = constructor.ParameterCount;
            IConstructorParameterShape[] parameters = constructor.GetParameters().ToArray();
            Assert.Equal(parameterCount, parameters.Length);

            if (parameterCount == 0)
            {
                var defaultCtor = constructor.GetDefaultConstructor();
                TDeclaringType defaultValue = defaultCtor();
                Assert.NotNull(defaultValue);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => constructor.GetDefaultConstructor());
            }

            int i = 0;
            TArgumentState argumentState = constructor.GetArgumentStateConstructor().Invoke();
            foreach (IConstructorParameterShape parameter in parameters)
            {
                Assert.Equal(i++, parameter.Position);
                argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
            }

            var parameterizedCtor = constructor.GetParameterizedConstructor();
            Assert.NotNull(parameterizedCtor);

            if (typeof(TDeclaringType).Assembly == Assembly.GetExecutingAssembly())
            {
                TDeclaringType value = parameterizedCtor.Invoke(ref argumentState);
                Assert.NotNull(value);
            }
            return null;
        }

        public override object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            var argState = (TArgumentState)state!;
            var setter = parameter.GetSetter();

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            setter(ref argState, value!);
            return argState;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetEnumType<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        if (shape.Kind.HasFlag(TypeKind.Enum))
        {
            IEnumShape enumType = shape.GetEnumShape();
            Assert.Equal(typeof(T), enumType.Type.Type);
            Assert.Equal(typeof(T).GetEnumUnderlyingType(), enumType.UnderlyingType.Type);
            var visitor = new EnumTestVisitor();
            enumType.Accept(visitor, typeof(T));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => shape.GetEnumShape());
        }
    }

    private sealed class EnumTestVisitor : TypeShapeVisitor
    {
        public override object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumType, object? state)
        {
            var type = (Type)state!;
            Assert.Equal(typeof(TEnum), type);
            Assert.Equal(typeof(TUnderlying), type.GetEnumUnderlyingType());
            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetNullableType<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        if (shape.Kind.HasFlag(TypeKind.Nullable))
        {
            INullableShape nullableType = shape.GetNullableShape();
            Assert.Equal(typeof(T).GetGenericArguments()[0], nullableType.ElementType.Type);
            var visitor = new NullableTestVisitor();
            nullableType.Accept(visitor, typeof(T));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => shape.GetNullableShape());
        }
    }

    private sealed class NullableTestVisitor : TypeShapeVisitor
    {
        public override object? VisitNullable<T>(INullableShape<T> nullable, object? state) where T : struct
        {
            var type = (Type)state!;
            Assert.Equal(typeof(T?), type);
            Assert.Equal(typeof(T), nullable.ElementType.Type);
            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetDictionaryType<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        if (shape.Kind.HasFlag(TypeKind.Dictionary))
        {
            IDictionaryShape dictionaryType = shape.GetDictionaryShape();
            Assert.Equal(typeof(T), dictionaryType.Type.Type);

            Type[]? keyValueTypes = typeof(T).GetDictionaryKeyValueTypes();
            Assert.NotNull(keyValueTypes);
            Assert.Equal(keyValueTypes[0], dictionaryType.KeyType.Type);
            Assert.Equal(keyValueTypes[1], dictionaryType.ValueType.Type);

            var visitor = new DictionaryTestVisitor();
            dictionaryType.Accept(visitor, null);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => shape.GetDictionaryShape());
        }
    }

    private sealed class DictionaryTestVisitor : TypeShapeVisitor
    {
        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            TDictionary dictionary;
            RandomGenerator<TKey> keyGenerator = RandomGenerator.Create((ITypeShape<TKey>)dictionaryShape.KeyType);
            var getter = dictionaryShape.GetGetDictionary();

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                var defaultCtor = dictionaryShape.GetDefaultConstructor();
                var adder = dictionaryShape.GetAddKeyValuePair();

                dictionary = defaultCtor();
                Assert.Empty(getter(dictionary));

                TKey newKey = keyGenerator.GenerateValue(size: 1000, seed: 42);
                adder(ref dictionary, new(newKey, default!));
                Assert.Single(getter(dictionary));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetDefaultConstructor());
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetAddKeyValuePair());
            }

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Enumerable)
            {
                var enumerableCtor = dictionaryShape.GetEnumerableConstructor();
                var values = keyGenerator.GenerateValues(seed: 42)
                    .Select(k => new KeyValuePair<TKey, TValue>(k, default!))
                    .Take(10);

                dictionary = enumerableCtor(values);
                Assert.Equal(10, getter(dictionary).Count);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetEnumerableConstructor());
            }

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Span)
            {
                var spanCtor = dictionaryShape.GetSpanConstructor();
                var values = keyGenerator.GenerateValues(seed: 42)
                    .Select(k => new KeyValuePair<TKey, TValue>(k, default!))
                    .Take(10)
                    .ToArray();

                dictionary = spanCtor(values);
                Assert.Equal(10, getter(dictionary).Count);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetSpanConstructor());
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetEnumerableType<T>(TestCase<T> testCase)
    {
        _ = testCase; // not used here
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        if (shape.Kind.HasFlag(TypeKind.Enumerable))
        {
            IEnumerableShape enumerableType = shape.GetEnumerableShape();
            Assert.Equal(typeof(T), enumerableType.Type.Type);

            if (typeof(T).GetCompatibleGenericInterface(typeof(IEnumerable<>)) is { } enumerableImplementation)
            {
                Assert.Equal(enumerableImplementation.GetGenericArguments()[0], enumerableType.ElementType.Type);
                Assert.Equal(1, enumerableType.Rank);
            }
            else if (typeof(T).IsArray)
            {
                Assert.Equal(typeof(T).GetElementType(), enumerableType.ElementType.Type);
                Assert.Equal(typeof(T).GetArrayRank(), enumerableType.Rank);
            }
            else if (typeof(IEnumerable).IsAssignableFrom(typeof(T)))
            {
                Assert.Equal(typeof(object), enumerableType.ElementType.Type);
                Assert.Equal(1, enumerableType.Rank);
            }
            else if (typeof(T).IsMemoryType(out Type? elementType, out _))
            {
                Assert.Equal(elementType, enumerableType.ElementType.Type);
                Assert.Equal(1, enumerableType.Rank);
            }
            else
            {
                Assert.Fail($"Unexpected enumerable type: {typeof(T)}");
            }

            var visitor = new EnumerableTestVisitor();
            enumerableType.Accept(visitor, null);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => shape.GetEnumerableShape());
        }
    }

    private sealed class EnumerableTestVisitor : TypeShapeVisitor
    {
        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            TEnumerable enumerable;
            RandomGenerator<TElement> elementGenerator = RandomGenerator.Create((ITypeShape<TElement>)enumerableShape.ElementType);
            var getter = enumerableShape.GetGetEnumerable();

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                var defaultCtor = enumerableShape.GetDefaultConstructor();
                var adder = enumerableShape.GetAddElement();

                enumerable = defaultCtor();
                Assert.Empty(getter(enumerable));

                TElement newElement = elementGenerator.GenerateValue(size: 1000, seed: 42);
                adder(ref enumerable, newElement);
                Assert.Single(getter(enumerable));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetDefaultConstructor());
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetAddElement());
            }

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Enumerable)
            {
                var enumerableCtor = enumerableShape.GetEnumerableConstructor();
                var values = elementGenerator.GenerateValues(seed: 42).Take(10);

                enumerable = enumerableCtor(values);
                Assert.Equal(10, getter(enumerable).Count());
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetEnumerableConstructor());
            }

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Span)
            {
                var spanCtor = enumerableShape.GetSpanConstructor();
                var values = elementGenerator.GenerateValues(seed: 42).Take(10).ToArray();

                enumerable = spanCtor(values);
                Assert.Equal(10, getter(enumerable).Count());
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetSpanConstructor());
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ReturnsExpectedAttributeProviders<T>(TestCase<T> testCase)
    {
        if (testCase.IsTuple)
        {
            return; // tuples don't report attribute metadata.
        }

        ITypeShape<T> shape = Provider.GetShape<T>()!;
        Assert.Equal(typeof(T), shape.AttributeProvider);

        foreach (IPropertyShape property in shape.GetProperties())
        {
            ICustomAttributeProvider? attributeProvider = property.AttributeProvider;
            Assert.NotNull(attributeProvider);
            PropertyShapeAttribute? attr = attributeProvider.GetCustomAttributes(inherit: true).OfType<PropertyShapeAttribute>().FirstOrDefault();

            if (property.IsField)
            {
                FieldInfo fieldInfo = Assert.IsAssignableFrom<FieldInfo>(attributeProvider);
                Assert.True(fieldInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                Assert.Equal(attr?.Name ?? fieldInfo.Name, property.Name);
                Assert.Equal(property.PropertyType.Type, fieldInfo.FieldType);
                Assert.True(property.HasGetter);
                Assert.Equal(!fieldInfo.IsInitOnly, property.HasSetter);
                Assert.Equal(fieldInfo.IsPublic, property.IsGetterPublic);
                Assert.Equal(property.HasSetter && fieldInfo.IsPublic, property.IsSetterPublic);
            }
            else
            {
                PropertyInfo propertyInfo = Assert.IsAssignableFrom<PropertyInfo>(attributeProvider);
                Assert.False(propertyInfo.IsOverride());
                Assert.True(propertyInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                Assert.Equal(attr?.Name ?? propertyInfo.Name, property.Name);
                Assert.Equal(property.PropertyType.Type, propertyInfo.PropertyType);
                Assert.True(!property.HasGetter || propertyInfo.CanRead);
                Assert.True(!property.HasSetter || propertyInfo.CanWrite);
                Assert.Equal(property.HasGetter && propertyInfo.GetMethod!.IsPublic, property.IsGetterPublic);
                Assert.Equal(property.HasSetter && propertyInfo.SetMethod!.IsPublic, property.IsSetterPublic);
            }
        }

        foreach (IConstructorShape constructor in shape.GetConstructors())
        {
            ICustomAttributeProvider? attributeProvider = constructor.AttributeProvider;
            if (attributeProvider is null)
            {
                continue;
            }

            MethodBase ctorInfo = Assert.IsAssignableFrom<MethodBase>(attributeProvider);
            Assert.True(ctorInfo is MethodInfo { IsStatic: true } or ConstructorInfo);
            Assert.True(typeof(T).IsAssignableFrom(ctorInfo is MethodInfo m ? m.ReturnType : ctorInfo.DeclaringType));
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            Assert.True(parameters.Length <= constructor.ParameterCount);
            Assert.Equal(ctorInfo.IsPublic, constructor.IsPublic);

            int i = 0;
            foreach (IConstructorParameterShape ctorParam in constructor.GetParameters())
            {
                if (i < parameters.Length)
                {
                    ParameterInfo actualParameter = parameters[i];
                    ParameterShapeAttribute? shapeAttr = actualParameter.GetCustomAttribute<ParameterShapeAttribute>();
                    string expectedName =
                        // 1. parameter attribute name
                        shapeAttr?.Name
                        // 2. property name picked up from matching parameter
                        ?? typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .FirstOrDefault(m => JsonNamingPolicy.CamelCase.ConvertName(m.Name) == actualParameter.Name)
                            ?.GetCustomAttribute<PropertyShapeAttribute>()
                            ?.Name
                        // 3. the actual parameter name.
                        ?? actualParameter.Name!;

                    Assert.Equal(actualParameter.Position, ctorParam.Position);
                    Assert.Equal(actualParameter.ParameterType, ctorParam.ParameterType.Type);
                    Assert.Equal(expectedName, ctorParam.Name);

                    bool hasDefaultValue = actualParameter.TryGetDefaultValueNormalized(out object? defaultValue);
                    Assert.Equal(hasDefaultValue, ctorParam.HasDefaultValue);
                    Assert.Equal(defaultValue, ctorParam.DefaultValue);
                    Assert.Equal(!hasDefaultValue, ctorParam.IsRequired);
                    Assert.Equal(ConstructorParameterKind.ConstructorParameter, ctorParam.Kind);

                    ParameterInfo paramInfo = Assert.IsAssignableFrom<ParameterInfo>(ctorParam.AttributeProvider);
                    Assert.Equal(actualParameter.Position, paramInfo.Position);
                    Assert.Equal(actualParameter.Name, paramInfo.Name);
                    Assert.Equal(actualParameter.ParameterType, paramInfo.ParameterType);
                }
                else
                {
                    MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(ctorParam.AttributeProvider);
                    PropertyShapeAttribute? attr = memberInfo.GetCustomAttribute<PropertyShapeAttribute>();

                    Assert.True(memberInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                    Assert.Equal(attr?.Name ?? memberInfo.Name, ctorParam.Name);
                    Assert.False(ctorParam.HasDefaultValue);
                    Assert.Equal(i, ctorParam.Position);
                    Assert.False(ctorParam.HasDefaultValue);
                    Assert.Null(ctorParam.DefaultValue);
                    Assert.Equal(memberInfo.GetCustomAttribute<RequiredMemberAttribute>() != null, ctorParam.IsRequired);
                    Assert.Equal(memberInfo is FieldInfo ? ConstructorParameterKind.FieldInitializer : ConstructorParameterKind.PropertyInitializer, ctorParam.Kind);

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

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ReturnsExpectedNullabilityAnnotations<T>(TestCase<T> testCase)
    {
        if (testCase.IsTuple)
        {
            return; // tuples don't report attribute metadata.
        }

        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);

        foreach (IPropertyShape property in shape.GetProperties())
        {
            MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(property.AttributeProvider);

            memberInfo.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
            Assert.Equal(property.HasGetter && isGetterNonNullable, property.IsGetterNonNullable);
            Assert.Equal(property.HasSetter && isSetterNonNullable, property.IsSetterNonNullable);
        }

        foreach (IConstructorShape constructor in shape.GetConstructors())
        {
            ICustomAttributeProvider? attributeProvider = constructor.AttributeProvider;
            if (attributeProvider is null)
            {
                continue;
            }

            MethodBase ctorInfo = Assert.IsAssignableFrom<MethodBase>(attributeProvider);
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            Assert.True(parameters.Length <= constructor.ParameterCount);

            foreach (IConstructorParameterShape ctorParam in constructor.GetParameters())
            {
                if (ctorParam.AttributeProvider is ParameterInfo pInfo)
                {
                    bool isNonNullableReferenceType = pInfo.IsNonNullableAnnotation();
                    Assert.Equal(isNonNullableReferenceType, ctorParam.IsNonNullable);
                }
                else
                {
                    MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(ctorParam.AttributeProvider);
                    memberInfo.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
                    Assert.Equal(isSetterNonNullable, ctorParam.IsNonNullable);
                }
            }
        }
    }
}

public static class ReflectionHelpers
{
    public static Type[]? GetDictionaryKeyValueTypes(this Type type)
    {
        if (type.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>)) is { } rod)
        {
            return rod.GetGenericArguments();
        }

        if (type.GetCompatibleGenericInterface(typeof(IDictionary<,>)) is { } d)
        {
            return d.GetGenericArguments();
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return [typeof(object), typeof(object)];
        }

        return null;
    }

    public static Type? GetCompatibleGenericInterface(this Type type, Type genericInterface)
    {
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface)
        {
            return type;
        }

        foreach (Type interfaceTy in type.GetInterfaces())
        {
            if (interfaceTy.IsGenericType && interfaceTy.GetGenericTypeDefinition() == genericInterface)
            {
                return interfaceTy;
            }
        }

        return null;
    }

    public static bool IsImmutableArray(this Type type)
        => type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);
}

public sealed class TypeShapeProviderTests_Reflection : TypeShapeProviderTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class TypeShapeProviderTests_ReflectionEmit : TypeShapeProviderTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class TypeShapeProviderTests_SourceGen : TypeShapeProviderTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}
