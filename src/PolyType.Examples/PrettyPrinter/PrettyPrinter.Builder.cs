using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace PolyType.Examples.PrettyPrinter;

public static partial class PrettyPrinter
{
    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
    {
        private static readonly Dictionary<Type, object> s_defaultPrinters = new(CreateDefaultPrinters());
        public PrettyPrinter<T> GetOrAddPrettyPrinter<T>(ITypeShape<T> typeShape) =>
            (PrettyPrinter<T>)generationContext.GetOrAdd(typeShape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? _)
        {
            if (s_defaultPrinters.TryGetValue(typeShape.Type, out object? defaultPrinter))
            {
                return defaultPrinter;
            }

            return typeShape.Accept(this);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            string typeName = FormatTypeName(typeof(T));
            PrettyPrinter<T>[] propertyPrinters = type
                .GetProperties()
                .Where(prop => prop.HasGetter)
                .Select(prop => (PrettyPrinter<T>?)prop.Accept(this)!)
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
                sb.Append(typeName);

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
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            PrettyPrinter<TPropertyType> propertyTypePrinter = GetOrAddPrettyPrinter(property.PropertyType);
            return new PrettyPrinter<TDeclaringType>((sb, indentation, obj) =>
            {
                Debug.Assert(obj != null);
                sb.Append(property.Name).Append(" = ");
                propertyTypePrinter(sb, indentation, getter(ref obj));
            });
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableShape.GetGetEnumerable();
            PrettyPrinter<TElement> elementPrinter = GetOrAddPrettyPrinter(enumerableShape.ElementType);
            bool valuesArePrimitives = s_defaultPrinters.ContainsKey(typeof(TElement));

            return new PrettyPrinter<TEnumerable>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                    return;
                }

                sb.Append('[');

                bool containsElements = false;
                if (valuesArePrimitives)
                {
                    foreach (TElement element in enumerableGetter(value))
                    {
                        elementPrinter(sb, indentation, element);
                        sb.Append(", ");
                        containsElements = true;
                    }

                    if (containsElements)
                    {
                        sb.Length -= 2;
                    }
                }
                else
                {
                    foreach (TElement element in enumerableGetter(value))
                    {
                        WriteLine(sb, indentation + 1);
                        elementPrinter(sb, indentation + 1, element);
                        sb.Append(',');
                        containsElements = true;
                    }

                    if (containsElements)
                    {
                        sb.Length--;
                    }

                    WriteLine(sb, indentation);
                }

                sb.Append(']');
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            string typeName = FormatTypeName(typeof(TDictionary));
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryShape.GetGetDictionary();
            PrettyPrinter<TKey> keyPrinter = GetOrAddPrettyPrinter(dictionaryShape.KeyType);
            PrettyPrinter<TValue> valuePrinter = GetOrAddPrettyPrinter(dictionaryShape.ValueType);

            return new PrettyPrinter<TDictionary>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                    return;
                }

                sb.Append("new ");
                sb.Append(typeName);

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
                    keyPrinter(sb, indentation + 1, kvp.Key); // TODO non-primitive key indentation
                    sb.Append("] = ");
                    valuePrinter(sb, indentation + 1, kvp.Value);
                    sb.Append(',');
                }

                sb.Length -= 1;
                WriteLine(sb, indentation);
                sb.Append('}');
            });
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return new PrettyPrinter<TEnum>((sb, _, e) => sb.Append(typeof(TEnum).Name).Append('.').Append(e));
        }

        public override object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state) where T : struct
        {
            PrettyPrinter<T> elementPrinter = GetOrAddPrettyPrinter(nullableShape.ElementType);
            return new PrettyPrinter<T?>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Append("null");
                }
                else
                {
                    elementPrinter(sb, indentation, value.Value);
                }
            });
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

        private static string FormatTypeName(Type type)
        {
            Debug.Assert(!type.IsArray || type.IsPointer || type.IsGenericTypeDefinition);
            if (type.IsGenericType)
            {
                string paramNames = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
                return $"{type.Name.Split('`')[0]}<{paramNames}>";
            }

            return type.Name;
        }

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
            yield return Create<BigInteger>((builder, _, i) => builder.Append(i));

            yield return Create<char>((builder, _, c) => builder.Append('\'').Append(c).Append('\''));
            yield return Create<string>((builder, _, s) =>
            {
                if (s is null)
                {
                    builder.Append("null");
                }
                else
                {
                    WriteStringLiteral(builder, s);
                }
            });

            yield return Create<DateTime>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<DateTimeOffset>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<TimeSpan>((builder, _, t) => WriteStringLiteral(builder, t));
            yield return Create<DateOnly>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<TimeOnly>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<Guid>((builder, _, g) => WriteStringLiteral(builder, g));

            static KeyValuePair<Type, object> Create<T>(PrettyPrinter<T> printer)
                => new(typeof(T), printer);
        }
    }

    private sealed class DelayedPrettyPrinterFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<PrettyPrinter<T>>(self => (sb, i, t) => self.Result(sb, i, t));
    }
}
