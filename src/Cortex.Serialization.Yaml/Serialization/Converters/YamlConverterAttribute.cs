using System;

namespace Cortex.Serialization.Yaml.Converters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
    public sealed class YamlConverterAttribute : Attribute
    {
        public Type ConverterType { get; }
        public YamlConverterAttribute(Type t) => ConverterType = t;
    }
}
