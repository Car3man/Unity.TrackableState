using System;
using System.Text;

namespace Klopoff.TrackableState.Generator.Internal;

internal sealed class CodeWriter
{
    private readonly StringBuilder _sb = new();
    private int _indent;
    private bool _atLineStart = true;

    public string IndentUnit => "    ";

    public void BlankLine() => _sb.AppendLine();

    public void WriteLine() => _sb.AppendLine();

    public void WriteLine(string text)
    {
        WriteIndentIfNeeded();
        _sb.AppendLine(text);
        _atLineStart = true;
    }

    public void Write(string text)
    {
        // Writes text as-is, respecting line breaks, and applies indentation at the start of each new line.
        int start = 0;
        while (true)
        {
            int nl = text.IndexOf('\n', start);
            if (nl < 0)
            {
                if (start < text.Length)
                {
                    WriteIndentIfNeeded();
                    _sb.Append(text, start, text.Length - start);
                    _atLineStart = false;
                }
                break;
            }

            int length = nl - start + 1;
            WriteIndentIfNeeded();
            _sb.Append(text, start, length);
            _atLineStart = true;
            start = nl + 1;
        }
    }

    public IDisposable Indent()
    {
        _indent++;
        return new IndentScope(this);
    }

    public void Usings(params string[] namespaces)
    {
        foreach (string ns in namespaces)
        {
            WriteLine($"using {ns};");
        }

        BlankLine();
    }
    
    public void Block(string header, Action body, string? suffix = null)
    {
        WriteLine(header);
        WriteLine("{");
        using (Indent())
        {
            body();
        }
        WriteLine($"}}{suffix}");
    }

    public void OptionalBlock(string? header, Action body)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            body();
        }
        else
        {
            Block(header!, body);
        }
    }

    private void WriteIndentIfNeeded()
    {
        if (!_atLineStart)
        {
            return;
        }

        for (int i = 0; i < _indent; i++)
        {
            _sb.Append(IndentUnit);
        }

        _atLineStart = false;
    }
    
    public override string ToString() => _sb.ToString();

    private sealed class IndentScope(CodeWriter w) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            
            w._indent--;
            
            if (w._indent < 0)
            {
                w._indent = 0;
            }
        }
    }
}