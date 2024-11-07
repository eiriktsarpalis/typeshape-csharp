namespace PolyType.Abstractions;

/// <summary>
/// Delegate representing a property/field getter.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type from which to get a value.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying getter.</typeparam>
/// <param name="obj">The instance from which to get the value.</param>
/// <returns>The value returned by the getter.</returns>
public delegate TPropertyType Getter<TDeclaringType, TPropertyType>(ref TDeclaringType obj);

/// <summary>
/// Delegate representing a property/field setter.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type on which to set a value.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying setter.</typeparam>
/// <param name="obj">The instance on which to set the value.</param>
/// <param name="value">The value to be set to the instance.</param>
public delegate void Setter<TDeclaringType, TPropertyType>(ref TDeclaringType obj, TPropertyType value);

/// <summary>
/// Delegate representing a parameterized constructor.
/// </summary>
/// <typeparam name="TArgumentState">Type of the state object containing all constructor arguments.</typeparam>
/// <typeparam name="TDeclaringType">Type of the object to be constructed.</typeparam>
/// <param name="state">State object containing all constructor arguments.</param>
/// <returns>The instance created by the constructor.</returns>
public delegate TDeclaringType Constructor<TArgumentState, TDeclaringType>(ref TArgumentState state);

/// <summary>
/// Delegate representing a constructor that accepts a span of values.
/// </summary>
/// <typeparam name="T">The element type of the span parameters.</typeparam>
/// <typeparam name="TDeclaringType">The type of the value produced by the constructor.</typeparam>
/// <param name="values">The span of values used to create the instance.</param>
/// <returns>A newly constructed instance using the specified values.</returns>
public delegate TDeclaringType SpanConstructor<T, TDeclaringType>(ReadOnlySpan<T> values);