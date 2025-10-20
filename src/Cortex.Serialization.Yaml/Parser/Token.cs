namespace Cortex.Serialization.Yaml.Parser
{
    internal sealed record Token(TokenType Type, string? Value, int Line, int Column);
    internal abstract record YamlNode;
    internal sealed record YamlScalar(object? Value) : YamlNode;
    internal sealed record YamlSequence(List<YamlNode> Items) : YamlNode;
    internal sealed record YamlMapping(Dictionary<string, YamlNode> Entries) : YamlNode;
}
