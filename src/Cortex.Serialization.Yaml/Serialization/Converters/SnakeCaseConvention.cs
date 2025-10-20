namespace Cortex.Serialization.Yaml.Converters
{
    public sealed class SnakeCaseConvention : INamingConvention
    {
        public string Convert(string n) => Common.StringUtils.ToSnakeCase(n);
    }
}
