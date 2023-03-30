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

        for (int i = 0; i < shapeInfo.TotalParameters; i++)
        {
            yield return _provider.CreateConstructorParameter(typeof(TArgumentState), shapeInfo, i);
        }
    }
}

internal sealed class ConstructorShapeInfo
{
    public ConstructorShapeInfo(
        Type declaringType, 
        ConstructorInfo? constructorInfo, 
        ConstructorParameterInfo[] parameters, 
        MemberInitializerInfo[]? memberInitializers = null, 
        ConstructorShapeInfo? nestedTupleCtor = null)
    {
        Debug.Assert(constructorInfo != null || (declaringType.IsValueType && parameters.Length == 0));
        DeclaringType = declaringType;
        ConstructorInfo = constructorInfo;
        Parameters = parameters;
        MemberInitializers = memberInitializers ?? Array.Empty<MemberInitializerInfo>();
        NestedTupleCtor = nestedTupleCtor;
        TotalParameters = Parameters.Length + MemberInitializers.Length + (NestedTupleCtor?.TotalParameters ?? 0);
    }

    public Type DeclaringType { get; }
    public ConstructorInfo? ConstructorInfo { get;}
    public int TotalParameters { get; }
    public ConstructorParameterInfo[] Parameters { get; }
    public MemberInitializerInfo[] MemberInitializers { get; }
    public ConstructorShapeInfo? NestedTupleCtor { get; }
    public bool IsNestedValueTuple => NestedTupleCtor != null;

    public IConstructorParameterInfo this[int i]
    {
        get
        {
            Debug.Assert(i < TotalParameters);

            if (i < Parameters.Length)
            {
                return Parameters[i];
            }

            i -= Parameters.Length;
            if (i < MemberInitializers.Length)
            {
                return MemberInitializers[i];
            }

            Debug.Assert(NestedTupleCtor != null);
            return NestedTupleCtor[i - MemberInitializers.Length];
        }
    }
}