namespace Cortex.Serialization.Yaml.Parser
{
    internal enum TokenType
    {
        Scalar,
        Key,
        Dash,
        NewLine,
        Indent,
        Dedent,
        BlockLiteral,
        BlockFolded,
        EOF,

        // Flow style tokens
        FlowSequenceStart,   // [
        FlowSequenceEnd,     // ]
        FlowMappingStart,    // {
        FlowMappingEnd,      // }
        Comma,               // ,
        Colon,               // :

        // Anchor and alias tokens
        Anchor,              // &anchor
        Alias,               // *alias
        MergeKey,            // <<

        // Tag tokens
        Tag,                 // !tag or !!type

        // Comment token
        Comment              // # comment
    }
}

