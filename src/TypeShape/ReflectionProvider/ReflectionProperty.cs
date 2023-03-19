using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionProperty<TDeclaringType, TPropertyType> : IProperty<TDeclaringType, TPropertyType>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly MemberInfo _memberInfo;

    public ReflectionProperty(ReflectionTypeShapeProvider provider, MemberInfo memberInfo, bool nonPublic)
    {
        Debug.Assert(memberInfo.DeclaringType!.IsAssignableFrom(typeof(TDeclaringType)));
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        _provider = provider;
        _memberInfo = memberInfo;

        if (memberInfo is FieldInfo f)
        {
            HasGetter = true;
            HasSetter = !f.IsInitOnly;
            IsField = true;
        }
        else if (memberInfo is PropertyInfo p)
        {
            HasGetter = p.CanRead && (nonPublic || p.GetMethod!.IsPublic);
            HasSetter = p.CanWrite && (nonPublic || p.SetMethod!.IsPublic) && !p.IsInitOnly();
        }
    }

    public string Name => _memberInfo.Name;
    public ICustomAttributeProvider? AttributeProvider => _memberInfo;
    public IType DeclaringType => _provider.GetShape<TDeclaringType>();
    public IType PropertyType => _provider.GetShape<TPropertyType>();

    public bool IsField { get; }

    public bool HasGetter { get; }
    public bool HasSetter { get; }

    public object? Accept(IPropertyVisitor visitor, object? state)
        => visitor.VisitProperty(this, state);

    public Getter<TDeclaringType, TPropertyType> GetGetter()
    {
        if (!HasGetter)
        {
            throw new InvalidOperationException();
        }

        return _provider.MemberAccessor.CreateGetter<TDeclaringType, TPropertyType>(_memberInfo);
    }

    public Setter<TDeclaringType, TPropertyType> GetSetter()
    {
        if (!HasSetter)
        {
            throw new InvalidOperationException();
        }

        return _provider.MemberAccessor.CreateSetter<TDeclaringType, TPropertyType>(_memberInfo);
    }
}
