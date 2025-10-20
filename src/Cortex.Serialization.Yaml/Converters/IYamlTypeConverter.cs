namespace Cortex.Serialization.Yaml.Converters
{
    public interface IYamlTypeConverter
    {
        bool CanConvert(Type t);
        object? Read(object? yamlNode, Type targetType);
        object? Write(object? clrObject, Type declaredType);
    }
}