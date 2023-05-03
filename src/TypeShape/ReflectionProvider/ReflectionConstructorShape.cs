using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape<TDeclaringType, TArgumentState>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly IConstructorShapeInfo _ctorInfo;

    public ReflectionConstructorShape(ReflectionTypeShapeProvider provider, IConstructorShapeInfo ctorInfo)
    {
        _ctorInfo = ctorInfo;
        _provider = provider;
    }

    public ITypeShape DeclaringType => _provider.GetShape<TDeclaringType>();
    public int ParameterCount => _ctorInfo.Parameters.Count;
    public ICustomAttributeProvider? AttributeProvider => _ctorInfo.AttributeProvider;

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
        IConstructorShapeInfo ctorInfo = _ctorInfo;

        for (int i = 0; i < ctorInfo.Parameters.Count; i++)
        {
            yield return _provider.CreateConstructorParameter(typeof(TArgumentState), ctorInfo, i);
        }
    }
}

internal interface IConstructorShapeInfo
{
    Type ConstructedType { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    IReadOnlyList<IParameterShapeInfo> Parameters { get; }
}

internal sealed class MethodConstructorShapeInfo : IConstructorShapeInfo
{
    public MethodConstructorShapeInfo(
        Type constructedType, 
        MethodBase? constructorMethod,
        MemberInitializerShapeInfo[]? memberInitializers = null)
    {
        Debug.Assert(constructorMethod is null or ConstructorInfo or MethodInfo { IsStatic: true });
        Debug.Assert(constructorMethod != null || constructedType.IsValueType);

        ConstructedType = constructedType;
        ConstructorMethod = constructorMethod;
        ConstructorParameters = constructorMethod is null
            ? Array.Empty<MethodParameterShapeInfo>()
            : constructorMethod.GetParameters()
                .Select(p => new MethodParameterShapeInfo(p))
                .ToArray();

        MemberInitializers = memberInitializers ?? Array.Empty<MemberInitializerShapeInfo>();

        var parameters = new IParameterShapeInfo[ConstructorParameters.Length + MemberInitializers.Length];
        ConstructorParameters.CopyTo(parameters, 0);
        MemberInitializers.CopyTo(parameters, ConstructorParameters.Length);
        Parameters = parameters;
    }

    public Type ConstructedType { get; }
    public MethodBase? ConstructorMethod { get; }
    public MethodParameterShapeInfo[] ConstructorParameters { get; }
    public MemberInitializerShapeInfo[] MemberInitializers { get; }

    public ICustomAttributeProvider? AttributeProvider => ConstructorMethod;
    public IReadOnlyList<IParameterShapeInfo> Parameters { get; }
}

internal sealed class TupleConstructorShapeInfo : IConstructorShapeInfo
{
    private IParameterShapeInfo[]? _allParameters;

    public TupleConstructorShapeInfo(
        Type constructedType, 
        ConstructorInfo constructorInfo,
        MethodParameterShapeInfo[] constructorParameters,
        TupleConstructorShapeInfo? nestedTupleCtor)
    {
        Debug.Assert(constructorParameters.Length > 0);
        ConstructedType = constructedType;
        ConstructorInfo = constructorInfo;
        ConstructorParameters = constructorParameters;
        NestedTupleConstructor = nestedTupleCtor;
    }

    public Type ConstructedType { get; }
    public ConstructorInfo ConstructorInfo { get; }
    public MethodParameterShapeInfo[] ConstructorParameters { get; }
    public TupleConstructorShapeInfo? NestedTupleConstructor { get; }
    public bool IsValueTuple => ConstructedType.IsValueType;

    public ICustomAttributeProvider? AttributeProvider => ConstructorInfo;
    public IReadOnlyList<IParameterShapeInfo> Parameters => _allParameters ??= GetAllParameters().ToArray();

    private IEnumerable<IParameterShapeInfo> GetAllParameters()
    {
        for (TupleConstructorShapeInfo? curr = this; curr != null; curr = curr.NestedTupleConstructor)
        {
            foreach (IParameterShapeInfo param in curr.ConstructorParameters)
            {
                yield return param;
            }
        }
    }
}