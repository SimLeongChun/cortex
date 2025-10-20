namespace Cortex.Serialization.Yaml.Reflection
{
    internal sealed class CachedTypeInfo
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyMap[]> Cache = new();
        public static PropertyMap[] GetProps(Type t) => Cache.GetOrAdd(t, x => PropertyMap.FromType(x).ToArray());
    }
}
