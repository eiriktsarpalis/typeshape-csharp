using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructor<TDeclaringType, TArgumentState> : IConstructor<TDeclaringType, TArgumentState>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly ConstructorInfo? _constructorInfo;
    private readonly ParameterInfo[] _parameters;

    public ReflectionConstructor(ReflectionTypeShapeProvider provider, ConstructorInfo? constructorInfo, ParameterInfo[] parameters)
    {
        Debug.Assert(constructorInfo != null || (typeof(TDeclaringType).IsValueType && parameters.Length == 0));
        _provider = provider;
        _constructorInfo = constructorInfo;
        _parameters = parameters;
    }

    public IType DeclaringType => _provider.GetShape<TDeclaringType>();
    public int ParameterCount => _parameters.Length;
    public ICustomAttributeProvider? AttributeProvider => _constructorInfo;

    public object? Accept(IConstructorVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public Func<TArgumentState> GetArgumentStateConstructor()
        => _provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(_parameters);

    public Func<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => _provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(_constructorInfo, _parameters);

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (ParameterCount > 0)
        {
            throw new InvalidOperationException();
        }

        return _provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(_constructorInfo);
    }

    public IEnumerable<IConstructorParameter> GetParameters()
    {
        if (_constructorInfo is null)
            yield break;

        foreach (ParameterInfo parameterInfo in _constructorInfo.GetParameters())
        {
            yield return _provider.CreateConstructorParameter(typeof(TArgumentState), parameterInfo, _parameters.Length);
        }
    }
}