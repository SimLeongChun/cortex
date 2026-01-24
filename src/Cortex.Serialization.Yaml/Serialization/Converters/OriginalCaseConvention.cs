namespace Cortex.Serialization.Yaml.Converters
{
    public sealed class OriginalCaseConvention : INamingConvention
    {
        public string Convert(string n) => n;
    }
}
