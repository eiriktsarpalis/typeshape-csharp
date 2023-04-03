using System.Reflection;

namespace TypeShape;

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