namespace Cortex.Serialization.Yaml.Converters
{
    public interface INamingConvention
    {
        string Convert(string name);
    }
}
