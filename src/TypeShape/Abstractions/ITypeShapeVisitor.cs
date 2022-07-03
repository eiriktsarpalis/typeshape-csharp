namespace TypeShape;

public interface ITypeShapeVisitor
    : ITypeVisitor, IPropertyVisitor,
      IConstructorVisitor, IConstructorParameterVisitor,
      IEnumerableTypeVisitor, IDictionaryTypeVisitor,
      IEnumTypeVisitor, INullableTypeVisitor
{
}
