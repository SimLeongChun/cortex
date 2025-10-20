using Cortex.Serialization.Yaml.Parser;
using System;
using System.Text;

namespace Cortex.Serialization.Yaml.Emitter
{
    internal sealed class Emitter
    {
        private readonly StringBuilder _sb = new();
        private readonly int _indentSize;

        public Emitter(int indentSize = 2) => _indentSize = indentSize;
        public string Emit(YamlNode node)
        {
            WriteNode(node, 0);
            return _sb.ToString();
        }

        private void Indent(int level) => _sb.Append(' ', _indentSize * level);

        private void WriteNode(YamlNode node, int level)
        {
            switch (node)
            {
                case YamlScalar s: WriteScalar(s.Value); _sb.Append('\n'); break;
                case YamlSequence seq:
                    foreach (var item in seq.Items)
                    {
                        Indent(level); _sb.Append("- ");
                        if (item is YamlScalar sc)
                        {
                            WriteScalar(sc.Value);
                            _sb.Append('\n');
                        }
                        else
                        {
                            _sb.Append('\n');
                            WriteNode(item, level + 1);
                        }
                    }
                    break;
                case YamlMapping map:
                    foreach (var kvp in map.Entries)
                    {
                        Indent(level); _sb.Append(kvp.Key).Append(": ");
                        if (kvp.Value is YamlScalar sv) { WriteScalar(sv.Value); _sb.Append('\n'); }
                        else { _sb.Append('\n'); WriteNode(kvp.Value, level + 1); }
                    }
                    break;
            }
        }
        private void WriteScalar(object? value)
        {
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
                if (s.Contains(':') || s.StartsWith(' ') || s.EndsWith(' ') || s.Contains('#') || s.Contains('\n'))
                {
                    _sb.Append('"')
                        .Append(s.Replace("\"", "\\\""))
                        .Append('"');
                }
                else _sb.Append(s); return;
            }

            _sb
                .Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
