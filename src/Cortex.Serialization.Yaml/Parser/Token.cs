using System.Collections.Generic;

namespace Cortex.Serialization.Yaml.Parser
{
    internal sealed record Token(TokenType Type, string? Value, int Line, int Column);

    internal abstract record YamlNode
    {
        /// <summary>
        /// Optional anchor name for this node (without the '&amp;' prefix).
        /// </summary>
        public string? Anchor { get; set; }

        /// <summary>
        /// Optional tag for this node (e.g., "!custom" or "!!str").
        /// </summary>
        public string? Tag { get; set; }

        /// <summary>
        /// Comments associated with this node.
        /// </summary>
        public List<string> Comments { get; set; } = new();
    }

    internal sealed record YamlScalar(object? Value) : YamlNode
    {
        /// <summary>
        /// Indicates the scalar style when serialized.
        /// </summary>
        public ScalarStyle Style { get; set; } = ScalarStyle.Plain;
    }

    internal sealed record YamlSequence(List<YamlNode> Items) : YamlNode
    {
        /// <summary>
        /// Indicates whether to use flow style [item1, item2] or block style (default).
        /// </summary>
        public bool FlowStyle { get; set; }
    }

    internal sealed record YamlMapping(Dictionary<string, YamlNode> Entries) : YamlNode
    {
        /// <summary>
        /// Indicates whether to use flow style {key: value} or block style (default).
        /// </summary>
        public bool FlowStyle { get; set; }
    }

    internal sealed record YamlAlias(string Name) : YamlNode;

    /// <summary>
    /// Represents scalar quoting/formatting styles.
    /// </summary>
    internal enum ScalarStyle
    {
        Plain,
        SingleQuoted,
        DoubleQuoted,
        Literal,  // |
        Folded    // >
    }
}

