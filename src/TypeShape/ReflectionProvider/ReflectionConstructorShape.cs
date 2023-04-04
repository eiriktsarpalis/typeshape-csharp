using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape<TDeclaringType, TArgumentState>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly ConstructorShapeInfo _ctorInfo;

    public ReflectionConstructorShape(ReflectionTypeShapeProvider provider, ConstructorShapeInfo ctorInfo)
    {
        _ctorInfo = ctorInfo;
        _provider = provider;
    }

    public ITypeShape DeclaringType => _provider.GetShape<TDeclaringType>();
    public int ParameterCount => _ctorInfo.TotalParameters;
    public ICustomAttributeProvider? AttributeProvider => _ctorInfo.ConstructorInfo;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public Func<TArgumentState> GetArgumentStateConstructor()
        => _provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(_ctorInfo);

    public Func<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => _provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(_ctorInfo);

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (ParameterCount > 0)
        {
            throw new InvalidOperationException("The current constructor shape is not parameterless.");
        }

        return _provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(_ctorInfo);
    }

    public IEnumerable<IConstructorParameterShape> GetParameters()
    {
        ConstructorShapeInfo ctorInfo = _ctorInfo;

        for (int i = 0; i < ctorInfo.TotalParameters; i++)
        {
            yield return _provider.CreateConstructorParameter(typeof(TArgumentState), ctorInfo, i);
        }
    }
}

internal sealed class ConstructorShapeInfo
{
    public ConstructorShapeInfo(
        Type declaringType, 
        ConstructorInfo? constructorInfo, 
        ConstructorParameterShapeInfo[] parameters, 
        MemberInitializerShapeInfo[]? memberInitializers = null, 
        ConstructorShapeInfo? nestedTupleCtor = null)
    {
        Debug.Assert(constructorInfo != null || (declaringType.IsValueType && parameters.Length == 0));
        DeclaringType = declaringType;
        ConstructorInfo = constructorInfo;
        Parameters = parameters;
        MemberInitializers = memberInitializers ?? Array.Empty<MemberInitializerShapeInfo>();
        NestedTupleCtor = nestedTupleCtor;
        TotalParameters = Parameters.Length + MemberInitializers.Length + (NestedTupleCtor?.TotalParameters ?? 0);
    }

    public Type DeclaringType { get; }
    public ConstructorInfo? ConstructorInfo { get;}
    public int TotalParameters { get; }
    public ConstructorParameterShapeInfo[] Parameters { get; }
    public MemberInitializerShapeInfo[] MemberInitializers { get; }
    public ConstructorShapeInfo? NestedTupleCtor { get; }
    public bool IsNestedValueTuple => NestedTupleCtor != null && DeclaringType.IsValueType;

    public IParameterShapeInfo GetParameter(int position)
    {
        Debug.Assert(position < TotalParameters);

        if (position < Parameters.Length)
        {
            return Parameters[position];
        }

        position -= Parameters.Length;
        if (position < MemberInitializers.Length)
        {
            return MemberInitializers[position];
        }

        Debug.Assert(NestedTupleCtor != null);
        return NestedTupleCtor.GetParameter(position - MemberInitializers.Length);
    }

    public IEnumerable<IParameterShapeInfo> GetAllParameters()
    {
        for (ConstructorShapeInfo? curr = this; curr != null; curr = curr.NestedTupleCtor)
        {
            foreach (ConstructorParameterShapeInfo param in curr.Parameters)
            {
                yield return param;
            }

            foreach (MemberInitializerShapeInfo memberInitializer in curr.MemberInitializers)
            {
                yield return memberInitializer;
            }
        }
    }
}