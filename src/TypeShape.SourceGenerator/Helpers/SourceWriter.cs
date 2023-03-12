using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Text;

namespace TypeShape.SourceGenerator.Helpers;

internal sealed class SourceWriter
{
    private readonly StringBuilder _sb = new();
    private int _indentationLevel;

    public int Length => _sb.Length;
    public int IndentationLevel => _indentationLevel;

    public void WriteStartBlock()
    {
        AddIndentation();
        _sb.AppendLine("{");
        _indentationLevel++;
    }

    public void WriteEndBlock()
    {
        Debug.Assert(_indentationLevel > 0);
        _indentationLevel--;
        AddIndentation();
        _sb.AppendLine("}");
    }

    public void WriteLine(string text)
    {
        if (_indentationLevel == 0)
        {
            _sb.AppendLine(text);
            return;
        }

        bool isFinalLine;
        ReadOnlySpan<char> remainingText = text.AsSpan();
        do
        {
            ReadOnlySpan<char> nextLine = GetNextLine(ref remainingText, out isFinalLine);

            AddIndentation();
            AppendSpan(nextLine);
            _sb.AppendLine();
        }
        while (!isFinalLine);
    }

    public void WriteLine() => _sb.AppendLine();

    public SourceText ToSourceText()
    {
        Debug.Assert(_indentationLevel == 0 && _sb.Length > 0);
        return SourceText.From(_sb.ToString(), Encoding.UTF8);
    }

    private void AddIndentation()
        => _sb.Append(' ', 4 * _indentationLevel);

    private static ReadOnlySpan<char> GetNextLine(ref ReadOnlySpan<char> remainingText, out bool isFinalLine)
    {
        if (remainingText.IsEmpty)
        {
            isFinalLine = true;
            return default;
        }

        ReadOnlySpan<char> next;
        ReadOnlySpan<char> rest;

        int lineLength = remainingText.IndexOf('\n');
        if (lineLength == -1)
        {
            lineLength = remainingText.Length;
            isFinalLine = true;
            rest = default;
        }
        else
        {
            rest = remainingText.Slice(lineLength + 1);
            isFinalLine = false;
        }

        if ((uint)lineLength > 0 && remainingText[lineLength - 1] == '\r')
        {
            lineLength--;
        }

        next = remainingText.Slice(0, lineLength);
        remainingText = rest;
        return next;
    }

    private unsafe void AppendSpan(ReadOnlySpan<char> span)
    {
        fixed (char* ptr = span)
        {
            _sb.Append(ptr, span.Length);
        }
    }
}
