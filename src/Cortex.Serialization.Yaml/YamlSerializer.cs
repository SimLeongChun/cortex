using Cortex.Serialization.Yaml.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Serialization.Yaml
{
    /// <summary>
    /// Provides methods for serializing .NET objects into YAML format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="YamlSerializer"/> class converts .NET objects into YAML documents, supporting
    /// a wide range of types including primitives, collections, dictionaries, and custom classes.
    /// It offers both instance-based and static convenience methods for serialization.
    /// </para>
    /// <para>
    /// Key features include:
    /// <list type="bullet">
    /// <item>Configurable naming conventions for property names</item>
    /// <item>Control over null and default value emission</item>
    /// <item>Property ordering (declaration order or alphabetical)</item>
    /// <item>Customizable indentation</item>
    /// <item>Automatic handling of collections and dictionaries</item>
    /// </list>
    /// </para>
    /// <para>
    /// The serializer automatically handles common .NET types including:
    /// <list type="bullet">
    /// <item>Primitives: string, bool, int, long, double, decimal, Guid, DateTime, DateOnly, TimeOnly</item>
    /// <item>Collections: arrays, lists, and any type implementing <see cref="System.Collections.IEnumerable"/></item>
    /// <item>Dictionaries: any type implementing <see cref="System.Collections.IDictionary"/></item>
    /// <item>Custom objects: public properties are serialized as YAML mappings</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>
    /// Basic usage with static method:
    /// </para>
    /// <code>
    /// var person = new Person { Name = "John", Age = 30 };
    /// string yaml = YamlSerializer.Serialize(person);
    /// 
    /// // Output:
    /// // name: John
    /// // age: 30
    /// </code>
    /// 
    /// <para>
    /// Serializing collections and complex objects:
    /// </para>
    /// <code>
    /// var users = new List&lt;Person&gt; 
    /// {
    ///     new Person { Name = "John", Age = 30 },
    ///     new Person { Name = "Jane", Age = 25 }
    /// };
    /// 
    /// string yaml = YamlSerializer.Serialize(users);
    /// 
    /// // Output:
    /// // - name: John
    /// //   age: 30
    /// // - name: Jane
    /// //   age: 25
    /// </code>
    /// 
    /// <para>
    /// Using instance methods for multiple serializations:
    /// </para>
    /// <code>
    /// var settings = new YamlSerializerSettings 
    /// { 
    ///     EmitNulls = false,
    ///     SortProperties = true 
    /// };
    /// 
    /// var serializer = new YamlSerializer(settings);
    /// string yaml1 = serializer.Serialize(obj1);
    /// string yaml2 = serializer.Serialize(obj2);
    /// </code>
    /// </example>
    /// <seealso cref="YamlDeserializer"/>
    /// <seealso cref="YamlSerializerSettings"/>
    public sealed class YamlSerializer
    {
        private readonly YamlSerializerSettings _settings;

        // --- Static convenience API ---

        /// <summary>
        /// Serializes the specified object to a YAML string using the provided settings.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="settings">The settings to use for serialization, or null to use default settings.</param>
        /// <returns>A YAML string representing the serialized object.</returns>
        /// <remarks>
        /// <para>
        /// This is a convenience method that creates a temporary <see cref="YamlSerializer"/> instance
        /// for a single serialization operation. For multiple serializations, consider creating
        /// a <see cref="YamlSerializer"/> instance and reusing it.
        /// </para>
        /// <para>
        /// If <paramref name="obj"/> is null, the method returns a YAML null value.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var person = new Person { Name = "John", Age = 30 };
        /// 
        /// // With default settings
        /// string yaml1 = YamlSerializer.Serialize(person);
        /// 
        /// // With custom settings
        /// var settings = new YamlSerializerSettings { EmitNulls = false };
        /// string yaml2 = YamlSerializer.Serialize(person, settings);
        /// </code>
        /// </example>
        public static string Serialize(object? obj, YamlSerializerSettings? settings = null)
            => new YamlSerializer(settings).Serialize(obj);

        // --- Instance API ---

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlSerializer"/> class with the specified settings.
        /// </summary>
        /// <param name="s">The settings to use for serialization. If null, default settings are used.</param>
        /// <remarks>
        /// <para>
        /// Creating an instance of <see cref="YamlSerializer"/> is recommended when you need to perform
        /// multiple serialization operations with the same configuration, as it avoids the overhead
        /// of recreating internal structures for each operation.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // With default settings
        /// var serializer1 = new YamlSerializer();
        /// 
        /// // With custom settings
        /// var settings = new YamlSerializerSettings 
        /// { 
        ///     EmitNulls = false,
        ///     SortProperties = true 
        /// };
        /// var serializer2 = new YamlSerializer(settings);
        /// </code>
        /// </example>
        public YamlSerializer(YamlSerializerSettings? s = null) => _settings = s ?? new();

        /// <summary>
        /// Serializes the specified object to a YAML string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A YAML string representing the serialized object.</returns>
        /// <remarks>
        /// <para>
        /// The serialization process follows these steps:
        /// <list type="number">
        /// <item>Convert the object to a YAML node tree using <see cref="ToNode"/></item>
        /// <item>Emit the YAML node tree to a string using the configured indentation</item>
        /// </list>
        /// </para>
        /// <para>
        /// The method handles null values by returning a YAML null scalar. For objects,
        /// only public readable properties are serialized (read-only properties are excluded).
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var serializer = new YamlSerializer();
        /// 
        /// // Serialize a simple object
        /// var person = new Person { Name = "John", Age = 30 };
        /// string yaml = serializer.Serialize(person);
        /// 
        /// // Serialize a collection
        /// var numbers = new List&lt;int&gt; { 1, 2, 3 };
        /// string yamlNumbers = serializer.Serialize(numbers);
        /// 
        /// // Serialize a dictionary
        /// var dict = new Dictionary&lt;string, object&gt; 
        /// {
        ///     ["key1"] = "value1",
        ///     ["key2"] = 42
        /// };
        /// string yamlDict = serializer.Serialize(dict);
        /// </code>
        /// </example>
        public string Serialize(object? obj)
        {
            var node = ToNode(obj);
            var emitter = new Emitter.Emitter(_settings.Indentation);

            return emitter.Emit(node);
        }

        private Parser.YamlNode ToNode(object? obj)
        {
            if (obj is null)
                return new Parser.YamlScalar(null);

            // Handle primitive types as scalars
            if (obj is string or bool or int or long or double or decimal or Guid or DateTime or DateOnly or TimeOnly)
                return new Parser.YamlScalar(obj);

            // Handle dictionaries as mappings
            if (obj is System.Collections.IDictionary dict)
            {
                var m = new Dictionary<string, Parser.YamlNode>();
                foreach (System.Collections.DictionaryEntry de in dict)
                    m[de.Key!.ToString()!] = ToNode(de.Value);
                return new Parser.YamlMapping(m);
            }

            // Handle collections as sequences (but not strings, which are handled above)
            if (obj is System.Collections.IEnumerable en && obj is not string)
            {
                var items = new List<Parser.YamlNode>();
                foreach (var it in en) items.Add(ToNode(it));
                return new Parser.YamlSequence(items);
            }

            // Handle complex objects as mappings with properties
            var insp = new TypeInspector(_settings.NamingConvention);
            var map = new Dictionary<string, Parser.YamlNode>();
            var props = insp.GetSerializableMembers(obj.GetType(), includeReadOnly: false);
            var seq = _settings.SortProperties ? props.OrderBy(p => p.Name) : props;

            foreach (var p in seq)
            {
                var val = p.GetValue(obj);
                if (val is null && !_settings.EmitNulls)
                    continue;
                if (val is not null && IsDefault(val) && !_settings.EmitDefaults)
                    continue;
                map[p.Name] = ToNode(val);
            }

            return new Parser.YamlMapping(map);
        }

        private static bool IsDefault(object value)
        {
            var t = value.GetType();
            return Equals(value, t.IsValueType ? Activator.CreateInstance(t) : null);
        }
    }
}