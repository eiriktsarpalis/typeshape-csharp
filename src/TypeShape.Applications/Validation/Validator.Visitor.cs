namespace TypeShape.Applications.Validation;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static partial class Validator
{
    private sealed class Visitor : TypeShapeVisitor
    {
        private readonly Dictionary<Type, object> _cache = new();
        
        public override object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedValue<T>() is Validator<T> result)
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, null);

                case TypeKind kind when (kind.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryShape().Accept(this, null);

                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, null);

                default:

                    (string, Validator<T>)[] propertyValidators = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Where(prop => prop.HasGetter)
                        .Select(prop => (prop.Name, (Validator<T>)prop.Accept(this, state)!))
                        .ToArray();

                    return CacheResult((T? value, List<string> path, ref List<string>? errors) =>
                    {
                        if (value is null)
                        {
                            return;
                        }

                        foreach ((string name, Validator<T> propertyValidator) in propertyValidators)
                        {
                            path.Add(name);
                            propertyValidator(value, path, ref errors);
                            path.RemoveAt(path.Count - 1);
                        }
                    });
            }
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            (Predicate<TPropertyType> Predicate, string ErrorMessage)[]? validationPredicates = property.AttributeProvider?
                .GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .Select(attr => (ValidationAttribute)attr)
                .Select(attr => (Predicate: attr.CreateValidationPredicate<TPropertyType>(), attr.ErrorMessage))
                .Where(pair => pair.Predicate != null)
                .ToArray()!;

            var propertyTypeValidator = (Validator<TPropertyType>)property.PropertyType.Accept(this, null)!;
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            return new Validator<TDeclaringType>((TDeclaringType? obj, List<string> path, ref List<string>? errors) =>
            {
                Debug.Assert(obj != null);

                // Step 1. get the property value
                TPropertyType propertyValue = getter(ref obj);

                // Step 2. run any validation predicates
                if (validationPredicates != null)
                {
                    foreach ((Predicate<TPropertyType> predicate, string errorMessage) in validationPredicates)
                    {
                        if (!predicate(propertyValue))
                        {
                            string msg = $"Validation error in $.{string.Join(".", path)}: {errorMessage}";
                            (errors ??= new()).Add(msg);
                        }
                    }
                }

                // Step 3. continue traversal of the property value
                propertyTypeValidator(propertyValue, path, ref errors);
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            var valueValidator = (Validator<TValue>)dictionaryShape.ValueType.Accept(this, null)!;
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();
            return CacheResult((TDictionary? dict, List<string> path, ref List<string>? errors) =>
            {
                if (dict is null)
                {
                    return;
                }

                foreach (var kvp in getDictionary(dict))
                {
                    path.Add(kvp.Key.ToString()!); // TODO formatting of non-string keys
                    valueValidator(kvp.Value, path, ref errors);
                    path.RemoveAt(path.Count - 1);
                }
            });
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
            var elementValidator = (Validator<TElement>)enumerableShape.ElementType.Accept(this, null)!;
            return CacheResult((TEnumerable? enumerable, List<string> path, ref List<string>? errors) =>
            {
                if (enumerable is null)
                {
                    return;
                }

                int i = 0;
                foreach (var e in getEnumerable(enumerable))
                {
                    path.Add($"[{i}]");
                    elementValidator(e, path, ref errors);
                    path.RemoveAt(path.Count - 1);
                    i++;
                }
            });
        }

        public override object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementValidator = (Validator<T>)nullableShape.ElementType.Accept(this, null)!;
            return CacheResult((T? nullable, List<string> path, ref List<string>? errors) =>
            {
                if (nullable.HasValue)
                {
                    elementValidator(nullable.Value, path, ref errors);
                }
            });
        }

        private Validator<T>? TryGetCachedValue<T>()
        {
            ref object? entryRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, typeof(T), out bool exists);
            if (exists)
            {
                return ((DelayedCacheEntry<T>)entryRef!).Result;
            }
            else
            {
                entryRef = new DelayedCacheEntry<T>();
                return null;
            }
        }

        private Validator<T> CacheResult<T>(Validator<T> validator)
        {
            ((DelayedCacheEntry<T>)_cache[typeof(T)]).Result = validator;
            return validator;
        }

        // Delayed delegate initializer for handling recursive types
        private sealed class DelayedCacheEntry<T>
        {
            private Validator<T>? _result;
            public Validator<T> Result
            {
                get => _result ?? ((T? value, List<string> path, ref List<string>? errors) => _result!(value, path, ref errors));
                set => _result = value;
            }
        }
    }
}
