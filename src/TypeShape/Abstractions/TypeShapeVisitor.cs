namespace TypeShape.Abstractions;

/// <summary>
/// Provides a visitor for strongly-typed traversal of .NET types.
/// </summary>
public abstract class TypeShapeVisitor : ITypeShapeVisitor
{
    /// <summary>
    /// Visits an <see cref="ITypeShape{T}"/> representing a simple type or object.
    /// </summary>
    /// <typeparam name="T">The type represented by the shape instance.</typeparam>
    /// <param name="typeShape">The type shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitType<T>(ITypeShape<T> typeShape, object? state = null)
        => ThrowNotImplemented(nameof(VisitType));

    /// <summary>
    /// Visits an <see cref="IPropertyShape{TDeclaringType, TPropertyType}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type of the visited property.</typeparam>
    /// <typeparam name="TPropertyType">The property type of the visited property.</typeparam>
    /// <param name="propertyShape">The property shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        => ThrowNotImplemented(nameof(VisitProperty));

    /// <summary>
    /// Visits an <see cref="IConstructorParameterShape{TDeclaringType, TArgumentState}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type of the visited constructor.</typeparam>
    /// <typeparam name="TArgumentState">The constructor argument state type used for aggregating constructor arguments.</typeparam>
    /// <param name="constructorShape">The constructor shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
        => ThrowNotImplemented(nameof(VisitConstructor));

    /// <summary>
    /// Visits an <see cref="IConstructorParameterShape{TArgumentState, TParameterType}"/> instance.
    /// </summary>
    /// <typeparam name="TArgumentState">The constructor argument state type used for aggregating constructor arguments.</typeparam>
    /// <typeparam name="TParameterType">The type of the visited constructor parameter.</typeparam>
    /// <param name="parameterShape">The parameter shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
        => ThrowNotImplemented(nameof(VisitConstructorParameter));

    /// <summary>
    /// Visits an <see cref="IEnumTypeShape{TEnum,TUnderlying}"/> instance.
    /// </summary>
    /// <typeparam name="TEnum">The type of visited enum.</typeparam>
    /// <typeparam name="TUnderlying">The underlying type used by the enum.</typeparam>
    /// <param name="enumTypeShape">The enum shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumTypeShape, object? state = null)
        where TEnum : struct, Enum
        => ThrowNotImplemented(nameof(VisitEnum));

    /// <summary>
    /// Visits an <see cref="INullableTypeShape{T}"/> instance representing the <see cref="Nullable{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The element type of the visited nullable.</typeparam>
    /// <param name="nullableTypeShape">The nullable shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitNullable<T>(INullableTypeShape<T> nullableTypeShape, object? state = null)
        where T : struct
        => ThrowNotImplemented(nameof(VisitNullable));

    /// <summary>
    /// Visits an <see cref="IEnumerableTypeShape{TEnumerable,TElement}"/> instance representing an enumerable type.
    /// </summary>
    /// <typeparam name="TEnumerable">The type of the visited enumerable.</typeparam>
    /// <typeparam name="TElement">The element type of the visited enumerable.</typeparam>
    /// <param name="enumerableTypeShape">The enumerable shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableTypeShape, object? state = null)
        => ThrowNotImplemented(nameof(VisitEnumerable));

    /// <summary>
    /// Visits an <see cref="IDictionaryShape{TDictionary, TKey, TValue}"/> instance representing a dictionary type.
    /// </summary>
    /// <typeparam name="TDictionary">The type of the visited dictionary.</typeparam>
    /// <typeparam name="TKey">The key type of the visited dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the visited dictionary.</typeparam>
    /// <param name="dictionaryShape">The dictionary shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
        where TKey : notnull
        => ThrowNotImplemented(nameof(VisitEnumerable));

    private object? ThrowNotImplemented(string methodName)
        => throw new NotImplementedException($"The visitor method {GetType().Name}.{methodName} has not been implemented.");
}
