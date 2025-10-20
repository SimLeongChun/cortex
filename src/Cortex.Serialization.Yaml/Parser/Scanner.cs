using System.Collections.Generic;

namespace Cortex.Serialization.Yaml.Parser
{
    internal sealed class Scanner
    {
        private readonly string[] _lines;
        private int _lineIdx;

        public Scanner(string input)
        {
            _lines = input.Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }
        public IEnumerable<Token> Scan()
        {
            var indentStack = new Stack<int>();
            indentStack.Push(0);

            for (_lineIdx = 0; _lineIdx < _lines.Length; _lineIdx++)
            {
                var raw = _lines[_lineIdx];
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                int i = 0;
                int spaces = 0;
                while (i < raw.Length && raw[i] == ' ')
                {
                    spaces++;
                    i++;
                }

                while (spaces > indentStack.Peek())
                {
                    indentStack.Push(indentStack.Peek() + 2);
                    yield return new Token(TokenType.Indent, null, _lineIdx + 1, 1);
                }

                while (spaces < indentStack.Peek())
                {
                    indentStack.Pop();
                    yield return new Token(TokenType.Dedent, null, _lineIdx + 1, 1);
                }

                if (i < raw.Length && raw[i] == '-')
                {
                    yield return new Token(TokenType.Dash, null, _lineIdx + 1, i + 1);
                    i++;
                    if (i < raw.Length && raw[i] == ' ')
                        i++;
                }

                var rest = raw[i..];
                if (rest.Contains(": "))
                {
                    var idx = rest.IndexOf(": ");
                    var key = rest[..idx];
                    var val = rest[(idx + 2)..];
                    yield return new Token(TokenType.Key, key, _lineIdx + 1, i + 1);
                    if (val == "|")
                        yield return new Token(TokenType.BlockLiteral, null, _lineIdx + 1, i + 1);

                    else if (val == ">")
                        yield return new Token(TokenType.BlockFolded, null, _lineIdx + 1, i + 1);

                    else yield return new Token(TokenType.Scalar, val, _lineIdx + 1, i + 1);
                }
                else
                {
                    yield return new Token(TokenType.Scalar, rest, _lineIdx + 1, i + 1);
                }

                yield return new Token(TokenType.NewLine, null, _lineIdx + 1, raw.Length + 1);
            }
            while (indentStack.Count > 1)
            {
                indentStack.Pop();
                yield return new Token(TokenType.Dedent, null, _lineIdx + 1, 1);
            }

            yield return new Token(TokenType.EOF, null, _lineIdx + 1, 1);
        }
    }

}
