namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for CollectionBuilderAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
public sealed class CollectionBuilderAttribute(Type builderType, string methodName) : Attribute
{
    public Type BuilderType { get; } = builderType;
    public string MethodName { get; } = methodName;
}
