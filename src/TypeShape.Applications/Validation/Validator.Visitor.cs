namespace TypeShape.Applications.Validation;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static partial class Validator
{
    private class Visitor : ITypeShapeVisitor
    {
        private readonly Dictionary<Type, object> _cache = new();
        
        public object? VisitType<T>(IType<T> type, object? state)
        {
            if (TryGetCachedValue<T>() is Validator<T> result)
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableType().Accept(this, null);

                case TypeKind kind when (kind.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryType().Accept(this, null);

                case TypeKind.Enumerable:
                    return type.GetEnumerableType().Accept(this, null);

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

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
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

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state)
            where TKey : notnull
        {
            var valueValidator = (Validator<TValue>)dictionaryType.ValueType.Accept(this, null)!;
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryType.GetGetDictionary();
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

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableType.GetGetEnumerable();
            var elementValidator = (Validator<TElement>)enumerableType.ElementType.Accept(this, null)!;
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

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            var elementValidator = (Validator<T>)nullableType.ElementType.Accept(this, null)!;
            return CacheResult((T? nullable, List<string> path, ref List<string>? errors) =>
            {
                if (nullable.HasValue)
                {
                    elementValidator(nullable.Value, path, ref errors);
                }
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

        public object? VisitEnum<TEnum, TUnderlying>(IEnumType<TEnum, TUnderlying> enumType, object? state) where TEnum : struct, Enum
        {
            throw new NotImplementedException();
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
