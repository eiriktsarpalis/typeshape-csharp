using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Numerics;
using TypeShape.Abstractions;

namespace TypeShape.Examples.ConfigurationBinder;

/// <summary>Defines an <see cref="IConfiguration"/> binder build on top of TypeShape.</summary>
public static class TypeShapeConfigurationBinder
{
    /// <summary>
    /// Builds a configuration binder delegate instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the binder.</typeparam>
    /// <param name="shape">The shape instance guiding binder construction.</param>
    /// <returns>A configuration binder delegate.</returns>
    public static Func<IConfiguration, T?> Create<T>(ITypeShape<T> shape) => 
        new Builder().CreateBinder(shape);

    /// <summary>
    /// Builds a configuration binder delegate instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the binder.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding binder construction.</param>
    /// <returns>A configuration binder delegate.</returns>
    public static Func<IConfiguration, T?> Create<T>(ITypeShapeProvider shapeProvider) =>
        Create(shapeProvider.Resolve<T>());

    /// <summary>
    /// Binds an <see cref="IConfiguration"/> to the specified type using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type to which to bind the configuration.</typeparam>
    /// <param name="configuration">The instance providing the configuration.</param>
    /// <returns>An instance of <typeparamref name="T"/> that binds the configuration.</returns>
    public static T? Get<T>(IConfiguration configuration) where T : IShapeable<T>
        => ConfigurationBinderCache<T, T>.Value(configuration);

    /// <summary>
    /// Binds an <see cref="IConfiguration"/> to the specified type using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type to which to bind the configuration.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="configuration">The instance providing the configuration.</param>
    /// <returns>An instance of <typeparamref name="T"/> that binds the configuration.</returns>
    public static T? Get<T, TProvider>(IConfiguration configuration) where TProvider : IShapeable<T>
        => ConfigurationBinderCache<T, TProvider>.Value(configuration);

    private static class ConfigurationBinderCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Func<IConfiguration, T?> Value => s_value ??= Create(TProvider.GetShape());
        private static Func<IConfiguration, T?>? s_value;
    }
    
    private sealed class Builder : TypeShapeVisitor
    {
        private delegate void PropertyBinder<T>(ref T obj, IConfigurationSection section);
        private static readonly Dictionary<Type, object> s_builtInParsers = new(GetBuiltInParsers());
        private readonly TypeDictionary _cache = new();

        public Func<IConfiguration, T?> CreateBinder<T>(ITypeShape<T> shape)
        {
            if (s_builtInParsers.TryGetValue(typeof(T), out object? stringParser))
            {
                return CreateValueBinder((Func<string, T>)stringParser!);
            }

            return _cache.GetOrAdd<Func<IConfiguration, T?>>(shape, visitor: this,
                delayedValueFactory: self => configuration => self.Result(configuration));
        }
        
        public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            IConstructorShape? ctorShape = objectShape.GetConstructor();
            return ctorShape != null
                ? ctorShape.Accept(this) 
                : throw new NotSupportedException(typeof(T).ToString());
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
        {
            if (constructorShape.ParameterCount == 0)
            {
                Func<TDeclaringType> defaultCtor = constructorShape.GetDefaultConstructor();
                (string Name, PropertyBinder<TDeclaringType> Binder)[] propertyBinders = constructorShape.DeclaringType
                    .GetProperties()
                    .Where(prop => prop.HasSetter)
                    .Select(prop => (prop.Name, (PropertyBinder<TDeclaringType>)prop.Accept(this)!))
                    .ToArray();

                return new Func<IConfiguration, TDeclaringType?>(configuration =>
                {
                    if (IsNullConfiguration(configuration))
                    {
                        return default;
                    }
                    
                    TDeclaringType obj = defaultCtor();
                    foreach ((string name, PropertyBinder<TDeclaringType> binder) in propertyBinders)
                    {
                        if (configuration.GetSection(name) is { } section)
                        {
                            binder(ref obj, section);
                        }
                    }

                    return obj;
                });
            }
            else
            {
                Func<TArgumentState> argStateCtor = constructorShape.GetArgumentStateConstructor();
                Constructor<TArgumentState, TDeclaringType> paramCtor = constructorShape.GetParameterizedConstructor();
                (string Name, bool IsRequired, PropertyBinder<TArgumentState> Binder)[] paramBinders = constructorShape
                    .GetParameters()
                    .Select(param => (param.Name, param.IsRequired, (PropertyBinder<TArgumentState>)param.Accept(this)!))
                    .ToArray();

                return new Func<IConfiguration, TDeclaringType?>(configuration =>
                {
                    if (IsNullConfiguration(configuration))
                    {
                        return default;
                    }
                    
                    TArgumentState argState = argStateCtor();
                    foreach ((string name, bool isRequired, PropertyBinder<TArgumentState> binder) in paramBinders)
                    {
                        if (configuration.GetSection(name) is { } section)
                        {
                            binder(ref argState, section);
                        }
                        else if (isRequired)
                        {
                            Throw(name);
                            static void Throw(string name) => throw new InvalidOperationException($"Missing required configuration key '{name}'.");
                        }
                    }

                    return paramCtor(ref argState);
                });
            }
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        {
            Func<IConfiguration, TPropertyType?> propertyTypeBinder = CreateBinder(propertyShape.PropertyType);
            Setter<TDeclaringType, TPropertyType> setter = propertyShape.GetSetter();
            return new PropertyBinder<TDeclaringType>((ref TDeclaringType obj, IConfigurationSection section) => setter(ref obj, propertyTypeBinder(section)!));
        }

        public override object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
        {
            Func<IConfiguration, TParameterType?> parameterTypeBinder = CreateBinder(parameterShape.ParameterType);
            Setter<TArgumentState, TParameterType> setter = parameterShape.GetSetter();
            return new PropertyBinder<TArgumentState>((ref TArgumentState argState, IConfigurationSection section) => setter(ref argState, parameterTypeBinder(section)!));
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
        {
            Func<IConfiguration, TElement> elementBinder = CreateBinder(enumerableShape.ElementType)!;
            switch (enumerableShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    Func<TEnumerable> defaultCtor = enumerableShape.GetDefaultConstructor();
                    Setter<TEnumerable, TElement> addElement = enumerableShape.GetAddElement();
                    return new Func<IConfiguration, TEnumerable?>(configuration =>
                    {
                        if (IsNullConfiguration(configuration))
                        {
                            return default;
                        }
                        
                        TEnumerable enumerable = defaultCtor();
                        foreach (IConfigurationSection child in configuration.GetChildren())
                        {
                            TElement element = elementBinder(child);
                            addElement(ref enumerable, element);
                        }

                        return enumerable;
                    });
                
                case CollectionConstructionStrategy.Span:
                    SpanConstructor<TElement, TEnumerable> spanCtor = enumerableShape.GetSpanConstructor();
                    return new Func<IConfiguration, TEnumerable?>(configuration =>
                    {
                        if (IsNullConfiguration(configuration))
                        {
                            return default;
                        }
                        
                        using var buffer = new PooledList<TElement>();
                        foreach (IConfigurationSection child in configuration.GetChildren())
                        {
                            TElement element = elementBinder(child);
                            buffer.Add(element);
                        }

                        return spanCtor(buffer.AsSpan());
                    });
                
                case CollectionConstructionStrategy.Enumerable:
                    Func<IEnumerable<TElement>, TEnumerable> enumerableCtor = enumerableShape.GetEnumerableConstructor();
                    return new Func<IConfiguration, TEnumerable?>(configuration =>
                    {
                        if (IsNullConfiguration(configuration))
                        {
                            return default;
                        }
                        
                        var buffer = new List<TElement>();
                        foreach (IConfigurationSection child in configuration.GetChildren())
                        {
                            TElement element = elementBinder(child);
                            buffer.Add(element);
                        }

                        return enumerableCtor(buffer);
                    });
                
                default:
                    throw new NotSupportedException(typeof(TEnumerable).ToString());
            }
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
        {
            if (!s_builtInParsers.TryGetValue(typeof(TKey), out object? parser))
            {
                throw new NotSupportedException($"Dictionary keys of type '{typeof(TKey)}' are not supported.");
            }

            Func<IConfigurationSection, TKey> keyBinder = CreateKeyBinder((Func<string, TKey>)parser!);
            Func<IConfiguration, TValue> valueBinder = CreateBinder(dictionaryShape.ValueType)!;

            switch (dictionaryShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    Func<TDictionary> defaultCtor = dictionaryShape.GetDefaultConstructor();
                    Setter<TDictionary, KeyValuePair<TKey, TValue>> addEntry = dictionaryShape.GetAddKeyValuePair();
                    return new Func<IConfiguration, TDictionary?>(configuration =>
                    {
                        if (IsNullConfiguration(configuration))
                        {
                            return default;
                        }
                        
                        TDictionary dict = defaultCtor();
                        foreach (IConfigurationSection section in configuration.GetChildren())
                        {
                            KeyValuePair<TKey, TValue> entry = new(keyBinder(section), valueBinder(section));
                            addEntry(ref dict, entry);
                        }

                        return dict;
                    });
                
                case CollectionConstructionStrategy.Span:
                    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> spanCtor = dictionaryShape.GetSpanConstructor();
                    return new Func<IConfiguration, TDictionary?>(configuration =>
                    {
                        if (IsNullConfiguration(configuration))
                        {
                            return default;
                        }
                        
                        using var buffer = new PooledList<KeyValuePair<TKey, TValue>>();
                        foreach (IConfigurationSection section in configuration.GetChildren())
                        {
                            KeyValuePair<TKey, TValue> entry = new(keyBinder(section), valueBinder(section));
                            buffer.Add(entry);
                        }

                        return spanCtor(buffer.AsSpan());
                    });
                
                case CollectionConstructionStrategy.Enumerable:
                    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> enumerableCtor = dictionaryShape.GetEnumerableConstructor();
                    return new Func<IConfiguration, TDictionary?>(configuration =>
                    {
                        if (IsNullConfiguration(configuration))
                        {
                            return default;
                        }
                        
                        var buffer = new List<KeyValuePair<TKey, TValue>>();
                        foreach (IConfigurationSection section in configuration.GetChildren())
                        {
                            KeyValuePair<TKey, TValue> entry = new(keyBinder(section), valueBinder(section));
                            buffer.Add(entry);
                        }

                        return enumerableCtor(buffer);
                    });
                
                default:
                    throw new NotSupportedException(typeof(TDictionary).ToString());
            }
        }

        public override object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state = null)
        {
            Func<IConfiguration, T> elementBinder = CreateBinder(nullableShape.ElementType);
            return new Func<IConfiguration, T?>(configuration => IsNullConfiguration(configuration) ? null : elementBinder(configuration));
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
        {
            return CreateValueBinder(Enum.Parse<TEnum>);
        }

        private static IEnumerable<KeyValuePair<Type, object>> GetBuiltInParsers()
        {
            yield return Create(bool.Parse);
            yield return Create(text => byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => ushort.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => uint.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => UInt128.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => sbyte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => Int128.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => BigInteger.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
            yield return Create(text => Half.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
            yield return Create(text => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
            yield return Create(text => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
            yield return Create(text => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
            yield return Create(text => string.IsNullOrEmpty(text) ? null : text);
            yield return Create(char.Parse);
            yield return Create(text => System.Text.Rune.GetRuneAt(text, 0));
            yield return Create(text => TimeSpan.Parse(text, CultureInfo.InvariantCulture));
            yield return Create(text => DateTime.Parse(text, CultureInfo.InvariantCulture));
            yield return Create(text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture));
            yield return Create(text => DateOnly.Parse(text, CultureInfo.InvariantCulture));
            yield return Create(text => TimeOnly.Parse(text, CultureInfo.InvariantCulture));
            yield return Create(text => Guid.Parse(text, CultureInfo.InvariantCulture));
            yield return Create(text => text is "" ? null : new Uri(text, UriKind.RelativeOrAbsolute));
            yield return Create(text => text is "" ? null : Version.Parse(text));
            yield return Create(text => text is "" ? null : Convert.FromBase64String(text));

            yield return Create<object?>(text =>
                text is null ? new object() :
                text is "" ? null :
                bool.TryParse(text, out bool boolResult) ? boolResult :
                int.TryParse(text, out int intResult) ? intResult :
                double.TryParse(text, out double doubleResult) ? doubleResult :
                text);
            
            static KeyValuePair<Type, object> Create<T>(Func<string, T> parser)
                => new(typeof(T), parser);
        }

        private static Func<IConfigurationSection, T> CreateKeyBinder<T>(Func<string, T> parser)
        {
            return configuration =>
            {
                try
                {
                    return parser(configuration.Key);    
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Failed to convert configuration key at '{configuration.Path}' to type '{typeof(T)}'.", e);
                }
            };
        }

        private static Func<IConfiguration, T> CreateValueBinder<T>(Func<string, T> parser)
        {
            return configuration =>
            {
                if (configuration is not IConfigurationSection section)
                {
                    throw new InvalidOperationException();
                }

                try
                {
                    return parser(section.Value!);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Failed to convert configuration value at '{section.Path}' to type '{typeof(T)}'.", e);
                }
            };
        }

        private static bool IsNullConfiguration(IConfiguration configuration) =>
            // https://github.com/dotnet/runtime/issues/36510
            configuration is IConfigurationSection { Value: "" } &&
            !configuration.GetChildren().Any();
    }
}