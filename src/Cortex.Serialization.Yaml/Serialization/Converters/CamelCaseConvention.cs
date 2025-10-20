namespace Cortex.Serialization.Yaml.Converters
{
    public sealed class CamelCaseConvention : INamingConvention 
    {
        public string Convert(string name)
        {
            return Common.StringUtils.ToCamelCase(name);
        }
    }
}
