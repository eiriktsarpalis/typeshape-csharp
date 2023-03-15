namespace TypeShape.Applications.PrettyPrinter;

using System;
using System.Diagnostics;
using System.Text;
using TypeShape;

public static partial class PrettyPrinter
{
    private class Visitor : ITypeShapeVisitor
    {
        private static readonly Dictionary<Type, object> s_defaultPrinters = new(CreateDefaultPrinters());

        public object? VisitType<T>(IType<T> type, object? state)
        {
            // Recursive type handling omitted for simplicity.
            if (s_defaultPrinters.TryGetValue(typeof(T), out object? result))
                return result;

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableType().Accept(this, null);
                case TypeKind.Enum:
                    return type.GetEnumType().Accept(this, null);
                case var k when (k.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryType().Accept(this, null);
                case TypeKind.Enumerable:
                    return type.GetEnumerableType().Accept(this, null)!;

                default:

                    PrettyPrinter<T>[] propertyPrinters = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Where(prop => prop.HasGetter)
                        .Select(prop => (PrettyPrinter<T>?)prop.Accept(this, null)!)
                        .Where(prop => prop != null)
                        .ToArray();

                    return new PrettyPrinter<T>((sb, indentation, value) =>
                    {
                        if (value is null)
                        {
                            sb.Append("null");
                            return;
                        }

                        sb.Append("new ");
                        sb.Append(typeof(T).Name);

                        if (propertyPrinters.Length == 0)
                        {
                            sb.Append("()");
                            return;
                        }

                        WriteLine(sb, indentation);
                        sb.Append('{');
                        for (int i = 0; i < propertyPrinters.Length; i++)
                        {
                            WriteLine(sb, indentation + 1);
                            propertyPrinters[i](sb, indentation + 1, value);
                            sb.Append(',');
                        }

                        sb.Length--;
                        WriteLine(sb, indentation);
                        sb.Append('}');
                    });
            };
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            PrettyPrinter<TPropertyType> propertyTypePrinter = (PrettyPrinter<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new PrettyPrinter<TDeclaringType>((sb, indentation, obj) =>
            {
                Debug.Assert(obj != null);
                sb.Append(property.Name).Append(" = ");
                propertyTypePrinter(sb, indentation, getter(ref obj));
            });
        }

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableType.GetGetEnumerable();
            PrettyPrinter<TElement> elementPrinter = (PrettyPrinter<TElement>)enumerableType.ElementType.Accept(this, null)!;
            bool valuesArePrimitives = s_defaultPrinters.ContainsKey(typeof(TElement));

            return new PrettyPrinter<TEnumerable>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                    return;
                }

                sb.Append("new ");
                sb.Append(typeof(TEnumerable).Name);

                bool containsElements = false;
                if (valuesArePrimitives)
                {
                    sb.Append(" { ");
                    foreach (TElement element in enumerableGetter(value))
                    {
                        elementPrinter(sb, indentation, element);
                        sb.Append(", ");
                        containsElements = true;
                    }

                    sb.Length--;
                    if (containsElements)
                        sb.Length--;

                    sb.Append(" }");
                }
                else
                {
                    WriteLine(sb, indentation);
                    sb.Append('{');
                    foreach (TElement element in enumerableGetter(value))
                    {
                        WriteLine(sb, indentation + 1);
                        elementPrinter(sb, indentation + 2, element);
                        sb.Append(',');
                        containsElements = true;
                    }

                    if (containsElements)
                        sb.Length -= 1;

                    WriteLine(sb, indentation);
                    sb.Append(" }");
                }
            });
        }

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state)
            where TKey : notnull
        {
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryType.GetGetDictionary();
            PrettyPrinter<TKey> keyPrinter = (PrettyPrinter<TKey>)dictionaryType.KeyType.Accept(this, null)!;
            PrettyPrinter<TValue> valuePrinter = (PrettyPrinter<TValue>)dictionaryType.ValueType.Accept(this, null)!;

            return new PrettyPrinter<TDictionary>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                    return;
                }

                sb.Append("new ");
                sb.Append(typeof(TDictionary).Name);

                IReadOnlyDictionary<TKey, TValue> dictionary = dictionaryGetter(value);

                if (dictionary.Count == 0)
                {
                    sb.Append("()");
                    return;
                }

                WriteLine(sb, indentation);
                sb.Append('{');
                foreach (KeyValuePair<TKey, TValue> kvp in dictionaryGetter(value))
                {
                    WriteLine(sb, indentation + 1);
                    sb.Append('[');
                    keyPrinter(sb, indentation + 2, kvp.Key); // TODO non-primitive key indentation
                    sb.Append("] = ");
                    valuePrinter(sb, indentation + 2, kvp.Value);
                    sb.Append(',');
                }

                sb.Length -= 1;
                WriteLine(sb, indentation);
                sb.Append('}');
            });
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumType<TEnum, TUnderlying> enumType, object? state) where TEnum : struct, Enum
        {
            return new PrettyPrinter<TEnum>((sb, _, e) => sb.Append('"').Append(e).Append('"'));
        }

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            var elementPrinter = (PrettyPrinter<T>)nullableType.ElementType.Accept(this, null)!;
            return new PrettyPrinter<T?>((sb, indentation, value) =>
            {
                if (value is null)
                    sb.Append("null");
                else
                    elementPrinter(sb, indentation, value.Value);
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

        private static void WriteLine(StringBuilder builder, int indentation)
        {
            builder.AppendLine();
            builder.Append(' ', 2 * indentation);
        }

        private static void WriteStringLiteral(StringBuilder builder, string value)
            => builder.Append('\"').Append(value).Append('\"');

        private static void WriteStringLiteral(StringBuilder builder, object value)
            => builder.Append('\"').Append(value).Append('\"');

        private static IEnumerable<KeyValuePair<Type, object>> CreateDefaultPrinters()
        {
            yield return Create<bool>((builder, _, b) => builder.Append(b ? "true" : "false"));

            yield return Create<byte>((builder, _, i) => builder.Append(i));
            yield return Create<ushort>((builder, _, i) => builder.Append(i));
            yield return Create<uint>((builder, _, i) => builder.Append(i));
            yield return Create<ulong>((builder, _, i) => builder.Append(i));

            yield return Create<sbyte>((builder, _, i) => builder.Append(i));
            yield return Create<short>((builder, _, i) => builder.Append(i));
            yield return Create<int>((builder, _, i) => builder.Append(i));
            yield return Create<long>((builder, _, i) => builder.Append(i));

            yield return Create<float>((builder, _, i) => builder.Append(i));
            yield return Create<double>((builder, _, i) => builder.Append(i));
            yield return Create<decimal>((builder, _, i) => builder.Append(i));

            yield return Create<char>((builder, _, c) => builder.Append('\'').Append(c).Append('\''));
            yield return Create<string>((builder, _, s) =>
            {
                if (s is null)
                    builder.Append("null");
                else
                    WriteStringLiteral(builder, s);
            });

            yield return Create<DateTime>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<TimeSpan>((builder, _, t) => WriteStringLiteral(builder, t));
            yield return Create<Guid>((builder, _, g) => WriteStringLiteral(builder, g));

            static KeyValuePair<Type, object> Create<T>(PrettyPrinter<T> printer)
                => new(typeof(T), printer);
        }
    }
}
