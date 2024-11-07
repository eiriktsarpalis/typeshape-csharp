using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.ReflectionProvider.MemberAccessors;

internal interface IReflectionMemberAccessor
{
    Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers);
    Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers);

    Setter<TEnumerable, TElement> CreateEnumerableAddDelegate<TEnumerable, TElement>(MethodInfo methodInfo);
    Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(MethodInfo methodInfo);

    Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IConstructorShapeInfo ctorInfo);

    Type CreateConstructorArgumentStateType(IConstructorShapeInfo ctorInfo);
    Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IConstructorShapeInfo ctorInfo);
    Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(IConstructorShapeInfo ctorInfo, int parameterIndex);
    Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IConstructorShapeInfo ctorInfo);

    Func<T, TResult> CreateFuncDelegate<T, TResult>(ConstructorInfo ctorInfo);
    SpanConstructor<T, TResult> CreateSpanConstructorDelegate<T, TResult>(ConstructorInfo ctorInfo);
}
