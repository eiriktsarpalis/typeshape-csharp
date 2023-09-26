using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructorShape<TDeclaringType, TArgumentState>(
    ReflectionTypeShapeProvider provider, 
    IConstructorShapeInfo ctorInfo) : IConstructorShape<TDeclaringType, TArgumentState>
{
    public ITypeShape DeclaringType => provider.GetShape<TDeclaringType>();
    public int ParameterCount => ctorInfo.Parameters.Count;
    public ICustomAttributeProvider? AttributeProvider => ctorInfo.AttributeProvider;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public Func<TArgumentState> GetArgumentStateConstructor()
        => provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(ctorInfo);

    public Func<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ctorInfo);

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (ParameterCount > 0)
        {
            throw new InvalidOperationException("The current constructor shape is not parameterless.");
        }

        return provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(ctorInfo);
    }

    public IEnumerable<IConstructorParameterShape> GetParameters()
    {
        for (int i = 0; i < ctorInfo.Parameters.Count; i++)
        {
            yield return provider.CreateConstructorParameter(typeof(TArgumentState), ctorInfo, i);
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

internal sealed class TupleConstructorShapeInfo(
    Type constructedType,
    ConstructorInfo constructorInfo,
    MethodParameterShapeInfo[] constructorParameters,
    TupleConstructorShapeInfo? nestedTupleCtor) : IConstructorShapeInfo
{
    private IParameterShapeInfo[]? _allParameters;

    public Type ConstructedType { get; } = constructedType;
    public ConstructorInfo ConstructorInfo { get; } = constructorInfo;
    public MethodParameterShapeInfo[] ConstructorParameters { get; } = constructorParameters;
    public TupleConstructorShapeInfo? NestedTupleConstructor { get; } = nestedTupleCtor;
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