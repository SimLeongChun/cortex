using Cortex.Serialization.Yaml.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cortex.Serialization.Yaml
{
    /// <summary>
    /// Provides methods for deserializing YAML content into .NET objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="YamlDeserializer"/> class converts YAML documents into .NET objects, supporting
    /// a wide range of types including primitives, collections, dictionaries, and custom classes.
    /// It offers both instance-based and static convenience methods for deserialization.
    /// </para>
    /// <para>
    /// Key features include:
    /// <list type="bullet">
    /// <item>Bidirectional naming convention support</item>
    /// <item>Custom type converters for specialized serialization</item>
    /// <item>Flexible property matching (case-sensitive or insensitive)</item>
    /// <item>Configurable handling of unmatched properties</item>
    /// <item>Support for arrays, lists, dictionaries, and complex object graphs</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>
    /// Basic usage with static methods:
    /// </para>
    /// <code>
    /// // Deserialize from string
    /// var person = YamlDeserializer.Deserialize&lt;Person&gt;(yamlString);
    /// 
    /// // Deserialize with custom settings
    /// var settings = new YamlDeserializerSettings 
    /// { 
    ///     CaseInsensitive = false,
    ///     IgnoreUnmatchedProperties = false
    /// };
    /// var config = YamlDeserializer.Deserialize&lt;Config&gt;(yamlString, settings);
    /// 
    /// // Deserialize from file
    /// using var reader = new StreamReader("config.yaml");
    /// var data = YamlDeserializer.Deserialize&lt;DataModel&gt;(reader);
    /// </code>
    /// 
    /// <para>
    /// Advanced usage with instance methods and custom converters:
    /// </para>
    /// <code>
    /// // Create deserializer with custom converters
    /// var converters = new List&lt;IYamlTypeConverter&gt; { new CustomDateConverter() };
    /// var deserializer = new YamlDeserializer(settings, converters);
    /// 
    /// // Reuse the same instance for multiple deserializations
    /// var obj1 = deserializer.Deserialize&lt;Type1&gt;(yaml1);
    /// var obj2 = deserializer.Deserialize&lt;Type2&gt;(yaml2);
    /// </code>
    /// </example>
    /// <seealso cref="YamlSerializer"/>
    /// <seealso cref="YamlDeserializerSettings"/>
    /// <seealso cref="IYamlTypeConverter"/>
    public sealed class YamlDeserializer
    {
        private readonly YamlDeserializerSettings _settings;
        private readonly List<IYamlTypeConverter> _converters = new()
        {
            new PrimitiveConverter()
        };


        // --- Static convenience API ---

        /// <summary>
        /// Deserializes YAML content from a string into an instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="input">The YAML string to deserialize.</param>
        /// <param name="settings">Optional settings to control deserialization behavior.</param>
        /// <param name="extra">Optional additional type converters to use during deserialization.</param>
        /// <returns>An instance of <typeparamref name="T"/> populated with data from the YAML string.</returns>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is a convenience method that creates a temporary <see cref="YamlDeserializer"/> instance
        /// for a single deserialization operation. For multiple deserializations, consider creating
        /// a <see cref="YamlDeserializer"/> instance and reusing it.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// string yaml = @"name: John Smith
        /// age: 30
        /// email: john@example.com";
        /// 
        /// var person = YamlDeserializer.Deserialize&lt;Person&gt;(yaml);
        /// Console.WriteLine(person.Name); // "John Smith"
        /// </code>
        /// </example>
        public static T Deserialize<T>(string input,
            YamlDeserializerSettings? settings = null,
            IEnumerable<IYamlTypeConverter>? extra = null)
            => new YamlDeserializer(settings, extra).Deserialize<T>(input);

        /// <summary>
        /// Deserializes YAML content from a string into an instance of the specified type.
        /// </summary>
        /// <param name="input">The YAML string to deserialize.</param>
        /// <param name="t">The type of object to deserialize.</param>
        /// <param name="settings">Optional settings to control deserialization behavior.</param>
        /// <param name="extra">Optional additional type converters to use during deserialization.</param>
        /// <returns>An instance of the specified type populated with data from the YAML string.</returns>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Use this overload when the target type is not known at compile time.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// string yaml = @"name: John Smith
        /// age: 30";
        /// 
        /// Type targetType = typeof(Person);
        /// var person = YamlDeserializer.Deserialize(yaml, targetType);
        /// </code>
        /// </example>
        public static object? Deserialize(string input, Type t,
            YamlDeserializerSettings? settings = null,
            IEnumerable<IYamlTypeConverter>? extra = null)
            => new YamlDeserializer(settings, extra).Deserialize(input, t);

        /// <summary>
        /// Deserializes YAML content from a <see cref="TextReader"/> into an instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="reader">The <see cref="TextReader"/> containing YAML content.</param>
        /// <param name="settings">Optional settings to control deserialization behavior.</param>
        /// <param name="extra">Optional additional type converters to use during deserialization.</param>
        /// <returns>An instance of <typeparamref name="T"/> populated with data from the YAML content.</returns>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is suitable for reading YAML from files, network streams, or any other
        /// source that can be wrapped in a <see cref="TextReader"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using var reader = new StreamReader("data.yaml");
        /// var data = YamlDeserializer.Deserialize&lt;DataModel&gt;(reader);
        /// </code>
        /// </example>
        public static T Deserialize<T>(TextReader reader,
            YamlDeserializerSettings? settings = null,
            IEnumerable<IYamlTypeConverter>? extra = null)
            => new YamlDeserializer(settings, extra).Deserialize<T>(reader);

        /// <summary>
        /// Deserializes YAML content from a <see cref="TextReader"/> into an instance of the specified type.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> containing YAML content.</param>
        /// <param name="t">The type of object to deserialize.</param>
        /// <param name="settings">Optional settings to control deserialization behavior.</param>
        /// <param name="extra">Optional additional type converters to use during deserialization.</param>
        /// <returns>An instance of the specified type populated with data from the YAML content.</returns>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <example>
        /// <code>
        /// using var reader = new StringReader(yamlString);
        /// Type targetType = typeof(Person);
        /// var person = YamlDeserializer.Deserialize(reader, targetType);
        /// </code>
        /// </example>
        public static object? Deserialize(TextReader reader, Type t,
            YamlDeserializerSettings? settings = null,
            IEnumerable<IYamlTypeConverter>? extra = null)
            => new YamlDeserializer(settings, extra).Deserialize(reader, t);
        // ------------------------------------

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlDeserializer"/> class with the specified settings and converters.
        /// </summary>
        /// <param name="s">The settings to use for deserialization. If null, default settings are used.</param>
        /// <param name="extra">Additional type converters to use during deserialization.</param>
        /// <remarks>
        /// <para>
        /// Creating an instance of <see cref="YamlDeserializer"/> is recommended when you need to perform
        /// multiple deserialization operations with the same configuration, as it avoids the overhead
        /// of recreating internal structures for each operation.
        /// </para>
        /// <para>
        /// The deserializer is initialized with a <see cref="PrimitiveConverter"/> by default, which handles
        /// basic types like strings, numbers, and booleans. Additional converters can be provided to handle
        /// custom types or override default conversion behavior.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // With default settings
        /// var deserializer1 = new YamlDeserializer();
        /// 
        /// // With custom settings
        /// var settings = new YamlDeserializerSettings { CaseInsensitive = false };
        /// var deserializer2 = new YamlDeserializer(settings);
        /// 
        /// // With custom settings and converters
        /// var converters = new List&lt;IYamlTypeConverter&gt; { new CustomConverter() };
        /// var deserializer3 = new YamlDeserializer(settings, converters);
        /// </code>
        /// </example>
        public YamlDeserializer(YamlDeserializerSettings? s = null, IEnumerable<IYamlTypeConverter>? extra = null)
        {
            _settings = s ?? new();
            if (extra is not null)
                _converters.AddRange(extra);
        }

        /// <summary>
        /// Deserializes YAML content from a <see cref="TextReader"/> into an instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="reader">The <see cref="TextReader"/> containing YAML content.</param>
        /// <returns>An instance of <typeparamref name="T"/> populated with data from the YAML content.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method reads the entire content from the <see cref="TextReader"/> and parses it as YAML.
        /// The reader is not closed or disposed by this method - the caller remains responsible for resource management.
        /// </para>
        /// </remarks>
        public T Deserialize<T>(TextReader reader)
        {
            var doc = Parse(reader.ReadToEnd());
            return (T)ConvertNode(doc, typeof(T))!;
        }

        /// <summary>
        /// Deserializes YAML content from a <see cref="TextReader"/> into an instance of the specified type.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> containing YAML content.</param>
        /// <param name="t">The type of object to deserialize.</param>
        /// <returns>An instance of the specified type populated with data from the YAML content.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="reader"/> or <paramref name="t"/> is null.
        /// </exception>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        public object? Deserialize(TextReader reader, Type t)
        {
            var doc = Parse(reader.ReadToEnd());
            return ConvertNode(doc, t);
        }

        /// <summary>
        /// Deserializes a YAML string into an instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="input">The YAML string to deserialize.</param>
        /// <returns>An instance of <typeparamref name="T"/> populated with data from the YAML string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is the primary method for deserializing YAML strings. It parses the YAML content
        /// and converts it to the specified .NET type using the configured settings and converters.
        /// </para>
        /// </remarks>
        public T Deserialize<T>(string input)
        {
            var doc = Parse(input);
            return (T)ConvertNode(doc, typeof(T))!;
        }

        /// <summary>
        /// Deserializes a YAML string into an instance of the specified type.
        /// </summary>
        /// <param name="input">The YAML string to deserialize.</param>
        /// <param name="t">The type of object to deserialize.</param>
        /// <returns>An instance of the specified type populated with data from the YAML string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="input"/> or <paramref name="t"/> is null.
        /// </exception>
        /// <exception cref="YamlException">
        /// Thrown when the YAML is invalid, cannot be parsed, or cannot be converted to the target type.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Use this overload when the target type is determined at runtime rather than compile time.
        /// </para>
        /// </remarks>
        public object? Deserialize(string input, Type t)
        {
            var doc = Parse(input);
            return ConvertNode(doc, t);
        }

        private Parser.YamlNode Parse(string input)
        {
            var scanner = new Parser.Scanner(input);
            var tokens = scanner.Scan();
            var parser = new Parser.Parser(tokens, _settings.PreserveComments);
            return parser.ParseDocument();
        }

        private object? ConvertNode(Parser.YamlNode node, Type target)
        {
            // Handle alias nodes
            if (node is Parser.YamlAlias alias)
            {
                // Alias should have been resolved during parsing if ResolveAnchors is true
                throw new Common.YamlException($"Unresolved alias: *{alias.Name}");
            }

            foreach (var c in _converters)
                if (c.CanConvert(target))
                    return c.Read((node as Parser.YamlScalar)?.Value, target);

            if (node is Parser.YamlScalar s)
            {
                if (target == typeof(object))
                    return s.Value;
                foreach (var c in _converters)
                    if (c.CanConvert(target))
                        return c.Read(s.Value, target);
                return s.Value;
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(target))
            {
                var (kt, vt) = GetDictTypes(target);
                var dict = (System.Collections.IDictionary)Activator.CreateInstance(target)!;
                if (node is not Parser.YamlMapping map)
                    throw new Common.YamlException("Expected mapping for dictionary");
                foreach (var (k, v) in map.Entries)
                {
                    var keyObj = Convert.ChangeType(k, kt);
                    var valObj = ConvertNode(v, vt);
                    dict.Add(keyObj!, valObj);
                }
                return dict;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(target) && target != typeof(string))
            {
                var et = target.IsArray ? target.GetElementType()! : target.GenericTypeArguments.FirstOrDefault() ?? typeof(object);
                var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(et))!;
                if (node is not Parser.YamlSequence seq)
                    throw new Common.YamlException("Expected sequence for collection");

                foreach (var it in seq.Items)
                    list.Add(ConvertNode(it, et));
                if (target.IsArray)
                {
                    var arr = Array.CreateInstance(et, list.Count);
                    list.CopyTo(arr, 0); return arr;
                }
                if (target.IsAssignableFrom(list.GetType()))
                    return list;
                var coll = Activator.CreateInstance(target);
                var add = target.GetMethod("Add");
                foreach (var it in list)
                    add!.Invoke(coll, new[] { it });
                return coll;
            }

            if (node is Parser.YamlMapping mapping)
            {
                var obj = Activator.CreateInstance(target)!;
                var lookup = BuildMemberLookup(target);
                foreach (var (key, val) in mapping.Entries)
                {
                    if (!lookup.TryGetValue(key, out var pm))
                    {
                        if (!_settings.IgnoreUnmatchedProperties)
                            throw new Common.YamlException($"Unknown property '{key}' for type {target.Name}");
                        continue;
                    }
                    var converted = ConvertNode(val, pm.MemberType);
                    pm.SetValue(obj, converted);
                }
                return obj;
            }
            throw new Common.YamlException($"Cannot convert node {node.GetType().Name} to {target.Name}");
        }
        private Dictionary<string, Reflection.PropertyMap> BuildMemberLookup(Type t)
        {
            var insp = new Reflection.TypeInspector(_settings.NamingConvention);
            var dict = new Dictionary<string, Reflection.PropertyMap>(_settings.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var m in insp.GetDeserializableMembers(t))
                dict[_settings.NamingConvention.Convert(m.Name)] = m;
            return dict;
        }
        private static (Type key, Type val) GetDictTypes(Type dict)
        {
            if (dict.IsGenericType)
            {
                var a = dict.GetGenericArguments();
                return (a[0], a[1]);
            }
            return (typeof(string), typeof(object));
        }
    }
}