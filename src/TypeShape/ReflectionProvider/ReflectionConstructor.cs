using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructor<TDeclaringType, TArgumentState> : IConstructor<TDeclaringType, TArgumentState>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly ConstructorShapeInfo _shapeInfo;

    public ReflectionConstructor(ReflectionTypeShapeProvider provider, ConstructorShapeInfo shapeInfo)
    {
        _shapeInfo = shapeInfo;
        _provider = provider;
    }

    public IType DeclaringType => _provider.GetShape<TDeclaringType>();
    public int ParameterCount => _shapeInfo.TotalParameters;
    public ICustomAttributeProvider? AttributeProvider => _shapeInfo.ConstructorInfo;

    public object? Accept(IConstructorVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public Func<TArgumentState> GetArgumentStateConstructor()
        => _provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(_shapeInfo);

    public Func<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => _provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(_shapeInfo);

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (ParameterCount > 0)
        {
            throw new InvalidOperationException();
        }

        return _provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(_shapeInfo);
    }

    public IEnumerable<IConstructorParameter> GetParameters()
    {
        ConstructorShapeInfo shapeInfo = _shapeInfo;

        foreach (ParameterInfo parameterInfo in shapeInfo.Parameters)
        {
            yield return _provider.CreateConstructorParameter(typeof(TArgumentState), shapeInfo, parameterInfo);
        }

        int i = shapeInfo.Parameters.Length;
        foreach (MemberInitializerInfo member in shapeInfo.MemberInitializers)
        {
            yield return _provider.CreateConstructorParameter(typeof(TArgumentState), shapeInfo, member, i++);
        }
    }
}

internal sealed class ConstructorShapeInfo
{
    public ConstructorShapeInfo(Type declaringType, ConstructorInfo? constructorInfo, ParameterInfo[] parameters, MemberInitializerInfo[] memberInitializers)
    {
        Debug.Assert(constructorInfo != null || (declaringType.IsValueType && parameters.Length == 0));
        DeclaringType = declaringType;
        ConstructorInfo = constructorInfo;
        Parameters = parameters;
        MemberInitializers = memberInitializers;
        TotalParameters = parameters.Length + memberInitializers.Length;
    }

    public Type DeclaringType { get; }
    public ConstructorInfo? ConstructorInfo { get;}
    public ParameterInfo[] Parameters { get; }
    public MemberInitializerInfo[] MemberInitializers { get; }
    public int TotalParameters { get; }
}

internal sealed class MemberInitializerInfo
{
    public MemberInitializerInfo(MemberInfo member, bool isRequired, bool isInitOnly)
    {
        Member = member;
        IsRequired = isRequired;
        IsInitOnly = isInitOnly;
        Type = member.MemberType();
    }

    public MemberInfo Member { get; }
    public Type Type { get; }
    public bool IsRequired { get; }
    public bool IsInitOnly { get; }
}