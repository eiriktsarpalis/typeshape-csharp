using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenTypeShape<T> : ITypeShape<T>
{
    public Type Type => typeof(T);

    public required ITypeShapeProvider Provider { get; init; }
    public ICustomAttributeProvider? AttributeProvider { get; init; }
    public Func<bool /* nonPublic */, IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }
    public Func<IEnumerable<IConstructorShape>>? CreateConstructorsFunc { get; init; }
    public Func<IDictionaryShape>? CreateDictionaryShapeFunc { get; init; }
    public Func<IEnumerableShape>? CreateEnumerableShapeFunc { get; init; }
    public Func<IEnumShape>? CreateEnumShapeFunc { get; init; }
    public Func<INullableShape>? CreateNullableShapeFunc { get; init; }

    public TypeKind Kind => _kind ??= GetKind();
    private TypeKind? _kind;

    private TypeKind GetKind()
    {
        TypeKind kind = TypeKind.None;

        if (CreateEnumerableShapeFunc != null)
            kind |= TypeKind.Enumerable;

        if (CreateDictionaryShapeFunc != null)
            kind |= TypeKind.Dictionary;

        if (CreateNullableShapeFunc != null)
            kind |= TypeKind.Nullable;
            
        if (CreateEnumShapeFunc != null)
            kind |= TypeKind.Enum;

        return kind;
    }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitType(this, state);

    public IEnumerable<IConstructorShape> GetConstructors(bool nonPublic)
    {
        IEnumerable<IConstructorShape> constructors = CreateConstructorsFunc?.Invoke() ?? [];

        if (!nonPublic)
        {
            constructors = constructors.Where(ctor => ctor.IsPublic);
        }

        return constructors;
    }

    public IEnumerable<IPropertyShape> GetProperties(bool nonPublic, bool includeFields)
    {
        IEnumerable<IPropertyShape> properties = CreatePropertiesFunc?.Invoke(nonPublic) ?? [];

        if (!nonPublic)
        {
            properties = properties.Where(prop => prop.IsGetterPublic || prop.IsSetterPublic);
        }
        if (!includeFields)
        {
            properties = properties.Where(prop => !prop.IsField);
        }

        return properties;
    }

    public IEnumShape GetEnumShape()
    {
        ValidateKind(TypeKind.Enum);
        return CreateEnumShapeFunc!();
    }

    public INullableShape GetNullableShape()
    {
        ValidateKind(TypeKind.Nullable);
        return CreateNullableShapeFunc!();
    }

    public IEnumerableShape GetEnumerableShape()
    {
        ValidateKind(TypeKind.Enumerable);
        return CreateEnumerableShapeFunc!();
    }

    public IDictionaryShape GetDictionaryShape()
    {
        ValidateKind(TypeKind.Dictionary);
        return CreateDictionaryShapeFunc!();
    }

    private void ValidateKind(TypeKind expectedKind)
    {
        if ((Kind & expectedKind) == 0)
        {
            throw new InvalidOperationException($"Type {typeof(T)} is not of kind {expectedKind}.");
        }
    }
}
