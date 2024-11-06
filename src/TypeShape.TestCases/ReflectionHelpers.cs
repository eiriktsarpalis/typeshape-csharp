using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

internal static class ReflectionHelpers
{
    internal static bool IsMemoryType(this Type type, [NotNullWhen(true)] out Type? elementType, out bool isReadOnlyMemory)
    {
        if (type.IsGenericType && type.IsValueType)
        {
            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ReadOnlyMemory<>))
            {
                elementType = type.GetGenericArguments()[0];
                isReadOnlyMemory = true;
                return true;
            }

            if (genericTypeDefinition == typeof(Memory<>))
            {
                elementType = type.GetGenericArguments()[0];
                isReadOnlyMemory = false;
                return true;
            }
        }

        elementType = null;
        isReadOnlyMemory = false;
        return false;
    }

    internal static bool IsRecordType(this Type type)
    {
        return !type.IsValueType
            ? type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
            : type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(StringBuilder)]) is { } method
                && method.ReturnType == typeof(bool)
                && method.GetCustomAttributesData().Any(attr => attr.AttributeType.Name == "CompilerGeneratedAttribute");
    }

    internal static bool IsImmutableArray(this Type type)
        => type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);
}
