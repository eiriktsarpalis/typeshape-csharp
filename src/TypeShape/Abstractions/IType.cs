using System.Reflection;

namespace TypeShape;

[Flags]
public enum TypeKind
{
    None = 0,
    Enumerable = 1,
    Dictionary = 2,
    Nullable = 4,
    Enum = 8,
}

public interface IType
{
    Type Type { get; }
    public ITypeShapeProvider Provider { get; }
    public ICustomAttributeProvider? AttributeProvider { get; }

    IEnumerable<IConstructor> GetConstructors(bool nonPublic);
    IEnumerable<IProperty> GetProperties(bool nonPublic, bool includeFields);
    
    TypeKind Kind { get; }
    IEnumerableType GetEnumerableType();
    IDictionaryType GetDictionaryType();
    INullableType GetNullableType();
    IEnumType GetEnumType();

    object? Accept(ITypeVisitor visitor, object? state);
}

public interface IType<T> : IType
{
}

public interface ITypeVisitor
{
    object? VisitType<T>(IType<T> type, object? state);
}