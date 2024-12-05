using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

internal static class ReflectionHelpers
{
    public static bool IsNetFrameworkProcessOnWindowsArm { get; } = IsNetFrameworkOnWindowsArmCore();
    private static bool IsNetFrameworkOnWindowsArmCore()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        if (!RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal))
        {
            return false;
        }

        IntPtr procHandle = Process.GetCurrentProcess().Handle;
        IsWow64Process2(procHandle, out _, out var nativeMachine);
        return nativeMachine == 0xaa64;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);
    }

    public static bool IsMemoryType(this Type type, [NotNullWhen(true)] out Type? elementType, out bool isReadOnlyMemory)
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

    public static bool IsRecordType(this Type type)
    {
        return !type.IsValueType
            ? type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
            : type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(StringBuilder)], null) is { } method
                && method.ReturnType == typeof(bool)
                && method.GetCustomAttributesData().Any(attr => attr.AttributeType.Name == "CompilerGeneratedAttribute");
    }

    public static bool IsTupleType(this Type type)
    {
        if (type is not { Namespace: "System", Name: string name })
        {
            return false;
        }

        return name.StartsWith("ValueTuple", StringComparison.Ordinal) ||
            name.StartsWith("Tuple", StringComparison.Ordinal);
    }

    public static bool IsImmutableArray(this Type type)
        => type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);
}
