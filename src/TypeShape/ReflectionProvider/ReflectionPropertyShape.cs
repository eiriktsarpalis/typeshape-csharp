using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly MemberInfo _memberInfo;
    private readonly MemberInfo[]? _parentMembers; // stack of parent members reserved for nested tuple representations

    public ReflectionPropertyShape(ReflectionTypeShapeProvider provider, string? logicalName, MemberInfo memberInfo, MemberInfo[]? parentMembers, bool nonPublic)
    {
        Debug.Assert(memberInfo.DeclaringType!.IsAssignableFrom(typeof(TDeclaringType)) || parentMembers is not null);
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);
        Debug.Assert(parentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        _provider = provider;
        _memberInfo = memberInfo;
        _parentMembers = parentMembers;

        Name = logicalName ?? memberInfo.Name;

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

        memberInfo.GetNonNullableReferenceInfo(out bool isGetterNonNullable, out bool isSetterNonNullable);
        IsGetterNonNullableReferenceType = isGetterNonNullable;
        IsSetterNonNullableReferenceType = isSetterNonNullable;
    }

    public string Name { get; }
    public ICustomAttributeProvider? AttributeProvider => _memberInfo;
    public ITypeShape DeclaringType => _provider.GetShape<TDeclaringType>();
    public ITypeShape PropertyType => _provider.GetShape<TPropertyType>();

    public bool IsField { get; }
    public bool IsGetterNonNullableReferenceType { get; }
    public bool IsSetterNonNullableReferenceType { get; }

    public bool HasGetter { get; }
    public bool HasSetter { get; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitProperty(this, state);

    public Getter<TDeclaringType, TPropertyType> GetGetter()
    {
        if (!HasGetter)
        {
            throw new InvalidOperationException("The current property shape does not define a getter.");
        }

        return _provider.MemberAccessor.CreateGetter<TDeclaringType, TPropertyType>(_memberInfo, _parentMembers);
    }

    public Setter<TDeclaringType, TPropertyType> GetSetter()
    {
        if (!HasSetter)
        {
            throw new InvalidOperationException("The current property shape does not define a setter.");
        }

        return _provider.MemberAccessor.CreateSetter<TDeclaringType, TPropertyType>(_memberInfo, _parentMembers);
    }
}
