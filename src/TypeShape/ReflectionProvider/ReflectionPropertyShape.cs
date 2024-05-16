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

    public ReflectionPropertyShape(ReflectionTypeShapeProvider provider, PropertyShapeInfo shapeInfo)
    {
        Debug.Assert(shapeInfo.MemberInfo.DeclaringType!.IsAssignableFrom(typeof(TDeclaringType)) || shapeInfo.ParentMembers is not null);
        Debug.Assert(shapeInfo.MemberInfo is PropertyInfo or FieldInfo);
        Debug.Assert(shapeInfo.ParentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        _provider = provider;
        _memberInfo = shapeInfo.MemberInfo;
        _parentMembers = shapeInfo.ParentMembers;
        AttributeProvider = shapeInfo.AttributeProvider;

        Name = shapeInfo.LogicalName ?? shapeInfo.MemberInfo.Name;

        if (shapeInfo.MemberInfo is FieldInfo f)
        {
            HasGetter = true;
            HasSetter = !f.IsInitOnly;
            IsField = true;
            IsGetterPublic = f.IsPublic;
            IsSetterPublic = !f.IsInitOnly && f.IsPublic;
        }
        else
        {
            PropertyInfo p = (PropertyInfo)shapeInfo.MemberInfo;
            HasGetter = p.CanRead && (shapeInfo.IncludeNonPublicAccessors || p.GetMethod!.IsPublic);
            HasSetter = p.CanWrite && (shapeInfo.IncludeNonPublicAccessors || p.SetMethod!.IsPublic) && !p.IsInitOnly();
            IsGetterPublic = HasGetter && p.GetMethod!.IsPublic;
            IsSetterPublic = HasSetter && p.SetMethod!.IsPublic;
        }

        IsGetterNonNullable = HasGetter && shapeInfo.IsGetterNonNullable;
        IsSetterNonNullable = HasSetter && shapeInfo.IsSetterNonNullable;
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

internal sealed record PropertyShapeInfo(
    Type DeclaringType,
    MemberInfo MemberInfo,
    ICustomAttributeProvider AttributeProvider,
    MemberInfo[]? ParentMembers = null,
    string? LogicalName = null,
    int Order = 0,
    bool IncludeNonPublicAccessors = false,
    bool IsGetterNonNullable = false,
    bool IsSetterNonNullable = false);