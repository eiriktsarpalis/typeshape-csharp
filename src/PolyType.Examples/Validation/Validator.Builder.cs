using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics;

namespace PolyType.Examples.Validation;

public static partial class Validator
{
    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
    {
        public Validator<T>? GetOrAddValidator<T>(ITypeShape<T> shape) => (Validator<T>?)generationContext.GetOrAdd(shape);
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => typeShape.Accept(this);

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            (string, Validator<T>)[] propertyValidators = type
                .GetProperties()
                .Where(prop => prop.HasGetter)
                .Select(prop => (prop.Name, Validator: (Validator<T>?)prop.Accept(this)))
                .Where(prop => prop.Validator != null)
                .ToArray()!;

            if (propertyValidators.Length == 0)
            {
                return null; // Nothing to validate for this object.
            }

            return new Validator<T>((T? value, List<string> path, ref List<string>? errors) =>
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

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            (Predicate<TPropertyType> Predicate, string ErrorMessage)[]? validationPredicates = property.AttributeProvider?
                .GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .Select(attr => (ValidationAttribute)attr)
                .Select(attr => (Predicate: attr.CreateValidationPredicate<TPropertyType>(), attr.ErrorMessage))
                .Where(pair => pair.Predicate != null)
                .ToArray()!;

            Validator<TPropertyType>? propertyTypeValidator = GetOrAddValidator(property.PropertyType);

            if (validationPredicates is null && propertyTypeValidator is null)
            {
                return null; // Nothing to validate for this property
            }

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
                propertyTypeValidator?.Invoke(propertyValue, path, ref errors);
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            Validator<TValue>? valueValidator = GetOrAddValidator(dictionaryShape.ValueType);
            if (valueValidator is null)
            {
                return null; // Nothing to validate for this type.
            }

            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();
            return new Validator<TDictionary>((TDictionary? dict, List<string> path, ref List<string>? errors) =>
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

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Validator<TElement>? elementValidator = GetOrAddValidator(enumerableShape.ElementType);
            if (elementValidator is null)
            {
                return null; // Nothing to validate for this type.
            }

            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
            return new Validator<TEnumerable>((TEnumerable? enumerable, List<string> path, ref List<string>? errors) =>
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

        public override object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state) where T : struct
        {
            Validator<T>? elementValidator = GetOrAddValidator(nullableShape.ElementType);
            if (elementValidator is null)
            {
                return null; // Nothing to validate for this type.
            }

            return new Validator<T?>((T? nullable, List<string> path, ref List<string>? errors) =>
            {
                if (nullable.HasValue)
                {
                    elementValidator(nullable.Value, path, ref errors);
                }
            });
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return null; // Nothing to validate for enums.
        }

        /// <summary>
        /// Creates a trivial validator that always succeeds.
        /// </summary>
        public static Validator<T> CreateNullValidator<T>() => new((T? value, List<string> path, ref List<string>? errors) => { });
    }

    private sealed class DelayedValidatorFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<Validator<T>>(self => (T? value, List<string> path, ref List<string>? errors) => self.Result(value, path, ref errors));
    }
}
