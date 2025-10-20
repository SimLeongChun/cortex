using Cortex.Serialization.Yaml.Attributes;
using System.Reflection;

namespace Cortex.Serialization.Yaml.Reflection
{
    internal sealed record PropertyMap(string Name, MemberInfo Member, Type MemberType, bool canRead, bool canWrite, bool Ignored)
    {
        public object? GetValue(object instance) => Member switch { PropertyInfo p when canRead => p.GetValue(instance), FieldInfo f => f.GetValue(instance), _ => null };
        public void SetValue(object instance, object? value)
        {
            switch (Member)
            {
                case PropertyInfo p when canWrite:
                    p.SetValue(instance, value);
                    break;
                case FieldInfo f:
                    f.SetValue(instance, value);
                    break;
            }
        }
        public static IEnumerable<PropertyMap> FromType(Type t)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;
            foreach (var p in t.GetProperties(Flags))
            {
                var ignore = p.GetCustomAttribute<YamlIgnoreAttribute>() != null;
                var attr = p.GetCustomAttribute<YamlPropertyAttribute>();
                var logical = attr?.Name ?? p.Name;

                yield return new PropertyMap(logical, p, p.PropertyType, p.CanRead, p.CanWrite, ignore);
            }

            foreach (var f in t.GetFields(Flags))
            {
                var ignore = f.GetCustomAttribute<YamlIgnoreAttribute>() != null;
                var attr = f.GetCustomAttribute<YamlPropertyAttribute>();
                var logical = attr?.Name ?? f.Name;

                yield return new PropertyMap(logical, f, f.FieldType, canRead: true, canWrite: !f.IsInitOnly, Ignored: ignore);
            }
        }
    }

}
