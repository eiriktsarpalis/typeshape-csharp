using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TypeShape.Abstractions;

namespace TypeShape.Examples.StructuralEquality;

/// <summary>Provides a structural <see cref="IEqualityComparer{T}"/> generator built on top of TypeShape.</summary>
public static partial class StructuralEqualityComparer
{
    /// <summary>
    /// Builds a structural <see cref="IEqualityComparer{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the comparer.</typeparam>
    /// <param name="shape">The shape instance guiding printer construction.</param>
    /// <returns>An <see cref="IEqualityComparer{T}"/> instance.</returns>
    public static IEqualityComparer<T> Create<T>(ITypeShape<T> shape) =>
        new Builder().BuildEqualityComparer(shape);

    /// <summary>
    /// Builds a structural <see cref="IEqualityComparer{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the comparer.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding comparer construction.</param>
    /// <returns>An <see cref="IEqualityComparer{T}"/> instance.</returns>
    public static IEqualityComparer<T> Create<T>(ITypeShapeProvider shapeProvider) =>
        Create(shapeProvider.Resolve<T>());

    /// <summary>
    /// Gets a structural <see cref="IEqualityComparer{T}"/> instance using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to build the comparer.</typeparam>
    /// <returns>An <see cref="IEqualityComparer{T}"/> instance.</returns>
    public static IEqualityComparer<T> Create<T>() where T : IShapeable<T> =>
        EqualityComparerCache<T, T>.Value;

    /// <summary>
    /// Gets a structural <see cref="IEqualityComparer{T}"/> instance using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to build the comparer.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <returns>An <see cref="IEqualityComparer{T}"/> instance.</returns>
    public static IEqualityComparer<T> Create<T, TProvider>() where TProvider : IShapeable<T> => 
        EqualityComparerCache<T, TProvider>.Value;

    /// <summary>
    /// Computes a structural hash code for a value using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the hash code.</typeparam>
    /// <param name="value">The value for which to compute the hash code.</param>
    /// <returns>A structural hash code for the value.</returns>
    public static int GetHashCode<T>([DisallowNull] T value) where T : IShapeable<T> => 
        EqualityComparerCache<T, T>.Value.GetHashCode(value);

    /// <summary>
    /// Compares two values of a type for structural equality using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to perform equality comparison.</typeparam>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>A structural hash code for the value.</returns>
    public static bool Equals<T>(T? left, T? right) where T : IShapeable<T> => 
        EqualityComparerCache<T, T>.Value.Equals(left, right);

    /// <summary>
    /// Computes a structural hash code for a value using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the hash code.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value for which to compute the hash code.</param>
    /// <returns>A structural hash code for the value.</returns>
    public static int GetHashCode<T, TProvider>([DisallowNull] T value) where TProvider : IShapeable<T> => 
        EqualityComparerCache<T, TProvider>.Value.GetHashCode(value);

    /// <summary>
    /// Compares two values of a type for structural equality using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to perform equality comparison.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>A structural hash code for the value.</returns>
    public static bool Equals<T, TProvider>(T? left, T? right) where TProvider : IShapeable<T> => 
        EqualityComparerCache<T, TProvider>.Value.Equals(left, right);

    internal static IEqualityComparer Create(Type type, ITypeShapeProvider shapeProvider)
    {
        ITypeShape shape = shapeProvider.Resolve(type);
        ITypeShapeFunc builder = new Builder();
        return (IEqualityComparer)shape.Invoke(builder, null)!;
    }

    private static class EqualityComparerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static IEqualityComparer<T> Value => s_value ??= Create(TProvider.GetShape());
        private static IEqualityComparer<T>? s_value;
    }
}