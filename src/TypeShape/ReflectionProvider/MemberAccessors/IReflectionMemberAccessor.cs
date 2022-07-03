using System.Reflection;

namespace TypeShape.ReflectionProvider.MemberAccessors;

internal interface IReflectionMemberAccessor
{
    Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo);
    Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo);

    Setter<TEnumerable, TElement> CreateCollectionAddDelegate<TEnumerable, TElement>(MethodInfo methodInfo);

    Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(ConstructorInfo? ctorInfo);

    Type CreateConstructorArgumentStateType(ParameterInfo[] constructorParams);
    Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(ParameterInfo[] constructorParams);
    Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(int parameterIndex, int totalParameters);
    Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ConstructorInfo? ctorInfo, ParameterInfo[] parameters);
}
