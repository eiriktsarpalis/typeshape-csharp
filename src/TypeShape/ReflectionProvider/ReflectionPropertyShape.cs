using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TypeShape.Abstractions;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly MemberInfo _memberInfo;
    private readonly MemberInfo[]? _parentMembers; // stack of parent members reserved for nested tuple representations

    public ReflectionPropertyShape(ReflectionTypeShapeProvider provider, MemberInfo memberInfo, MemberInfo[]? parentMembers, ICustomAttributeProvider attributeProvider, string? logicalName, bool includeNonPublicAccessors)
    {
        Debug.Assert(memberInfo.DeclaringType!.IsAssignableFrom(typeof(TDeclaringType)) || parentMembers is not null);
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);
        Debug.Assert(parentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        _provider = provider;
        _memberInfo = memberInfo;
        _parentMembers = parentMembers;
        AttributeProvider = attributeProvider;

        Name = logicalName ?? memberInfo.Name;

        if (memberInfo is FieldInfo f)
        {
            HasGetter = true;
            HasSetter = !f.IsInitOnly;
            IsField = true;
            IsGetterPublic = f.IsPublic;
            IsSetterPublic = !f.IsInitOnly && f.IsPublic;
        }
        else if (memberInfo is PropertyInfo p)
        {
            HasGetter = p.CanRead && (includeNonPublicAccessors || p.GetMethod!.IsPublic);
            HasSetter = p.CanWrite && (includeNonPublicAccessors || p.SetMethod!.IsPublic) && !p.IsInitOnly();
            IsGetterPublic = HasGetter && p.GetMethod!.IsPublic;
            IsSetterPublic = HasSetter && p.SetMethod!.IsPublic;
        }

        memberInfo.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
        IsGetterNonNullable = HasGetter && isGetterNonNullable;
        IsSetterNonNullable = HasSetter && isSetterNonNullable;
    }

    public string Name { get; }
    public ICustomAttributeProvider AttributeProvider { get; }
    public IObjectTypeShape<TDeclaringType> DeclaringType => (IObjectTypeShape<TDeclaringType>)_provider.GetShape<TDeclaringType>();
    public ITypeShape<TPropertyType> PropertyType => _provider.GetShape<TPropertyType>();

    public bool IsField { get; }
    public bool IsGetterPublic { get; }
    public bool IsSetterPublic { get; }
    public bool IsGetterNonNullable { get; }
    public bool IsSetterNonNullable { get; }

    public bool HasGetter { get; }
    public bool HasSetter { get; }

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
