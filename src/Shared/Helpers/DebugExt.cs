using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Diagnostics;

internal static class DebugExt
{
    /// <summary>
    /// A replacement for <see cref="Debug.Assert(bool, string?)"/> that has the appropriate annotations for netstandard2.0.
    /// </summary>
    /// <param name="condition">The conditional expression to evaluate. If the condition is true, the specified messages are not sent and the message box is not displayed.</param>
    /// <param name="message">The message to send to the Listeners collection.</param>
    [Conditional("DEBUG")]
    public static void Assert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? message = null)
    {
        Debug.Assert(condition, message, string.Empty);
    }
}