using Cortex.Serialization.Yaml.Parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cortex.Serialization.Yaml.Emitter
{
    internal sealed class Emitter
    {
        private readonly StringBuilder _sb = new();
        private readonly int _indentSize;
        private readonly bool _emitComments;
        private readonly bool _preferFlowStyle;
        private readonly int _flowStyleThreshold;
        private readonly HashSet<string> _emittedAnchors = new();

        public Emitter(int indentSize = 2, bool emitComments = true, bool preferFlowStyle = false, int flowStyleThreshold = 80)
        {
            _indentSize = indentSize;
            _emitComments = emitComments;
            _preferFlowStyle = preferFlowStyle;
            _flowStyleThreshold = flowStyleThreshold;
        }

        public string Emit(YamlNode node)
        {
            WriteNode(node, 0, isRoot: true);
            return _sb.ToString();
        }

        private void Indent(int level) => _sb.Append(' ', _indentSize * level);

        private void WriteComments(YamlNode node, int level)
        {
            if (!_emitComments || node.Comments.Count == 0)
                return;

            foreach (var comment in node.Comments)
            {
                Indent(level);
                _sb.Append("# ").Append(comment).Append('\n');
            }
        }

        private void WriteAnchorAndTag(YamlNode node)
        {
            if (!string.IsNullOrEmpty(node.Tag))
            {
                _sb.Append(node.Tag).Append(' ');
            }

            if (!string.IsNullOrEmpty(node.Anchor))
            {
                if (_emittedAnchors.Add(node.Anchor))
                {
                    _sb.Append('&').Append(node.Anchor).Append(' ');
                }
            }
        }

        private void WriteNode(YamlNode node, int level, bool isRoot = false, bool inlineSequenceItem = false)
        {
            // Handle alias nodes
            if (node is YamlAlias alias)
            {
                _sb.Append('*').Append(alias.Name);
                if (!inlineSequenceItem)
                    _sb.Append('\n');
                return;
            }

            WriteComments(node, level);

            switch (node)
            {
                case YamlScalar s:
                    WriteAnchorAndTag(node);
                    WriteScalar(s);
                    if (!inlineSequenceItem)
                        _sb.Append('\n');
                    break;

                case YamlSequence seq:
                    WriteSequence(seq, level, isRoot, inlineSequenceItem);
                    break;

                case YamlMapping map:
                    WriteMapping(map, level, isRoot, inlineSequenceItem);
                    break;
            }
        }

        private void WriteSequence(YamlSequence seq, int level, bool isRoot, bool inlineSequenceItem)
        {
            // Determine if we should use flow style
            bool useFlowStyle = seq.FlowStyle || (_preferFlowStyle && ShouldUseFlowStyleForSequence(seq));

            if (useFlowStyle)
            {
                WriteAnchorAndTag(seq);
                WriteFlowSequence(seq);
                if (!inlineSequenceItem)
                    _sb.Append('\n');
            }
            else
            {
                if (!string.IsNullOrEmpty(seq.Tag) || !string.IsNullOrEmpty(seq.Anchor))
                {
                    WriteAnchorAndTag(seq);
                    _sb.Append('\n');
                }

                foreach (var item in seq.Items)
                {
                    WriteComments(item, level);
                    Indent(level);
                    _sb.Append("- ");

                    if (item is YamlScalar sc)
                    {
                        WriteAnchorAndTag(item);
                        WriteScalar(sc);
                        _sb.Append('\n');
                    }
                    else if (item is YamlAlias alias)
                    {
                        _sb.Append('*').Append(alias.Name).Append('\n');
                    }
                    else if (item is YamlMapping itemMap && !itemMap.FlowStyle)
                    {
                        // Inline mapping after dash
                        WriteAnchorAndTag(item);
                        _sb.Append('\n');
                        WriteNode(item, level + 1);
                    }
                    else if (item is YamlSequence itemSeq && itemSeq.FlowStyle)
                    {
                        WriteAnchorAndTag(item);
                        WriteFlowSequence(itemSeq);
                        _sb.Append('\n');
                    }
                    else
                    {
                        WriteAnchorAndTag(item);
                        _sb.Append('\n');
                        WriteNode(item, level + 1);
                    }
                }
            }
        }

        private void WriteMapping(YamlMapping map, int level, bool isRoot, bool inlineSequenceItem)
        {
            // Determine if we should use flow style
            bool useFlowStyle = map.FlowStyle || (_preferFlowStyle && ShouldUseFlowStyleForMapping(map));

            if (useFlowStyle)
            {
                WriteAnchorAndTag(map);
                WriteFlowMapping(map);
                if (!inlineSequenceItem)
                    _sb.Append('\n');
            }
            else
            {
                if (!string.IsNullOrEmpty(map.Tag) || !string.IsNullOrEmpty(map.Anchor))
                {
                    WriteAnchorAndTag(map);
                    if (!isRoot)
                        _sb.Append('\n');
                }

                foreach (var kvp in map.Entries)
                {
                    WriteComments(kvp.Value, level);
                    Indent(level);
                    _sb.Append(EscapeKey(kvp.Key)).Append(": ");

                    if (kvp.Value is YamlScalar sv)
                    {
                        WriteAnchorAndTag(kvp.Value);
                        WriteScalar(sv);
                        _sb.Append('\n');
                    }
                    else if (kvp.Value is YamlAlias alias)
                    {
                        _sb.Append('*').Append(alias.Name).Append('\n');
                    }
                    else if (kvp.Value is YamlSequence seq && seq.FlowStyle)
                    {
                        WriteAnchorAndTag(kvp.Value);
                        WriteFlowSequence(seq);
                        _sb.Append('\n');
                    }
                    else if (kvp.Value is YamlMapping innerMap && innerMap.FlowStyle)
                    {
                        WriteAnchorAndTag(kvp.Value);
                        WriteFlowMapping(innerMap);
                        _sb.Append('\n');
                    }
                    else
                    {
                        _sb.Append('\n');
                        WriteNode(kvp.Value, level + 1);
                    }
                }
            }
        }

        private void WriteFlowSequence(YamlSequence seq)
        {
            _sb.Append('[');
            bool first = true;

            foreach (var item in seq.Items)
            {
                if (!first) _sb.Append(", ");
                first = false;

                if (item is YamlScalar sc)
                {
                    WriteAnchorAndTag(item);
                    WriteScalar(sc);
                }
                else if (item is YamlAlias alias)
                {
                    _sb.Append('*').Append(alias.Name);
                }
                else if (item is YamlSequence innerSeq)
                {
                    WriteAnchorAndTag(item);
                    WriteFlowSequence(innerSeq);
                }
                else if (item is YamlMapping innerMap)
                {
                    WriteAnchorAndTag(item);
                    WriteFlowMapping(innerMap);
                }
            }

            _sb.Append(']');
        }

        private void WriteFlowMapping(YamlMapping map)
        {
            _sb.Append('{');
            bool first = true;

            foreach (var kvp in map.Entries)
            {
                if (!first) _sb.Append(", ");
                first = false;

                _sb.Append(EscapeKey(kvp.Key)).Append(": ");

                if (kvp.Value is YamlScalar sv)
                {
                    WriteAnchorAndTag(kvp.Value);
                    WriteScalar(sv);
                }
                else if (kvp.Value is YamlAlias alias)
                {
                    _sb.Append('*').Append(alias.Name);
                }
                else if (kvp.Value is YamlSequence innerSeq)
                {
                    WriteAnchorAndTag(kvp.Value);
                    WriteFlowSequence(innerSeq);
                }
                else if (kvp.Value is YamlMapping innerMap)
                {
                    WriteAnchorAndTag(kvp.Value);
                    WriteFlowMapping(innerMap);
                }
            }

            _sb.Append('}');
        }

        private bool ShouldUseFlowStyleForSequence(YamlSequence seq)
        {
            // Use flow style for simple, short sequences
            if (seq.Items.Count > 5) return false;

            foreach (var item in seq.Items)
            {
                if (item is not YamlScalar) return false;
            }

            // Estimate the length
            int estimatedLength = 2; // []
            foreach (var item in seq.Items)
            {
                if (item is YamlScalar s)
                {
                    estimatedLength += (s.Value?.ToString()?.Length ?? 4) + 2;
                }
            }

            return estimatedLength < _flowStyleThreshold;
        }

        private bool ShouldUseFlowStyleForMapping(YamlMapping map)
        {
            // Use flow style for simple, short mappings
            if (map.Entries.Count > 3) return false;

            foreach (var kvp in map.Entries)
            {
                if (kvp.Value is not YamlScalar) return false;
            }

            // Estimate the length
            int estimatedLength = 2; // {}
            foreach (var kvp in map.Entries)
            {
                estimatedLength += kvp.Key.Length + 2;
                if (kvp.Value is YamlScalar s)
                {
                    estimatedLength += (s.Value?.ToString()?.Length ?? 4) + 2;
                }
            }

            return estimatedLength < _flowStyleThreshold;
        }

        private void WriteScalar(YamlScalar scalar)
        {
            var value = scalar.Value;

            if (value is null)
            {
                _sb.Append("null");
                return;
            }

            if (value is bool b)
            {
                _sb.Append(b ? "true" : "false");
                return;
            }

            if (value is string s)
            {
                WriteStringScalar(s, scalar.Style);
                return;
            }

            _sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        private void WriteStringScalar(string s, ScalarStyle style)
        {
            // Determine the best style based on content
            if (style == ScalarStyle.Plain && NeedsQuoting(s))
            {
                style = ScalarStyle.DoubleQuoted;
            }

            switch (style)
            {
                case ScalarStyle.SingleQuoted:
                    _sb.Append('\'');
                    _sb.Append(s.Replace("'", "''"));
                    _sb.Append('\'');
                    break;

                case ScalarStyle.DoubleQuoted:
                    _sb.Append('"');
                    _sb.Append(EscapeDoubleQuoted(s));
                    _sb.Append('"');
                    break;

                default:
                    _sb.Append(s);
                    break;
            }
        }

        private static bool NeedsQuoting(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;

            // Check if it looks like a special value
            if (s == "null" || s == "~" || s == "true" || s == "false")
                return true;

            // Check if it looks like a number
            if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return true;

            // Check for special characters
            if (s.Contains(':') || s.Contains('#') || s.Contains('\n') ||
                s.Contains('\r') || s.Contains('\t') ||
                s.StartsWith(' ') || s.EndsWith(' ') ||
                s.StartsWith("'") || s.StartsWith("\"") ||
                s.StartsWith("&") || s.StartsWith("*") ||
                s.StartsWith("!") || s.StartsWith("|") ||
                s.StartsWith(">") || s.StartsWith("%") ||
                s.StartsWith("@") || s.StartsWith("`") ||
                s.Contains('{') || s.Contains('}') ||
                s.Contains('[') || s.Contains(']') ||
                s.Contains(','))
            {
                return true;
            }

            return false;
        }

        private static string EscapeDoubleQuoted(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\v': sb.Append("\\v"); break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append($"\\u{(int)c:X4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string EscapeKey(string key)
        {
            if (NeedsQuoting(key))
            {
                return $"\"{EscapeDoubleQuoted(key)}\"";
            }
            return key;
        }
    }
}

