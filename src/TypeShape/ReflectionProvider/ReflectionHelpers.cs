using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal static class ReflectionHelpers
{
    public static bool CanBeGenericArgument(this Type type)
    {
        return !(type == typeof(void) || type.IsPointer || type.IsByRef || type.IsByRefLike || type.ContainsGenericParameters);
    }

    public static bool IsRecord(this Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    public static bool IsCompilerGenerated(this MemberInfo memberInfo)
        => memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    public static bool IsAutoPropertyWithSetter(this MemberInfo memberInfo)
        => memberInfo is PropertyInfo { SetMethod: { } setter } && setter.IsCompilerGenerated();

    public static bool IsInitOnly(this MemberInfo memberInfo)
    {
        return memberInfo is PropertyInfo { SetMethod: { } setter } &&
            setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(static modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }

    public static bool IsRequired(this MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");
    }

    public static bool SetsRequiredMembers(this ConstructorInfo ctorInfo)
    {
        return ctorInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
    }

    public static Type MemberType(this MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo is FieldInfo f ? f.FieldType : ((PropertyInfo)memberInfo).PropertyType;
    }

    public static object? GetDefaultValueNormalized(this ParameterInfo parameterInfo, bool convertToParameterType = true)
    {
        object? defaultValue = parameterInfo.DefaultValue;

        if (!parameterInfo.HasDefaultValue || defaultValue is null)
            return null;

        Debug.Assert(defaultValue is not DBNull, "should have been caught by the HasDefaultValue check.");

        if (convertToParameterType)
        {
            Type parameterType = parameterInfo.ParameterType;
            if (parameterType.IsEnum)
            {
                defaultValue = Enum.ToObject(parameterType, defaultValue);
            }
            else if (Nullable.GetUnderlyingType(parameterType) is Type underlyingType && underlyingType.IsEnum)
            {
                defaultValue = Enum.ToObject(underlyingType, defaultValue);
            }
            else if (parameterType == typeof(IntPtr))
            {
                defaultValue = checked((IntPtr)Convert.ToInt64(defaultValue));
            }
            else if (parameterType == typeof(UIntPtr))
            {
                defaultValue = checked((UIntPtr)Convert.ToUInt64(defaultValue));
            }
        }

        return defaultValue;
    }

    public static Type[] GetSortedTypeHierarchy(this Type type)
    {
        if (!type.IsInterface)
        {
            // No need to walk the class hierarchy in reflection,
            // all members are reported in the current type.
            return new[] { type };
        }
        else
        {
            // Interface hierarchies support multiple inheritance.
            // For consistency with class hierarchy resolution order,
            // sort topologically from most derived to least derived.
            return CommonHelpers.TraverseGraphWithTopologicalSort(type, static t => t.GetInterfaces());
        }
    }
}
