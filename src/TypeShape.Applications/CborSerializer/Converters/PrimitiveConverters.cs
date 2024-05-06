using System.Diagnostics;
using System.Formats.Cbor;
using System.Numerics;
using System.Text;

namespace TypeShape.Applications.CborSerializer.Converters;

internal sealed class BoolConverter : CborConverter<bool>
{
    public override bool Read(CborReader reader)
        => reader.ReadBoolean();

    public override void Write(CborWriter writer, bool value)
        => writer.WriteBoolean(value);
}

internal sealed class ByteConverter : CborConverter<byte>
{
    public override byte Read(CborReader reader)
        => (byte)reader.ReadUInt32();

    public override void Write(CborWriter writer, byte value)
        => writer.WriteUInt32(value);
}

internal sealed class UInt16Converter : CborConverter<ushort>
{
    public override ushort Read(CborReader reader)
        => (ushort)reader.ReadUInt32();

    public override void Write(CborWriter writer, ushort value)
        => writer.WriteUInt32(value);
}

internal sealed class UInt32Converter : CborConverter<uint>
{
    public override uint Read(CborReader reader)
        => reader.ReadUInt32();

    public override void Write(CborWriter writer, uint value)
        => writer.WriteUInt32(value);
}

internal sealed class UInt64Converter : CborConverter<ulong>
{
    public override ulong Read(CborReader reader)
        => reader.ReadUInt64();

    public override void Write(CborWriter writer, ulong value)
        => writer.WriteUInt64(value);
}

internal sealed class UInt128Converter : CborConverter<UInt128>
{
    public override UInt128 Read(CborReader reader)
        => (UInt128)reader.ReadBigInteger();

    public override void Write(CborWriter writer, UInt128 value)
        => writer.WriteBigInteger(value);
}

internal sealed class SByteConverter : CborConverter<sbyte>
{
    public override sbyte Read(CborReader reader)
        => (sbyte)reader.ReadInt32();

    public override void Write(CborWriter writer, sbyte value)
        => writer.WriteInt32(value);
}

internal sealed class Int16Converter : CborConverter<short>
{
    public override short Read(CborReader reader)
        => (short)reader.ReadInt32();

    public override void Write(CborWriter writer, short value)
        => writer.WriteInt32(value);
}

internal sealed class Int32Converter : CborConverter<int>
{
    public override int Read(CborReader reader)
        => reader.ReadInt32();

    public override void Write(CborWriter writer, int value)
        => writer.WriteInt32(value);
}

internal sealed class Int64Converter : CborConverter<long>
{
    public override long Read(CborReader reader)
        => reader.ReadInt64();

    public override void Write(CborWriter writer, long value)
        => writer.WriteInt64(value);
}

internal sealed class Int128Converter : CborConverter<Int128>
{
    public override Int128 Read(CborReader reader)
        => (Int128)reader.ReadBigInteger();

    public override void Write(CborWriter writer, Int128 value)
        => writer.WriteBigInteger(value);
}

internal sealed class StringConverter : CborConverter<string>
{
    public override string? Read(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.SkipValue();
            return null;
        }
        else
        {
            return reader.ReadTextString();
        }
    }

    public override void Write(CborWriter writer, string? value)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteTextString(value);
        }
    }
}

internal sealed class UriConverter : CborConverter<Uri>
{
    public override Uri? Read(CborReader reader)
    {
        reader.EnsureTag(CborTag.Uri);
        
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.SkipValue();
            return null;
        }
        else
        {
            return new Uri(reader.ReadTextString(), UriKind.RelativeOrAbsolute);   
        }
    }

    public override void Write(CborWriter writer, Uri? value)
    {
        writer.WriteTag(CborTag.Uri);
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteTextString(value.ToString());
        }
    }
}

internal sealed class VersionConverter : CborConverter<Version>
{
    public override Version? Read(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.SkipValue();
            return null;
        }
        else
        {
            return new Version(reader.ReadTextString());   
        }
    }

    public override void Write(CborWriter writer, Version? value)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteTextString(value.ToString());
        }
    }
}

internal sealed class CharConverter : CborConverter<char>
{
    public override char Read(CborReader reader)
    {
        return reader.ReadTextString()[0];
    }

    public override void Write(CborWriter writer, char value)
    {
        Span<char> buffer = [value];
        writer.WriteTextString(buffer);
    }
}

public sealed class RuneConverter : CborConverter<Rune>
{
    public override Rune Read(CborReader reader)
    {
        return Rune.GetRuneAt(reader.ReadTextString(), 0);
    }

    public override void Write(CborWriter writer, Rune value)
    {
        writer.WriteTextString(value.ToString());
    }
}

internal sealed class HalfConverter : CborConverter<Half>
{
    public override Half Read(CborReader reader)
        => reader.ReadHalf();

    public override void Write(CborWriter writer, Half value)
        => writer.WriteHalf(value);
}

internal sealed class SingleConverter : CborConverter<float>
{
    public override float Read(CborReader reader)
        => reader.ReadSingle();

    public override void Write(CborWriter writer, float value)
        => writer.WriteSingle(value);
}

internal sealed class DoubleConverter : CborConverter<double>
{
    public override double Read(CborReader reader)
        => reader.ReadDouble();

    public override void Write(CborWriter writer, double value)
        => writer.WriteDouble(value);
}

internal sealed class DecimalConverter : CborConverter<decimal>
{
    public override decimal Read(CborReader reader)
        => reader.ReadDecimal();

    public override void Write(CborWriter writer, decimal value)
        => writer.WriteDecimal(value);
}

internal sealed class BigIntegerConverter : CborConverter<BigInteger>
{
    public override BigInteger Read(CborReader reader)
        => reader.ReadBigInteger();

    public override void Write(CborWriter writer, BigInteger value)
        => writer.WriteBigInteger(value);
}

internal sealed class ByteArrayConverter : CborConverter<byte[]>
{
    public override byte[]? Read(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.SkipValue();
            return null;
        }
        else
        {
            return reader.ReadByteString();
        }
    }

    public override void Write(CborWriter writer, byte[]? value)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteByteString(value);
        }
    }
}

internal sealed class GuidConverter : CborConverter<Guid>
{
    // https://www.iana.org/assignments/cbor-tags/cbor-tags.xhtml
    internal const CborTag UUID = (CborTag)1002;

    public override Guid Read(CborReader reader)
    {
        reader.EnsureTag(UUID);
        ReadOnlyMemory<byte> data = reader.ReadDefiniteLengthByteString();
        return new Guid(data.Span);
    }

    public override void Write(CborWriter writer, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        bool success = value.TryWriteBytes(bytes);
        Debug.Assert(success);
        writer.WriteTag(UUID);
        writer.WriteByteString(bytes);
    }
}

internal sealed class DateTimeConverter : CborConverter<DateTime>
{
    public override DateTime Read(CborReader reader)
        => reader.ReadDateTimeOffset().DateTime;

    public override void Write(CborWriter writer, DateTime value)
        => writer.WriteDateTimeOffset(new DateTimeOffset(value));
}

internal sealed class DateTimeOffsetConverter : CborConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(CborReader reader)
        => reader.ReadDateTimeOffset();

    public override void Write(CborWriter writer, DateTimeOffset value)
        => writer.WriteDateTimeOffset(value);
}

internal sealed class TimeSpanConverter : CborConverter<TimeSpan>
{
    // https://www.iana.org/assignments/cbor-tags/cbor-tags.xhtml
    internal const CborTag DurationSeconds = (CborTag)37;

    public override TimeSpan Read(CborReader reader)
    {
        reader.EnsureTag(DurationSeconds);
        return reader.PeekState() switch
        {
            CborReaderState.UnsignedInteger or
            CborReaderState.NegativeInteger => TimeSpan.FromSeconds(reader.ReadInt64()),
            CborReaderState.HalfPrecisionFloat or
            CborReaderState.SinglePrecisionFloat or
            CborReaderState.DoublePrecisionFloat => TimeSpan.FromSeconds(reader.ReadDouble()),
            var state => CborHelpers.ThrowInvalidToken<TimeSpan>(state),
        };
    }

    public override void Write(CborWriter writer, TimeSpan value)
    {
        writer.WriteTag(DurationSeconds);
        writer.WriteDouble(value.TotalSeconds);
    }
}

internal sealed class DateOnlyConverter : CborConverter<DateOnly>
{
    public override DateOnly Read(CborReader reader)
        => DateOnly.FromDateTime(reader.ReadDateTimeOffset().DateTime);

    public override void Write(CborWriter writer, DateOnly value)
        => writer.WriteDateTimeOffset(new(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
}

internal sealed class TimeOnlyConverter : CborConverter<TimeOnly>
{
    private readonly static TimeSpanConverter s_timeSpanConverter = new();

    public override TimeOnly Read(CborReader reader)
        => TimeOnly.FromTimeSpan(s_timeSpanConverter.Read(reader));

    public override void Write(CborWriter writer, TimeOnly value)
        => s_timeSpanConverter.Write(writer, value.ToTimeSpan());
}

internal sealed class ObjectConverter : CborConverter<object>
{
    public override object? Read(CborReader reader)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.Null: 
                reader.ReadNull(); 
                return null;

            case CborReaderState.Boolean: 
                return reader.ReadBoolean();

            case CborReaderState.UnsignedInteger or
                 CborReaderState.NegativeInteger:
                return reader.ReadInt32();

            case CborReaderState.TextString: 
                return reader.ReadTextString();
            default:
                reader.SkipValue();
                return new object();

        }
    }

    public override void Write(CborWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(); break;
            case bool b: writer.WriteBoolean(b); break;
            case int i: writer.WriteInt32(i); break;
            case string s: writer.WriteTextString(s); break;
            default:
                writer.WriteStartMap(definiteLength: 0);
                writer.WriteEndMap();
                break;
        }
    }
}
