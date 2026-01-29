using System.Collections.Generic;
using System.Text;

namespace Cortex.Serialization.Yaml.Parser
{
    internal sealed class Scanner
    {
        private readonly string _input;
        private int _pos;
        private int _line = 1;
        private int _column = 1;
        private readonly Stack<int> _indentStack = new();
        private bool _inFlowContext;
        private int _flowDepth;

        public Scanner(string input)
        {
            _input = input.Replace("\r\n", "\n").Replace('\r', '\n');
            _indentStack.Push(0);
        }

        public IEnumerable<Token> Scan()
        {
            while (_pos < _input.Length)
            {
                // Skip blank lines
                if (IsAtLineStart() && (PeekChar() == '\n'))
                {
                    Advance();
                    continue;
                }

                // Handle indentation at the start of a line (only in block context)
                if (IsAtLineStart() && !_inFlowContext)
                {
                    foreach (var tok in HandleIndentation())
                        yield return tok;
                }

                // Skip whitespace (but not newlines)
                SkipWhitespace();

                if (_pos >= _input.Length)
                    break;

                char c = PeekChar();

                // Handle comments
                if (c == '#')
                {
                    var comment = ReadComment();
                    yield return new Token(TokenType.Comment, comment, _line, _column);
                    continue;
                }

                // Handle newlines
                if (c == '\n')
                {
                    yield return new Token(TokenType.NewLine, null, _line, _column);
                    Advance();
                    continue;
                }

                // Flow style tokens
                if (c == '[')
                {
                    yield return new Token(TokenType.FlowSequenceStart, null, _line, _column);
                    Advance();
                    _flowDepth++;
                    _inFlowContext = true;
                    continue;
                }

                if (c == ']')
                {
                    yield return new Token(TokenType.FlowSequenceEnd, null, _line, _column);
                    Advance();
                    _flowDepth--;
                    if (_flowDepth == 0) _inFlowContext = false;
                    continue;
                }

                if (c == '{')
                {
                    yield return new Token(TokenType.FlowMappingStart, null, _line, _column);
                    Advance();
                    _flowDepth++;
                    _inFlowContext = true;
                    continue;
                }

                if (c == '}')
                {
                    yield return new Token(TokenType.FlowMappingEnd, null, _line, _column);
                    Advance();
                    _flowDepth--;
                    if (_flowDepth == 0) _inFlowContext = false;
                    continue;
                }

                if (c == ',' && _inFlowContext)
                {
                    yield return new Token(TokenType.Comma, null, _line, _column);
                    Advance();
                    continue;
                }

                // Anchor (&name)
                if (c == '&')
                {
                    Advance();
                    var name = ReadAnchorOrAliasName();
                    yield return new Token(TokenType.Anchor, name, _line, _column);
                    continue;
                }

                // Alias (*name)
                if (c == '*')
                {
                    Advance();
                    var name = ReadAnchorOrAliasName();
                    yield return new Token(TokenType.Alias, name, _line, _column);
                    continue;
                }

                // Tag (!tag or !!type)
                if (c == '!')
                {
                    var tag = ReadTag();
                    yield return new Token(TokenType.Tag, tag, _line, _column);
                    continue;
                }

                // Dash (sequence item)
                if (c == '-' && !_inFlowContext && IsFollowedByWhitespaceOrEnd())
                {
                    yield return new Token(TokenType.Dash, null, _line, _column);
                    Advance();
                    SkipWhitespace();
                    continue;
                }

                // Block scalars
                if (c == '|' || c == '>')
                {
                    bool literal = c == '|';
                    Advance();
                    yield return new Token(literal ? TokenType.BlockLiteral : TokenType.BlockFolded, null, _line, _column);
                    continue;
                }

                // Key-value or scalar
                foreach (var tok in ReadKeyOrScalar())
                    yield return tok;
            }

            // Emit remaining dedents
            while (_indentStack.Count > 1)
            {
                _indentStack.Pop();
                yield return new Token(TokenType.Dedent, null, _line, _column);
            }

            yield return new Token(TokenType.EOF, null, _line, _column);
        }

        private bool IsAtLineStart()
        {
            if (_pos == 0) return true;
            return _pos > 0 && _input[_pos - 1] == '\n';
        }

        private bool IsFollowedByWhitespaceOrEnd()
        {
            int next = _pos + 1;
            if (next >= _input.Length) return true;
            char ch = _input[next];
            return ch == ' ' || ch == '\t' || ch == '\n';
        }

        private IEnumerable<Token> HandleIndentation()
        {
            int spaces = 0;
            while (_pos < _input.Length && _input[_pos] == ' ')
            {
                spaces++;
                Advance();
            }

            // Skip blank lines and comment-only lines for indentation purposes
            if (_pos >= _input.Length || _input[_pos] == '\n' || _input[_pos] == '#')
            {
                yield break;
            }

            int current = _indentStack.Peek();

            if (spaces > current)
            {
                _indentStack.Push(spaces);
                yield return new Token(TokenType.Indent, null, _line, 1);
            }
            else if (spaces < current)
            {
                while (_indentStack.Count > 1 && spaces < _indentStack.Peek())
                {
                    _indentStack.Pop();
                    yield return new Token(TokenType.Dedent, null, _line, 1);
                }
            }
        }

        private IEnumerable<Token> ReadKeyOrScalar()
        {
            int startLine = _line;
            int startCol = _column;

            // Check for merge key
            if (PeekChar() == '<' && _pos + 1 < _input.Length && _input[_pos + 1] == '<')
            {
                Advance();
                Advance();
                yield return new Token(TokenType.MergeKey, "<<", startLine, startCol);
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == ':')
                {
                    Advance();
                    SkipWhitespace();
                }
                yield break;
            }

            string value = ReadValue();

            if (string.IsNullOrEmpty(value))
                yield break;

            // Check if this is a key (followed by colon)
            SkipWhitespace();

            if (_pos < _input.Length && _input[_pos] == ':')
            {
                // Check if colon is followed by space, newline, or end
                int nextPos = _pos + 1;
                bool isKey = nextPos >= _input.Length ||
                             _input[nextPos] == ' ' ||
                             _input[nextPos] == '\n' ||
                             _input[nextPos] == '\t' ||
                             (_inFlowContext && (_input[nextPos] == ',' || _input[nextPos] == '}' || _input[nextPos] == ']'));

                if (isKey)
                {
                    yield return new Token(TokenType.Key, value, startLine, startCol);
                    Advance(); // consume ':'
                    SkipWhitespace();

                    // Read the value after the colon
                    if (_pos < _input.Length && _input[_pos] != '\n' && _input[_pos] != '#')
                    {
                        // Check for block scalar indicators
                        if (_input[_pos] == '|')
                        {
                            Advance();
                            yield return new Token(TokenType.BlockLiteral, null, _line, _column);
                        }
                        else if (_input[_pos] == '>')
                        {
                            Advance();
                            yield return new Token(TokenType.BlockFolded, null, _line, _column);
                        }
                        else if (_input[_pos] == '&')
                        {
                            Advance();
                            var anchorName = ReadAnchorOrAliasName();
                            yield return new Token(TokenType.Anchor, anchorName, _line, _column);
                            SkipWhitespace();
                            // Continue to read scalar after anchor
                            if (_pos < _input.Length && _input[_pos] != '\n' && _input[_pos] != '#')
                            {
                                string scalarVal = ReadValue();
                                if (!string.IsNullOrEmpty(scalarVal))
                                    yield return new Token(TokenType.Scalar, scalarVal, _line, _column);
                            }
                        }
                        else if (_input[_pos] == '*')
                        {
                            Advance();
                            var aliasName = ReadAnchorOrAliasName();
                            yield return new Token(TokenType.Alias, aliasName, _line, _column);
                        }
                        else if (_input[_pos] == '[' || _input[_pos] == '{')
                        {
                            // Flow collection - don't read as scalar, let main loop handle it
                        }
                        else
                        {
                            string scalarVal = ReadValue();
                            yield return new Token(TokenType.Scalar, scalarVal, _line, _column);
                        }
                    }
                    else
                    {
                        // Empty value (nested structure follows)
                        yield return new Token(TokenType.Scalar, "", _line, _column);
                    }
                    yield break;
                }
            }

            // It's just a scalar
            yield return new Token(TokenType.Scalar, value, startLine, startCol);
        }

        private string ReadValue()
        {
            // Handle quoted strings
            if (_pos < _input.Length && (_input[_pos] == '"' || _input[_pos] == '\''))
            {
                return ReadQuotedString();
            }

            var sb = new StringBuilder();
            while (_pos < _input.Length)
            {
                char c = _input[_pos];

                // Stop at structural characters
                if (c == '\n' || c == '#')
                    break;

                if (_inFlowContext && (c == ',' || c == ']' || c == '}' || c == ':'))
                    break;

                if (!_inFlowContext && c == ':')
                {
                    // Check if this is a key separator
                    int nextPos = _pos + 1;
                    if (nextPos >= _input.Length || _input[nextPos] == ' ' || _input[nextPos] == '\n')
                        break;
                }

                sb.Append(c);
                Advance();
            }

            return sb.ToString().Trim();
        }

        private string ReadQuotedString()
        {
            char quote = _input[_pos];
            Advance(); // consume opening quote

            var sb = new StringBuilder();
            bool escaped = false;

            while (_pos < _input.Length)
            {
                char c = _input[_pos];

                if (escaped)
                {
                    sb.Append(GetEscapedChar(c));
                    escaped = false;
                    Advance();
                    continue;
                }

                if (c == '\\' && quote == '"')
                {
                    escaped = true;
                    Advance();
                    continue;
                }

                if (c == quote)
                {
                    Advance(); // consume closing quote
                    break;
                }

                sb.Append(c);
                Advance();
            }

            return sb.ToString();
        }

        private char GetEscapedChar(char c)
        {
            return c switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                '0' => '\0',
                'a' => '\a',
                'b' => '\b',
                'f' => '\f',
                'v' => '\v',
                '/' => '/',
                _ => c
            };
        }

        private string ReadAnchorOrAliasName()
        {
            var sb = new StringBuilder();
            while (_pos < _input.Length)
            {
                char c = _input[_pos];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sb.Append(c);
                    Advance();
                }
                else
                {
                    break;
                }
            }
            return sb.ToString();
        }

        private string ReadTag()
        {
            var sb = new StringBuilder();
            sb.Append(_input[_pos]); // '!'
            Advance();

            // Handle !!type
            if (_pos < _input.Length && _input[_pos] == '!')
            {
                sb.Append(_input[_pos]);
                Advance();
            }

            while (_pos < _input.Length)
            {
                char c = _input[_pos];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '/' || c == ':')
                {
                    sb.Append(c);
                    Advance();
                }
                else
                {
                    break;
                }
            }

            SkipWhitespace();
            return sb.ToString();
        }

        private string ReadComment()
        {
            var sb = new StringBuilder();
            Advance(); // skip '#'
            while (_pos < _input.Length && _input[_pos] != '\n')
            {
                sb.Append(_input[_pos]);
                Advance();
            }
            return sb.ToString().Trim();
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && (_input[_pos] == ' ' || _input[_pos] == '\t'))
            {
                Advance();
            }
        }

        private char PeekChar() => _input[_pos];

        private void Advance()
        {
            if (_pos < _input.Length)
            {
                if (_input[_pos] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _pos++;
            }
        }
    }
}

