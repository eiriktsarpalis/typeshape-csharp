using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenType<T> : IType<T>
{
    public Type Type => typeof(T);

    public required ITypeShapeProvider Provider { get; init; }
    public ICustomAttributeProvider? AttributeProvider { get; init; }
    public Func<IEnumerable<IConstructor>>? CreateConstructorsFunc { get; init; }
    public Func<IDictionaryType>? CreateDictionaryTypeFunc { get; init; }
    public Func<IEnumerableType>? CreateEnumerableTypeFunc { get; init; }
    public Func<IEnumType>? CreateEnumTypeFunc { get; init; }
    public Func<INullableType>? CreateNullableTypeFunc { get; init; }
    public Func<IEnumerable<IProperty>>? CreatePropertiesFunc { get; init; }

    public TypeKind Kind => _kind ??= GetKind();
    private TypeKind? _kind;

    private TypeKind GetKind()
    {
        TypeKind kind = TypeKind.None;

        if (CreateEnumerableTypeFunc != null)
            kind |= TypeKind.Enumerable;

        if (CreateDictionaryTypeFunc != null)
            kind |= TypeKind.Dictionary;

        if (CreateNullableTypeFunc != null)
            kind |= TypeKind.Nullable;
            
        if (CreateEnumTypeFunc != null)
            kind |= TypeKind.Enum;

        return kind;
    }

    public object? Accept(ITypeVisitor visitor, object? state)
        => visitor.VisitType(this, state);

    public IEnumerable<IConstructor> GetConstructors(bool nonPublic)
    {
        if (nonPublic)
            throw new InvalidOperationException();

        return CreateConstructorsFunc is null ? Array.Empty<IConstructor>() : CreateConstructorsFunc();
    }

    public IDictionaryType GetDictionaryType()
    {
        if (CreateDictionaryTypeFunc is null)
            throw new InvalidOperationException();

        return CreateDictionaryTypeFunc();
    }

    public IEnumerableType GetEnumerableType()
    {
        if (CreateEnumerableTypeFunc is null)
            throw new InvalidOperationException();

        return CreateEnumerableTypeFunc();
    }

    public IEnumType GetEnumType()
    {
        if (CreateEnumTypeFunc is null)
            throw new InvalidOperationException();

        return CreateEnumTypeFunc();
    }

    public INullableType GetNullableType()
    {
        if (CreateNullableTypeFunc is null)
            throw new InvalidOperationException();

        return CreateNullableTypeFunc();
    }

    public IEnumerable<IProperty> GetProperties(bool nonPublic, bool includeFields)
    {
        if (nonPublic)
            throw new InvalidOperationException();

        var properties = CreatePropertiesFunc is null ? Array.Empty<IProperty>() : CreatePropertiesFunc();
        return includeFields ? properties : properties.Where(prop => !prop.IsField);
    }
}
