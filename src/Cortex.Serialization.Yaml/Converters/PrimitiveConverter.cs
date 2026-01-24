using System;

namespace Cortex.Serialization.Yaml.Converters
{
    public sealed class PrimitiveConverter : IYamlTypeConverter
    {
        public bool CanConvert(Type t) => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid) || t == typeof(DateOnly) || t == typeof(TimeOnly);
        public object? Read(object? yamlNode, Type targetType)
        {
            if (yamlNode is null)
                return null;
            if (targetType == typeof(string))
                return yamlNode.ToString();
            if (targetType == typeof(bool))
                return yamlNode is bool b ? b : bool.Parse(yamlNode.ToString()!);
            if (targetType == typeof(int))
                return Convert.ToInt32(yamlNode, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return Convert.ToInt64(yamlNode, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return Convert.ToDouble(yamlNode, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return Convert.ToDecimal(yamlNode, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(Guid))
                return Guid.Parse(yamlNode.ToString()!);
            if (targetType == typeof(DateTime))
                return DateTime.Parse(yamlNode.ToString()!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            if (targetType == typeof(DateOnly))
                return DateOnly.Parse(yamlNode.ToString()!, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(TimeOnly))
                return TimeOnly.Parse(yamlNode.ToString()!, System.Globalization.CultureInfo.InvariantCulture);

            return yamlNode;
        }
        public object? Write(object? clrObject, Type declaredType) => clrObject;
    }
}
