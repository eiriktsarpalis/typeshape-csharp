using System.Reflection;

namespace TypeShape.ReflectionProvider.MemberAccessors;

internal interface IReflectionMemberAccessor
{
    Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo);
    Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo);

    Setter<TEnumerable, TElement> CreateEnumerableAddDelegate<TEnumerable, TElement>(MethodInfo methodInfo);
    Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(MethodInfo methodInfo);

    Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(ConstructorShapeInfo ctorInfo);

    Type CreateConstructorArgumentStateType(ConstructorShapeInfo ctorInfo);
    Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(ConstructorShapeInfo ctorInfo);
    Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(ConstructorShapeInfo ctorInfo, int parameterIndex);
    Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ConstructorShapeInfo ctorInfo);
}
