using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructorParameter<TArgumentState, TParameter> : IConstructorParameter<TArgumentState, TParameter>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly object? _defaultValue;
    private readonly int _constructorArity;

    public ReflectionConstructorParameter(ReflectionTypeShapeProvider provider, ParameterInfo parameterInfo, int constructorArity)
    {
        Name = parameterInfo.Name;
        Position = parameterInfo.Position;
        _constructorArity = constructorArity;
        _provider = provider;

        if (parameterInfo.HasDefaultValue)
        {
            _defaultValue = parameterInfo.GetDefaultValueNormalized();
            HasDefaultValue = true;
        }
    }

    public IType ParameterType => _provider.GetShape<TParameter>();

    public int Position { get; }
    public string? Name { get; }
    public bool HasDefaultValue { get; }
    public TParameter? DefaultValue => (TParameter?)_defaultValue;
    object? IConstructorParameter.DefaultValue => _defaultValue;

    public Setter<TArgumentState, TParameter> GetSetter()
        => _provider.MemberAccessor.CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(Position, _constructorArity);

    public object? Accept(IConstructorParameterVisitor visitor, object? state)
        => visitor.VisitConstructorParameter(this, state);
}
