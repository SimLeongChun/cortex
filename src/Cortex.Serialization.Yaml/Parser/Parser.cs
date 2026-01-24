using Cortex.Serialization.Yaml.Common;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Serialization.Yaml.Parser
{
    internal sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _idx;
        public Parser(IEnumerable<Token> tokens) => _tokens = tokens.ToList();
        private Token Peek() => _tokens[_idx];
        private Token Next() => _tokens[_idx++];
        private bool Match(TokenType t)
        {
            if (Peek().Type == t)
            {
                _idx++;
                return true;
            }
            return false;
        }
        public YamlNode ParseDocument() => ParseNode();
        private YamlNode ParseNode()
        {
            if (Peek().Type == TokenType.Key)
                return ParseMapping();

            if (Peek().Type == TokenType.Dash)
                return ParseSequence();

            return ParseScalar();
        }
        private YamlNode ParseMapping()
        {
            var dict = new Dictionary<string, YamlNode>();
            while (Peek().Type == TokenType.Key)
            {
                var keyTok = Next();
                var key = keyTok.Value!;
                var after = Next();

                if (after.Type == TokenType.BlockLiteral)
                {
                    var text = ReadBlock(true);
                    dict[key] = new YamlScalar(text);
                }
                else if (after.Type == TokenType.BlockFolded)
                {
                    var text = ReadBlock(false);
                    dict[key] = new YamlScalar(text);
                }
                else if (after.Type == TokenType.Scalar)
                {
                    dict[key] = ParseScalarValue(after.Value!);
                }
                if (Match(TokenType.NewLine)) { }

                while (Match(TokenType.Dedent)) { }

                while (Match(TokenType.Indent))
                {
                    dict[key] = ParseNode();
                }
            }
            return new YamlMapping(dict);
        }
        private YamlNode ParseSequence()
        {
            var list = new List<YamlNode>();
            while (Peek().Type == TokenType.Dash)
            {
                Next();

                if (Peek().Type == TokenType.NewLine)
                {
                    Next();
                    Match(TokenType.Indent);
                    list.Add(ParseNode());
                    while (Match(TokenType.Dedent)) { }
                }
                else
                {
                    if (Peek().Type == TokenType.Key)
                        list.Add(ParseMapping());
                    else if (Peek().Type == TokenType.Scalar)
                        list.Add(ParseScalar());
                }

                Match(TokenType.NewLine);
            }
            return new YamlSequence(list);
        }

        private YamlNode ParseScalar()
        {
            var tok = Next();
            if (tok.Type != TokenType.Scalar)
                throw new YamlException($"Expected scalar but got {tok.Type}", tok.Line, tok.Column);
            return ParseScalarValue(tok.Value!);
        }

        private YamlNode ParseScalarValue(string raw)
        {
            if (raw == "null" || raw == "~")
                return new YamlScalar(null);

            if (raw == "true" || raw == "false")
                return new YamlScalar(bool.Parse(raw));

            if (int.TryParse(raw, out var i))
                return new YamlScalar(i);

            if (double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                return new YamlScalar(d);

            if ((raw.StartsWith('"') && raw.EndsWith('"')) || (raw.StartsWith('\'') && raw.EndsWith('\'')))
                return new YamlScalar(raw[1..^1]);

            return new YamlScalar(raw);
        }
        private string ReadBlock(bool literal)
        {
            if (!Match(TokenType.NewLine)) { }
            if (!Match(TokenType.Indent))
                throw new YamlException("Expected indentation for block scalar", Peek().Line, Peek().Column);

            var sb = new System.Text.StringBuilder();
            while (Peek().Type is TokenType.Scalar or TokenType.Key or TokenType.Dash)
            {
                var tok = Next();
                if (tok.Type == TokenType.Scalar)
                {
                    if (!literal && sb.Length > 0)
                        sb.Append(' ');
                    sb.Append(tok.Value);
                }
                else if (tok.Type == TokenType.Key)
                {
                    sb.Append(tok.Value).Append(':');
                    if (Peek().Type == TokenType.Scalar)
                        sb.Append(' ').Append(Next().Value);
                }
                else if (tok.Type == TokenType.Dash)
                {
                    sb.Append("- ");
                    if (Peek().Type == TokenType.Scalar)
                        sb.Append(Next().Value);
                }

                Match(TokenType.NewLine);

                if (Peek().Type == TokenType.Dedent)
                    break;
            }
            Match(TokenType.Dedent);

            return sb.ToString();
        }
    }
}