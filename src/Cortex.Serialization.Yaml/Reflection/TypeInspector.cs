using Cortex.Serialization.Yaml.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Serialization.Yaml.Reflection
{
    internal sealed class TypeInspector
    {
        private readonly INamingConvention _conv;
        public TypeInspector(INamingConvention c) => _conv = c;
        public IEnumerable<PropertyMap> GetSerializableMembers(Type t, bool includeReadOnly)
        {
            foreach (var m in CachedTypeInfo.GetProps(t))
            {
                if (m.Ignored) continue;
                if (!includeReadOnly && !m.canWrite) continue;
                yield return m with { Name = _conv.Convert(m.Name) };
            }
        }

        public IEnumerable<PropertyMap> GetDeserializableMembers(Type t) => CachedTypeInfo
            .GetProps(t)
            .Where(m => !m.Ignored && m.canWrite);
    }
}
