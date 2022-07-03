namespace TypeShape.Applications.PrettyPrinter;

using System;
using System.Diagnostics;
using System.Text;
using TypeShape;

public static partial class PrettyPrinter
{
    private class Visitor : ITypeShapeVisitor
    {
        public object? VisitType<T>(IType<T> type, object? state)
        {
            switch (type)
            {
                case IType<bool>: return new PrettyPrinter<bool>((builder, b) => builder.Append(b ? "true" : "false"));
                case IType<int>: return new PrettyPrinter<int>((builder, i) => builder.Append(i));
                case IType<string>:
                    return new PrettyPrinter<string>((builder, s) =>
                    {
                        if (s is null)
                            builder.Append("null");
                        else
                            builder.Append('"').Append(s).Append('"');
                    });

                case { Kind: TypeKind.Nullable }:
                    return type.GetNullableType().Accept(this, null);
                case { Kind: TypeKind.Enum }:
                    return type.GetEnumType().Accept(this, null);
                case { Kind: var k } when (k.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryType().Accept(this, null);
                case { Kind: TypeKind.Enumerable }:
                    return type.GetEnumerableType().Accept(this, null)!;
                default:

                    PrettyPrinter<T>[] propertyPrinters = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Where(prop => prop.HasGetter)
                        .Select(prop => (PrettyPrinter<T>?)prop.Accept(this, null)!)
                        .Where(prop => prop != null)
                        .ToArray();

                    return new PrettyPrinter<T>((sb, value) =>
                    {
                        if (value is null)
                        {
                            sb.Append("null");
                            return;
                        }

                        sb.Append('{');
                        for (int i = 0; i < propertyPrinters.Length; i++)
                        {
                            sb.Append(' ');
                            propertyPrinters[i](sb, value);
                            sb.Append(", ");
                        }

                        if (propertyPrinters.Length > 0) 
                            sb.Length -= 2;

                        sb.Append(" }");
                    });
            };
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            PrettyPrinter<TPropertyType> propertyTypePrinter = (PrettyPrinter<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new PrettyPrinter<TDeclaringType>((sb, obj) =>
            {
                Debug.Assert(obj != null);
                sb.Append(property.Name).Append(" = ");
                propertyTypePrinter(sb, getter(ref obj));
            });
        }

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableType.GetGetEnumerable();
            PrettyPrinter<TElement> elementPrinter = (PrettyPrinter<TElement>)enumerableType.ElementType.Accept(this, null)!;

            return new PrettyPrinter<TEnumerable>((sb, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                    return;
                }

                bool containsElements = false;
                sb.Append('[');
                foreach (TElement element in enumerableGetter(value))
                {
                    elementPrinter(sb, element);
                    sb.Append(", ");
                    containsElements = true;
                }

                if (containsElements)
                    sb.Length -= 2;

                sb.Append(']');
            });
        }

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state)
            where TKey : notnull
        {
            Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> enumerableGetter = dictionaryType.GetGetEnumerable();
            PrettyPrinter<TKey> keyPrinter = (PrettyPrinter<TKey>)dictionaryType.KeyType.Accept(this, null)!;
            PrettyPrinter<TValue> valuePrinter = (PrettyPrinter<TValue>)dictionaryType.ValueType.Accept(this, null)!;

            return new PrettyPrinter<TDictionary>((sb, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                    return;
                }

                bool containsEntries = false;
                sb.Append('{');
                foreach (KeyValuePair<TKey, TValue> kvp in enumerableGetter(value))
                {
                    sb.Append(" [");
                    keyPrinter(sb, kvp.Key);
                    sb.Append("] = ");
                    valuePrinter(sb, kvp.Value);
                    sb.Append(", ");
                    containsEntries = true;
                }

                if (containsEntries)
                    sb.Length -= 2;

                sb.Append(" }");
            });
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumType<TEnum, TUnderlying> enumType, object? state) where TEnum : struct, Enum
        {
            return new PrettyPrinter<TEnum>((sb, e) => sb.Append('"').Append(e).Append('"'));
        }

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            var elementPrinter = (PrettyPrinter<T>)nullableType.ElementType.Accept(this, null)!;
            return new PrettyPrinter<T?>((sb, value) =>
            {
                if (value is null)
                    sb.Append("null");
                else
                    elementPrinter(sb, value.Value);
            });
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructor<TDeclaringType, TArgumentState> constructor, object? state)
        {
            throw new NotImplementedException();
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameter<TArgumentState, TParameter> parameter, object? state)
        {
            throw new NotImplementedException();
        }
    }
}
