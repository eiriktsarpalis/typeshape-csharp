using System.Runtime.CompilerServices;

namespace PolyType.Utilities;

/// <summary>
/// A container for a value that may be computed at a later time.
/// </summary>
/// <typeparam name="T">The type of the computed value.</typeparam>
public sealed class DelayedValue<T> : DelayedValue
{
    private bool _isCompleted;
    private T _result;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayedValue{T}"/> class.
    /// </summary>
    /// <param name="delayedValueFactory">A delegate creating a facade that wraps the delayed value reference.</param>
    public DelayedValue(Func<DelayedValue<T>, T> delayedValueFactory)
    {
        _result = delayedValueFactory(this);
        _isCompleted = false;
    }

    /// <summary>
    /// Gets the contained result if populated.
    /// </summary>
    public T Result
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!_isCompleted)
            {
                Throw();
                static void Throw() => throw new InvalidOperationException($"Value of type '{typeof(T)}' has not been completed yet.");
            }

            return _result;
        }
    }

    /// <inheritdoc/>
    public override bool IsCompleted => _isCompleted;

    internal override object? PotentiallyDelayedResult => _result;

    internal override void CompleteValue(object? value)
    {
        _result = (T)value!;
        _isCompleted = true;
    }
}

/// <summary>
/// A container for a value that may be computed at a later time.
/// </summary>
public abstract class DelayedValue
{
    internal DelayedValue() { }

    /// <summary>
    /// Gets a value indicating whether the result has been computed.
    /// </summary>
    public abstract bool IsCompleted { get; }

    internal abstract object? PotentiallyDelayedResult { get; }

    internal abstract void CompleteValue(object? value);
}