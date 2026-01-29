using Cortex.Serialization.Yaml.Common;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Serialization.Yaml.Parser
{
    internal sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _idx;
        private readonly Dictionary<string, YamlNode> _anchors = new();
        private readonly List<string> _pendingComments = new();
        private bool _preserveComments;

        public Parser(IEnumerable<Token> tokens, bool preserveComments = false)
        {
            _tokens = tokens.ToList();
            _preserveComments = preserveComments;
        }

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

        private void CollectComments()
        {
            while (Peek().Type == TokenType.Comment)
            {
                var tok = Next();
                if (_preserveComments)
                    _pendingComments.Add(tok.Value ?? "");
            }
        }

        private void AttachComments(YamlNode node)
        {
            if (_preserveComments && _pendingComments.Count > 0)
            {
                node.Comments.AddRange(_pendingComments);
                _pendingComments.Clear();
            }
        }

        public YamlNode ParseDocument()
        {
            CollectComments();
            return ParseNode();
        }

        private YamlNode ParseNode()
        {
            CollectComments();

            // Handle tag
            string? tag = null;
            if (Peek().Type == TokenType.Tag)
            {
                tag = Next().Value;
                SkipWhitespaceTokens();
            }

            // Handle anchor
            string? anchor = null;
            if (Peek().Type == TokenType.Anchor)
            {
                anchor = Next().Value;
                SkipWhitespaceTokens();
            }

            // Handle alias
            if (Peek().Type == TokenType.Alias)
            {
                var aliasName = Next().Value!;
                if (_anchors.TryGetValue(aliasName, out var aliasedNode))
                {
                    return aliasedNode;
                }
                return new YamlAlias(aliasName);
            }

            YamlNode node;

            // Flow sequence [...]
            if (Peek().Type == TokenType.FlowSequenceStart)
            {
                node = ParseFlowSequence();
            }
            // Flow mapping {...}
            else if (Peek().Type == TokenType.FlowMappingStart)
            {
                node = ParseFlowMapping();
            }
            // Block mapping
            else if (Peek().Type == TokenType.Key || Peek().Type == TokenType.MergeKey)
            {
                node = ParseMapping();
            }
            // Block sequence
            else if (Peek().Type == TokenType.Dash)
            {
                node = ParseSequence();
            }
            // Scalar
            else
            {
                node = ParseScalar();
            }

            // Apply tag and anchor
            if (tag != null) node.Tag = tag;
            if (anchor != null)
            {
                node.Anchor = anchor;
                _anchors[anchor] = node;
            }

            AttachComments(node);
            return node;
        }

        private void SkipWhitespaceTokens()
        {
            while (Peek().Type == TokenType.NewLine)
            {
                Next();
                CollectComments();
            }
        }

        private YamlSequence ParseFlowSequence()
        {
            Next(); // consume '['
            var items = new List<YamlNode>();

            CollectComments();
            SkipWhitespaceTokens();

            while (Peek().Type != TokenType.FlowSequenceEnd && Peek().Type != TokenType.EOF)
            {
                CollectComments();
                items.Add(ParseNode());
                CollectComments();
                SkipWhitespaceTokens();

                if (Peek().Type == TokenType.Comma)
                {
                    Next();
                    CollectComments();
                    SkipWhitespaceTokens();
                }
            }

            if (Peek().Type == TokenType.FlowSequenceEnd)
                Next(); // consume ']'

            return new YamlSequence(items) { FlowStyle = true };
        }

        private YamlMapping ParseFlowMapping()
        {
            Next(); // consume '{'
            var dict = new Dictionary<string, YamlNode>();

            CollectComments();
            SkipWhitespaceTokens();

            while (Peek().Type != TokenType.FlowMappingEnd && Peek().Type != TokenType.EOF)
            {
                CollectComments();

                // Handle merge key in flow context
                if (Peek().Type == TokenType.MergeKey)
                {
                    Next();
                    SkipWhitespaceTokens();
                    CollectComments();

                    // Check for colon
                    if (Peek().Type == TokenType.Colon || Peek().Type == TokenType.Scalar)
                    {
                        if (Peek().Type == TokenType.Scalar && Peek().Value == ":")
                            Next();
                    }
                    SkipWhitespaceTokens();

                    var mergeValue = ParseNode();
                    if (mergeValue is YamlMapping mergeMapping)
                    {
                        foreach (var kvp in mergeMapping.Entries)
                        {
                            if (!dict.ContainsKey(kvp.Key))
                                dict[kvp.Key] = kvp.Value;
                        }
                    }
                    else if (mergeValue is YamlSequence mergeSeq)
                    {
                        foreach (var item in mergeSeq.Items)
                        {
                            if (item is YamlMapping itemMapping)
                            {
                                foreach (var kvp in itemMapping.Entries)
                                {
                                    if (!dict.ContainsKey(kvp.Key))
                                        dict[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Parse key
                    string key;
                    if (Peek().Type == TokenType.Key)
                    {
                        key = Next().Value!;
                        // The scanner already consumed the value, so check for scalar
                        CollectComments();
                        SkipWhitespaceTokens();
                        if (Peek().Type == TokenType.Scalar)
                        {
                            var val = ParseScalarValue(Next().Value!);
                            dict[key] = val;
                        }
                        else
                        {
                            dict[key] = ParseNode();
                        }
                    }
                    else if (Peek().Type == TokenType.Scalar)
                    {
                        key = Next().Value!;
                        CollectComments();
                        SkipWhitespaceTokens();

                        // Expect colon
                        if (Peek().Type == TokenType.Colon)
                            Next();

                        CollectComments();
                        SkipWhitespaceTokens();

                        dict[key] = ParseNode();
                    }
                }

                CollectComments();
                SkipWhitespaceTokens();

                if (Peek().Type == TokenType.Comma)
                {
                    Next();
                    CollectComments();
                    SkipWhitespaceTokens();
                }
            }

            if (Peek().Type == TokenType.FlowMappingEnd)
                Next(); // consume '}'

            return new YamlMapping(dict) { FlowStyle = true };
        }

        private YamlNode ParseMapping()
        {
            var dict = new Dictionary<string, YamlNode>();

            while (Peek().Type == TokenType.Key || Peek().Type == TokenType.MergeKey)
            {
                CollectComments();

                // Handle merge key
                if (Peek().Type == TokenType.MergeKey)
                {
                    Next();
                    SkipWhitespaceTokens();

                    // Expect a colon after <<
                    // The scanner might have already handled the colon, or we need to skip the scalar
                    CollectComments();
                    Match(TokenType.NewLine);
                    CollectComments();

                    while (Match(TokenType.Indent))
                    {
                        var mergeValue = ParseNode();
                        ApplyMerge(dict, mergeValue);
                    }

                    while (Match(TokenType.Dedent)) { }
                    continue;
                }

                var keyTok = Next();
                var key = keyTok.Value!;
                var after = Next();

                if (after.Type == TokenType.BlockLiteral)
                {
                    var text = ReadBlock(true);
                    dict[key] = new YamlScalar(text) { Style = ScalarStyle.Literal };
                }
                else if (after.Type == TokenType.BlockFolded)
                {
                    var text = ReadBlock(false);
                    dict[key] = new YamlScalar(text) { Style = ScalarStyle.Folded };
                }
                else if (after.Type == TokenType.Anchor)
                {
                    string anchorName = after.Value!;
                    CollectComments();

                    // Read the value after the anchor
                    YamlNode valueNode;
                    if (Peek().Type == TokenType.Scalar)
                    {
                        valueNode = ParseScalarValue(Next().Value!);
                    }
                    else if (Peek().Type == TokenType.NewLine)
                    {
                        Match(TokenType.NewLine);
                        CollectComments();
                        while (Match(TokenType.Indent))
                        {
                            valueNode = ParseNode();
                            valueNode.Anchor = anchorName;
                            _anchors[anchorName] = valueNode;
                            dict[key] = valueNode;
                        }
                        while (Match(TokenType.Dedent)) { }
                        continue;
                    }
                    else
                    {
                        valueNode = ParseNode();
                    }

                    valueNode.Anchor = anchorName;
                    _anchors[anchorName] = valueNode;
                    dict[key] = valueNode;

                    Match(TokenType.NewLine);
                    while (Match(TokenType.Dedent)) { }
                    continue;
                }
                else if (after.Type == TokenType.Alias)
                {
                    var aliasName = after.Value!;
                    if (_anchors.TryGetValue(aliasName, out var aliasedNode))
                    {
                        dict[key] = aliasedNode;
                    }
                    else
                    {
                        dict[key] = new YamlAlias(aliasName);
                    }
                    Match(TokenType.NewLine);
                    while (Match(TokenType.Dedent)) { }
                    continue;
                }
                else if (after.Type == TokenType.FlowSequenceStart)
                {
                    _idx--; // Back up to re-parse the flow sequence
                    dict[key] = ParseFlowSequence();
                    Match(TokenType.NewLine);
                    while (Match(TokenType.Dedent)) { }
                    continue;
                }
                else if (after.Type == TokenType.FlowMappingStart)
                {
                    _idx--; // Back up to re-parse the flow mapping
                    dict[key] = ParseFlowMapping();
                    Match(TokenType.NewLine);
                    while (Match(TokenType.Dedent)) { }
                    continue;
                }
                else if (after.Type == TokenType.Scalar)
                {
                    var scalarValue = after.Value!;
                    dict[key] = ParseScalarValue(scalarValue);

                    if (Match(TokenType.NewLine))
                    {
                        CollectComments();
                    }

                    // Only parse nested content if the scalar was empty
                    if (string.IsNullOrEmpty(scalarValue))
                    {
                        while (Match(TokenType.Indent))
                        {
                            CollectComments();
                            dict[key] = ParseNode();
                        }
                        while (Match(TokenType.Dedent)) { }
                    }
                    else
                    {
                        if (Peek().Type == TokenType.Dedent)
                        {
                            break;
                        }
                        if (Peek().Type == TokenType.Indent)
                        {
                            Next();
                        }
                    }
                    continue;
                }

                if (Match(TokenType.NewLine))
                {
                    CollectComments();
                }

                while (Match(TokenType.Dedent)) { }

                while (Match(TokenType.Indent))
                {
                    CollectComments();
                    dict[key] = ParseNode();
                }
            }

            return new YamlMapping(dict);
        }

        private void ApplyMerge(Dictionary<string, YamlNode> dict, YamlNode mergeValue)
        {
            if (mergeValue is YamlMapping mergeMapping)
            {
                foreach (var kvp in mergeMapping.Entries)
                {
                    if (!dict.ContainsKey(kvp.Key))
                        dict[kvp.Key] = kvp.Value;
                }
            }
            else if (mergeValue is YamlSequence mergeSeq)
            {
                foreach (var item in mergeSeq.Items)
                {
                    ApplyMerge(dict, item);
                }
            }
            else if (mergeValue is YamlAlias alias && _anchors.TryGetValue(alias.Name, out var aliased))
            {
                ApplyMerge(dict, aliased);
            }
        }

        private YamlNode ParseSequence()
        {
            var list = new List<YamlNode>();

            while (Peek().Type == TokenType.Dash)
            {
                Next();
                CollectComments();

                if (Peek().Type == TokenType.NewLine)
                {
                    Next();
                    CollectComments();
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
                    else if (Peek().Type == TokenType.Anchor)
                    {
                        var anchorName = Next().Value!;
                        var node = ParseNode();
                        node.Anchor = anchorName;
                        _anchors[anchorName] = node;
                        list.Add(node);
                    }
                    else if (Peek().Type == TokenType.Alias)
                    {
                        var aliasName = Next().Value!;
                        if (_anchors.TryGetValue(aliasName, out var aliasedNode))
                            list.Add(aliasedNode);
                        else
                            list.Add(new YamlAlias(aliasName));
                    }
                    else if (Peek().Type == TokenType.FlowSequenceStart)
                    {
                        list.Add(ParseFlowSequence());
                    }
                    else if (Peek().Type == TokenType.FlowMappingStart)
                    {
                        list.Add(ParseFlowMapping());
                    }
                    else
                    {
                        list.Add(ParseNode());
                    }

                    while (Match(TokenType.Dedent)) { }
                }

                Match(TokenType.NewLine);
                CollectComments();
            }

            return new YamlSequence(list);
        }

        private YamlNode ParseScalar()
        {
            CollectComments();

            var tok = Next();
            if (tok.Type != TokenType.Scalar)
                throw new YamlException($"Expected scalar but got {tok.Type}", tok.Line, tok.Column);

            var node = ParseScalarValue(tok.Value!);
            AttachComments(node);
            return node;
        }

        private YamlNode ParseScalarValue(string raw)
        {
            if (raw == "null" || raw == "~")
                return new YamlScalar(null);

            if (raw == "true" || raw == "false")
                return new YamlScalar(bool.Parse(raw));

            if (int.TryParse(raw, out var i))
                return new YamlScalar(i);

            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
                return new YamlScalar(d);

            // Handle quoted strings
            if ((raw.StartsWith('"') && raw.EndsWith('"')) || (raw.StartsWith('\'') && raw.EndsWith('\'')))
            {
                var style = raw.StartsWith('"') ? ScalarStyle.DoubleQuoted : ScalarStyle.SingleQuoted;
                return new YamlScalar(raw[1..^1]) { Style = style };
            }

            return new YamlScalar(raw);
        }

        private string ReadBlock(bool literal)
        {
            if (!Match(TokenType.NewLine)) { }
            CollectComments();

            if (!Match(TokenType.Indent))
                throw new YamlException("Expected indentation for block scalar", Peek().Line, Peek().Column);

            var sb = new System.Text.StringBuilder();

            while (Peek().Type is TokenType.Scalar or TokenType.Key or TokenType.Dash)
            {
                var tok = Next();
                if (tok.Type == TokenType.Scalar)
                {
                    if (literal)
                    {
                        if (sb.Length > 0)
                            sb.Append('\n');
                        sb.Append(tok.Value);
                    }
                    else
                    {
                        if (sb.Length > 0)
                            sb.Append(' ');
                        sb.Append(tok.Value);
                    }
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
                CollectComments();

                if (Peek().Type == TokenType.Dedent)
                    break;
            }

            Match(TokenType.Dedent);

            return sb.ToString();
        }

        /// <summary>
        /// Gets all anchors that were defined during parsing.
        /// </summary>
        public Dictionary<string, YamlNode> GetAnchors() => new(_anchors);
    }
}
