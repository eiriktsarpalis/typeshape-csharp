using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace PolyType.Roslyn.Tests;

public static class SourceWriterTests
{
    [Theory]
    [InlineData('\t', 1)]
    [InlineData(' ', 2)]
    [InlineData('\n', 42)]
    public static void Constructor_CustomizedIndentation(char indentationChar, int charsPerIndentation)
    {
        SourceWriter writer = new(indentationChar, charsPerIndentation);
        Assert.Equal(indentationChar, writer.IndentationChar);
        Assert.Equal(charsPerIndentation, writer.CharsPerIndentation);

        writer.Indentation = 2;
        writer.WriteLine("hi");
        writer.Indentation = 0;

        SourceText text = writer.ToSourceText();
        Assert.Equal(charsPerIndentation * 2 + 2 + Environment.NewLine.Length, text.Length);
    }
    
    [Theory]
    [InlineData('a', 1)]
    [InlineData('?', 1)]
    [InlineData('.', 1)]
    [InlineData(' ', 0)]
    [InlineData(' ', -1)]
    public static void Constructor_InvalidArguments(char indentationChar, int charsPerIndentation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceWriter(indentationChar, charsPerIndentation));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public static void Indentation_ValidArgument(int indentation)
    {
        SourceWriter writer = new();
        writer.Indentation = indentation;
        Assert.Equal(indentation, writer.Indentation);
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public static void Indentation_InvalidArgument(int indentation)
    {
        SourceWriter writer = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Indentation = indentation);
        Assert.Equal(0, writer.Indentation);
    }
}