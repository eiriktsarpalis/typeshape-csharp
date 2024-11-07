using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class BoolConverter : XmlConverter<bool>
{
    public override bool Read(XmlReader reader)
        => reader.ReadElementContentAsBoolean();

    public override void Write(XmlWriter writer, string localName, bool value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class ByteConverter : XmlConverter<byte>
{
    public override byte Read(XmlReader reader)
        => (byte)reader.ReadElementContentAsInt();

    public override void Write(XmlWriter writer, string localName, byte value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class UInt16Converter : XmlConverter<ushort>
{
    public override ushort Read(XmlReader reader)
        => (ushort)reader.ReadElementContentAsInt();

    public override void Write(XmlWriter writer, string localName, ushort value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class UInt32Converter : XmlConverter<uint>
{
    public override uint Read(XmlReader reader)
        => (uint)reader.ReadElementContentAsLong();

    public override void Write(XmlWriter writer, string localName, uint value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class UInt64Converter : XmlConverter<ulong>
{
    public override ulong Read(XmlReader reader)
        => ulong.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, ulong value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class UInt128Converter : XmlConverter<UInt128>
{
    public override UInt128 Read(XmlReader reader)
        => UInt128.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, UInt128 value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class SByteConverter : XmlConverter<sbyte>
{
    public override sbyte Read(XmlReader reader)
        => (sbyte)reader.ReadElementContentAsInt();

    public override void Write(XmlWriter writer, string localName, sbyte value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class Int16Converter : XmlConverter<short>
{
    public override short Read(XmlReader reader)
        => (short)reader.ReadElementContentAsInt();

    public override void Write(XmlWriter writer, string localName, short value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class Int32Converter : XmlConverter<int>
{
    public override int Read(XmlReader reader)
        => reader.ReadElementContentAsInt();

    public override void Write(XmlWriter writer, string localName, int value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class Int64Converter : XmlConverter<long>
{
    public override long Read(XmlReader reader)
        => reader.ReadElementContentAsLong();

    public override void Write(XmlWriter writer, string localName, long value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class Int128Converter : XmlConverter<Int128>
{
    public override Int128 Read(XmlReader reader)
        => Int128.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, Int128 value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class StringConverter : XmlConverter<string>
{
    public override string? Read(XmlReader reader)
        => reader.TryReadNullElement() ? null : reader.ReadElementContentAsString();

    public override void Write(XmlWriter writer, string localName, string? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class CharConverter : XmlConverter<char>
{
    public override char Read(XmlReader reader)
        => reader.ReadElementContentAsString()[0];

    public override void Write(XmlWriter writer, string localName, char value)
        => writer.WriteElementString(localName, value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class RuneConverter : XmlConverter<Rune>
{
    public override Rune Read(XmlReader reader)
        => Rune.GetRuneAt(reader.ReadElementContentAsString(), 0);

    public override void Write(XmlWriter writer, string localName, Rune value)
        => writer.WriteElementString(localName, value.ToString());
}

internal sealed class HalfConverter : XmlConverter<Half>
{
    public override Half Read(XmlReader reader)
        => Half.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, Half value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class SingleConverter : XmlConverter<float>
{
    public override float Read(XmlReader reader)
        => reader.ReadElementContentAsFloat();

    public override void Write(XmlWriter writer, string localName, float value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class DoubleConverter : XmlConverter<double>
{
    public override double Read(XmlReader reader)
        => reader.ReadElementContentAsDouble();

    public override void Write(XmlWriter writer, string localName, double value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class DecimalConverter : XmlConverter<decimal>
{
    public override decimal Read(XmlReader reader)
        => reader.ReadElementContentAsDecimal();

    public override void Write(XmlWriter writer, string localName, decimal value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class BigIntegerConverter : XmlConverter<BigInteger>
{
    public override BigInteger Read(XmlReader reader)
        => BigInteger.Parse(reader.ReadElementContentAsString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, BigInteger value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class ByteArrayConverter : XmlConverter<byte[]>
{
    public override byte[]? Read(XmlReader reader)
        => reader.TryReadNullElement() ? null : Convert.FromBase64String(reader.ReadElementContentAsString());

    public override void Write(XmlWriter writer, string localName, byte[]? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
        }
        else
        {
            writer.WriteElementString(localName, Convert.ToBase64String(value));
        }
    }
}

internal sealed class GuidConverter : XmlConverter<Guid>
{
    public override Guid Read(XmlReader reader)
        => Guid.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, Guid value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString());
        writer.WriteEndElement();
    }
}

internal sealed class DateTimeConverter : XmlConverter<DateTime>
{
    public override DateTime Read(XmlReader reader)
        => DateTime.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, DateTime value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class DateTimeOffsetConverter : XmlConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(XmlReader reader)
        => DateTimeOffset.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, DateTimeOffset value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class TimeSpanConverter : XmlConverter<TimeSpan>
{
    public override TimeSpan Read(XmlReader reader)
        => TimeSpan.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, TimeSpan value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString());
        writer.WriteEndElement();
    }
}

internal sealed class DateOnlyConverter : XmlConverter<DateOnly>
{
    public override DateOnly Read(XmlReader reader)
        => DateOnly.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, DateOnly value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class UriConverter : XmlConverter<Uri>
{
    public override Uri? Read(XmlReader reader)
        => reader.TryReadNullElement() ? null : new Uri(reader.ReadElementContentAsString(), UriKind.RelativeOrAbsolute);

    public override void Write(XmlWriter writer, string localName, Uri? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

internal sealed class VersionConverter : XmlConverter<Version>
{
    public override Version? Read(XmlReader reader)
        => reader.TryReadNullElement() ? null : Version.Parse(reader.ReadElementContentAsString());

    public override void Write(XmlWriter writer, string localName, Version? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString());
        writer.WriteEndElement();
    }
}

internal sealed class TimeOnlyConverter : XmlConverter<TimeOnly>
{
    public override TimeOnly Read(XmlReader reader)
        => TimeOnly.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, string localName, TimeOnly value)
    {
        writer.WriteStartElement(localName);
        writer.WriteValue(value.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}

internal sealed class ObjectConverter : XmlConverter<object>
{
    // TODO implement proper polymorphism.
    public override object? Read(XmlReader reader)
    {
        if (reader.TryReadNullElement())
        {
            return null;
        }

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return new object();
        }

        return reader.ReadElementContentAsString();
    }

    public override void Write(XmlWriter writer, string localName, object? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        writer.WriteStartElement(localName);
        switch (value)
        {
            case bool b: writer.WriteValue(b); break;
            case int i: writer.WriteValue(i); break;
            case string s: writer.WriteValue(s); break;
        }
        writer.WriteEndElement();
    }
}