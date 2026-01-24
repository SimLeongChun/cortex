namespace Cortex.Serialization.Yaml.Converters
{
    public sealed class KebabCaseConvention : INamingConvention
    {
        public string Convert(string n) => Common.StringUtils.ToKebabCase(n);
    }
}
