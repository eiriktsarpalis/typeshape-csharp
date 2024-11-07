using System.Reflection;

namespace PolyType.Utilities;

/// <summary>
/// Defines a set of common reflection utilities for use by PolyType applications.
/// </summary>
public static class ReflectionUtilities
{
    /// <summary>
    /// Determines if the specified attribute is defined by the attribute provider.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to attempt to look up.</typeparam>
    /// <param name="attributeProvider">The custom attribute provider to look up from.</param>
    /// <param name="inherit">Whether to look for inherited attributes.</param>
    /// <returns><see langword="true"/> is the attribute is defined, or <see langword="false"/> otherwise.</returns>
    public static bool IsDefined<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = false)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(attributeProvider);
        return attributeProvider.IsDefined(typeof(TAttribute), inherit);
    }

    /// <summary>
    /// Looks up the specified attribute provider for an attribute of the given type.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to attempt to look up.</typeparam>
    /// <param name="attributeProvider">The custom attribute provider to look up from.</param>
    /// <param name="inherit">Whether to look for inherited attributes.</param>
    /// <returns>The first occurrence of the attribute if found, or <see langword="null" /> otherwise.</returns>
    public static TAttribute? GetCustomAttribute<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = false)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(attributeProvider);
        return attributeProvider.GetCustomAttributes(typeof(TAttribute), inherit).OfType<TAttribute>().FirstOrDefault();
    }

    /// <summary>
    /// Looks up the specified attribute provider for attributes of the given type.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to attempt to look up.</typeparam>
    /// <param name="attributeProvider">The custom attribute provider to look up from.</param>
    /// <param name="inherit">Whether to look for inherited attributes.</param>
    /// <returns>An enumerable containing all instances of the attribute defined on the attribute provider.</returns>
    public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = false)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(attributeProvider);
        return attributeProvider.GetCustomAttributes(typeof(TAttribute), inherit).OfType<TAttribute>();
    }
}
