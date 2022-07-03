using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal static class ReflectionHelpers
{
    public static bool CanBeGenericArgument(this Type type)
    {
        return !(type == typeof(void) || type.IsPointer || type.IsByRef || type.IsByRefLike || type.ContainsGenericParameters);
    }

    public static bool IsInitOnly(this PropertyInfo propertyInfo)
    {
        return propertyInfo.SetMethod is MethodInfo setter &&
            setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(static modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }

    public static bool IsRequired(this MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");
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
}
