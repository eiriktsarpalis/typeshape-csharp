namespace TypeShape.Applications.Validator;

using System.Diagnostics;

public static partial class Validator
{
    private class Visitor : ITypeShapeVisitor
    {
        public object? VisitType<T>(IType<T> type, object? state)
        {
            switch (type.Kind)
            {
                case TypeKind kind when (kind.HasFlag(TypeKind.Dictionary)):
                    IDictionaryType dictionaryType = type.GetDictionaryType();
                    return dictionaryType.Accept(this, null);

                case TypeKind.Enumerable:
                    IEnumerableType enumerableType = type.GetEnumerableType();
                    return enumerableType.Accept(this, null);

                default:
                    Debug.Assert(type.Kind == TypeKind.None);

                    (string, Validator<T>)[] propertyValidators = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Where(prop => prop.HasGetter)
                        .Select(prop => (prop.Name, (Validator<T>)prop.Accept(this, state)!))
                        .ToArray();

                    return new Validator<T>((Func<object?, bool> isNodeValid, T value, List<string> path, ref List<string>? errors) =>
                    {
                        if (!isNodeValid(value))
                        {
                            errors ??= new();
                            errors.Add($"Value in path $.{string.Join(".", path)} is not valid.");
                        }

                        foreach (var (name, propertyValidator) in propertyValidators)
                        {
                            path.Add(name);
                            propertyValidator(isNodeValid, value, path, ref errors);
                            path.RemoveAt(path.Count - 1);
                        }
                    });
            }
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
        {
            var propertyTypeValidator = (Validator<TPropertyType>)property.PropertyType.Accept(this, null)!;
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            return new Validator<TDeclaringType>((Func<object?, bool> isNodeValid, TDeclaringType obj, List<string> path, ref List<string>? errors) =>
            {
                propertyTypeValidator(isNodeValid, getter(ref obj), path, ref errors);
            });
        }

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state)
            where TKey : notnull
        {
            var valueValidator = (Validator<TValue>)dictionaryType.ValueType.Accept(this, null)!;
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryType.GetGetDictionary();
            return new Validator<TDictionary>((Func<object?, bool> isNodeValid, TDictionary dict, List<string> path, ref List<string>? errors) =>
            {
                if (!isNodeValid(dict))
                {
                    errors ??= new();
                    errors.Add($"Value in path {string.Join(".", path)} is not valid.");
                }

                foreach (var kvp in getDictionary(dict))
                {
                    path.Add(kvp.Key.ToString()!);
                    valueValidator(isNodeValid, kvp.Value, path, ref errors);
                    path.RemoveAt(path.Count - 1);
                }
            });
        }

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableType.GetGetEnumerable();
            var elementValidator = (Validator<TElement>)enumerableType.ElementType.Accept(this, null)!;
            return new Validator<TEnumerable>((Func<object?, bool> isNodeValid, TEnumerable enumerable, List<string> path, ref List<string>? errors) =>
            {
                if (!isNodeValid(enumerable))
                {
                    errors ??= new();
                    errors.Add($"Value in path {string.Join(".", path)} is not valid.");
                }

                int i = 0;
                foreach (var e in getEnumerable(enumerable))
                {
                    path.Add($"[{i}]");
                    elementValidator(isNodeValid, e, path, ref errors);
                    path.RemoveAt(path.Count - 1);
                    i++;
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

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            throw new NotImplementedException();
        }
    }
}

