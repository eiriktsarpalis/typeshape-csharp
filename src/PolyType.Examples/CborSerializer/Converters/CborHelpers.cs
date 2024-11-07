using System.Formats.Cbor;
using System.Runtime.CompilerServices;

namespace PolyType.Examples.CborSerializer.Converters;

internal static class CborHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureTag(this CborReader reader, CborTag expectedTag)
    {
        CborTag actualTag = reader.ReadTag();
        if (actualTag != expectedTag)
        {
            ThrowFormatException(expectedTag, actualTag);
            static void ThrowFormatException(CborTag expectedTag, CborTag actualTag) => throw new FormatException($"Expected CBOR tag {expectedTag} but got {actualTag}.");
        }
    }

    public static T ThrowInvalidToken<T>(CborReaderState state)
        => throw new InvalidOperationException($"Unexpected CBOR token type '{state}'.");
}
