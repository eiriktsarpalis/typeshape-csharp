namespace TypeShape.Applications.PrettyPrinter;

using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using TypeShape;

public static partial class PrettyPrinter
{
    private sealed class Visitor : TypeShapeVisitor
    {
        private static readonly Dictionary<Type, object> s_defaultPrinters = new(CreateDefaultPrinters());
        private readonly TypeCache _cache = new();

        public override object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedResult<T>() is { } result)
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, null);
                case TypeKind.Enum:
                    return type.GetEnumShape().Accept(this, null);
                case TypeKind.Dictionary:
                    return type.GetDictionaryShape().Accept(this, null);
                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, null)!;

                default:

                    PrettyPrinter<T>[] propertyPrinters = type
                        .GetProperties(includeFields: true)
                        .Where(prop => prop.HasGetter)
                        .Select(prop => (PrettyPrinter<T>?)prop.Accept(this, null)!)
                        .Where(prop => prop != null)
                        .ToArray();

                    return CacheResult<T>((sb, indentation, value) =>
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
            }
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
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

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableShape.GetGetEnumerable();
            PrettyPrinter<TElement> elementPrinter = (PrettyPrinter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
            bool valuesArePrimitives = s_defaultPrinters.ContainsKey(typeof(TElement));

            return CacheResult<TEnumerable>((sb, indentation, value) =>
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
                        sb.Length -= 2;
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
                        sb.Length--;

                    WriteLine(sb, indentation);
                }

                sb.Append(']');
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryShape.GetGetDictionary();
            PrettyPrinter<TKey> keyPrinter = (PrettyPrinter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            PrettyPrinter<TValue> valuePrinter = (PrettyPrinter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;

            return CacheResult<TDictionary>((sb, indentation, value) =>
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

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumType, object? state)
        {
            return CacheResult<TEnum>((sb, _, e) => sb.Append('"').Append(e).Append('"'));
        }

        public override object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementPrinter = (PrettyPrinter<T>)nullableShape.ElementType.Accept(this, null)!;
            return CacheResult<T?>((sb, indentation, value) =>
            {
                if (value is null)
                    sb.Append("null");
                else
                    elementPrinter(sb, indentation, value.Value);
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
                    builder.Append("null");
                else
                    WriteStringLiteral(builder, s);
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

        private PrettyPrinter<T>? TryGetCachedResult<T>()
        {
            if (s_defaultPrinters.TryGetValue(typeof(T), out object? result))
            {
                return (PrettyPrinter<T>)result;
            }

            return _cache.GetOrAddDelayedValue<PrettyPrinter<T>>(static holder => ((b,i,v) => holder.Value!(b,i,v)));
        }

        private PrettyPrinter<T> CacheResult<T>(PrettyPrinter<T> counter)
        {
            _cache.Add(counter);
            return counter;
        }
    }
}
