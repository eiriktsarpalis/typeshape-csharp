namespace TypeShape.Applications.Validation;

using System.Diagnostics;

public static partial class Validator
{
    private sealed class Visitor : TypeShapeVisitor
    {
        private readonly TypeCache _cache = new();
        
        public override object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedResult<T>() is { } result)
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.None or TypeKind.Enum:
                    // No properties in this type, so no validation to do
                    return CacheResult((T? value, List<string> path, ref List<string>? errors) => { });
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, null);

                case TypeKind.Dictionary:
                    return type.GetDictionaryShape().Accept(this, null);

                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, null);

                default:
                    Debug.Assert(type.Kind is TypeKind.Object);

                    (string, Validator<T>)[] propertyValidators = type
                        .GetProperties()
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
                            string msg = $"$.{string.Join(".", path)}: {errorMessage}";
                            (errors ??= []).Add(msg);
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
                foreach (TElement? e in getEnumerable(enumerable))
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

        private Validator<T>? TryGetCachedResult<T>()
            => _cache.GetOrAddDelayedValue<Validator<T>>(static holder => ((T? value, List<string> path, ref List<string>? errors) => holder.Value!(value, path, ref errors)));

        private Validator<T> CacheResult<T>(Validator<T> validator)
        {
            _cache.Add(validator);
            return validator;
        }
    }
}
