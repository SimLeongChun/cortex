using Cortex.Serialization.Yaml.Converters;
using System.Collections.Generic;

namespace Cortex.Serialization.Yaml
{
    /// <summary>
    /// Provides configuration options for customizing the behavior of YAML serialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class allows you to control how .NET objects are converted to YAML format.
    /// You can customize naming conventions, formatting, null handling, and type conversion
    /// to produce YAML output that matches your specific requirements and style preferences.
    /// </para>
    /// <para>
    /// The default settings are optimized for readability and compatibility:
    /// <list type="bullet">
    /// <item><see cref="NamingConvention"/>: camelCase (common in YAML/JSON ecosystems)</item>
    /// <item><see cref="EmitNulls"/>: true (include null values in output)</item>
    /// <item><see cref="EmitDefaults"/>: true (include default values in output)</item>
    /// <item><see cref="SortProperties"/>: false (maintain property declaration order)</item>
    /// <item><see cref="Indentation"/>: 2 (standard YAML indentation)</item>
    /// <item><see cref="Converters"/>: Includes <see cref="PrimitiveConverter"/> for basic types</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example shows how to configure custom serializer settings:
    /// <code>
    /// var settings = new YamlSerializerSettings
    /// {
    ///     NamingConvention = new PascalCaseNamingConvention(),
    ///     EmitNulls = false, // Skip null values
    ///     SortProperties = true, // Alphabetical order
    ///     Indentation = 4 // Use 4 spaces for indentation
    /// };
    /// 
    /// string yaml = YamlSerializer.Serialize(obj, settings);
    /// </code>
    /// 
    /// Example for compact YAML output:
    /// <code>
    /// var compactSettings = new YamlSerializerSettings
    /// {
    ///     EmitNulls = false,
    ///     EmitDefaults = false,
    ///     Indentation = 2
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="YamlDeserializerSettings"/>
    /// <seealso cref="IYamlTypeConverter"/>
    public sealed class YamlSerializerSettings
    {
        /// <summary>
        /// Gets or sets the naming convention used to convert .NET property names to YAML property names.
        /// </summary>
        /// <value>
        /// An implementation of <see cref="INamingConvention"/> that defines how property names are transformed.
        /// Default is <see cref="CamelCaseConvention"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// This convention is applied during serialization to convert .NET property names (typically PascalCase)
        /// to the desired naming convention in the output YAML.
        /// </para>
        /// <para>
        /// Set to <c>null</c> to use exact .NET property names without transformation.
        /// </para>
        /// </remarks>
        /// <example>
        /// With camelCase convention:
        /// - C#: "FirstName" → YAML: "firstName"
        /// - C#: "EmailAddress" → YAML: "emailAddress"
        /// </example>
        public INamingConvention NamingConvention { get; init; } = new CamelCaseConvention();

        /// <summary>
        /// Gets or sets a value indicating whether null values should be included in the YAML output.
        /// </summary>
        /// <value>
        /// <c>true</c> to emit null values as explicit nulls in the YAML; <c>false</c> to omit null properties entirely.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>true</c>, null properties are written as <c>property: null</c> in the YAML output.
        /// When set to <c>false</c>, properties with null values are completely omitted from the output.
        /// </para>
        /// <para>
        /// Setting this to <c>false</c> can produce more compact YAML but may lose information about
        /// which properties were explicitly set to null versus omitted.
        /// </para>
        /// </remarks>
        /// <example>
        /// Given an object: <c>new { Name = "John", Address = null }</c>
        /// 
        /// With EmitNulls = true:
        /// <code>
        /// name: John
        /// address: null
        /// </code>
        /// 
        /// With EmitNulls = false:
        /// <code>
        /// name: John
        /// </code>
        /// </example>
        public bool EmitNulls { get; init; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether default values should be included in the YAML output.
        /// </summary>
        /// <value>
        /// <c>true</c> to emit default values (0, false, empty strings, etc.) in the YAML;
        /// <c>false</c> to omit properties with default values.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>false</c>, properties with default values (0 for numbers, false for booleans,
        /// default for structs, empty collections, etc.) are omitted from the YAML output.
        /// </para>
        /// <para>
        /// This setting works in conjunction with <see cref="EmitNulls"/> - null values are handled
        /// by that setting regardless of this one.
        /// </para>
        /// </remarks>
        /// <example>
        /// Given an object: <c>new { Count = 0, Enabled = false, Name = "" }</c>
        /// 
        /// With EmitDefaults = true:
        /// <code>
        /// count: 0
        /// enabled: false
        /// name: ""
        /// </code>
        /// 
        /// With EmitDefaults = false:
        /// <code>
        /// # All properties omitted since they have default values
        /// </code>
        /// </example>
        public bool EmitDefaults { get; init; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether properties should be sorted alphabetically in the YAML output.
        /// </summary>
        /// <value>
        /// <c>true</c> to sort properties alphabetically; <c>false</c> to maintain the order in which
        /// properties are declared in the class. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>false</c> (default), properties are emitted in the order they are declared
        /// in the class, which can provide more logical grouping of related properties.
        /// </para>
        /// <para>
        /// When set to <c>true</c>, properties are sorted alphabetically by their YAML names (after
        /// applying the naming convention), which can provide consistent ordering across different
        /// .NET runtime implementations.
        /// </para>
        /// </remarks>
        /// <example>
        /// Given a class:
        /// <code>
        /// public class Example 
        /// {
        ///     public string LastName { get; set; }
        ///     public string FirstName { get; set; }
        /// }
        /// </code>
        /// 
        /// With SortProperties = false (declaration order):
        /// <code>
        /// lastName: Smith
        /// firstName: John
        /// </code>
        /// 
        /// With SortProperties = true (alphabetical order):
        /// <code>
        /// firstName: John
        /// lastName: Smith
        /// </code>
        /// </example>
        public bool SortProperties { get; init; } = false;

        /// <summary>
        /// Gets or sets the number of spaces used for each level of indentation in the YAML output.
        /// </summary>
        /// <value>
        /// The number of spaces for each indentation level. Default is 2.
        /// </value>
        /// <remarks>
        /// <para>
        /// Common values are 2 (standard YAML convention) or 4 (common in some codebases).
        /// The value must be positive.
        /// </para>
        /// <para>
        /// This setting affects the readability and compactness of the generated YAML.
        /// Smaller values produce more compact output, while larger values can improve
        /// readability for deeply nested structures.
        /// </para>
        /// </remarks>
        /// <example>
        /// With Indentation = 2:
        /// <code>
        /// parent:
        ///   child:
        ///     value: example
        /// </code>
        /// 
        /// With Indentation = 4:
        /// <code>
        /// parent:
        ///     child:
        ///         value: example
        /// </code>
        /// </example>
        public int Indentation { get; init; } = 2;

        /// <summary>
        /// Gets or sets a value indicating whether to prefer flow style for collections.
        /// </summary>
        /// <value>
        /// <c>true</c> to prefer flow style (inline) for simple collections; 
        /// <c>false</c> to always use block style. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When enabled, simple collections (sequences and mappings with only scalar values)
        /// will be serialized using flow style (JSON-like syntax) when they fit within 
        /// the <see cref="FlowStyleThreshold"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// With PreferFlowStyle = true:
        /// <code>
        /// tags: [tag1, tag2, tag3]
        /// metadata: {key1: value1, key2: value2}
        /// </code>
        /// 
        /// With PreferFlowStyle = false:
        /// <code>
        /// tags:
        ///   - tag1
        ///   - tag2
        ///   - tag3
        /// metadata:
        ///   key1: value1
        ///   key2: value2
        /// </code>
        /// </example>
        public bool PreferFlowStyle { get; init; } = false;

        /// <summary>
        /// Gets or sets the maximum line length threshold for using flow style collections.
        /// </summary>
        /// <value>
        /// The maximum estimated line length for flow style output. Default is 80 characters.
        /// </value>
        /// <remarks>
        /// <para>
        /// When <see cref="PreferFlowStyle"/> is true, collections will only use flow style
        /// if their estimated output length is less than this threshold.
        /// </para>
        /// </remarks>
        public int FlowStyleThreshold { get; init; } = 80;

        /// <summary>
        /// Gets or sets a value indicating whether to emit comments in the output.
        /// </summary>
        /// <value>
        /// <c>true</c> to emit comments that were associated with nodes; <c>false</c> to omit them.
        /// Default is <c>true</c>.
        /// </value>
        public bool EmitComments { get; init; } = true;

        /// <summary>
        /// Gets the list of custom type converters used during serialization.
        /// </summary>
        /// <value>
        /// A list of <see cref="IYamlTypeConverter"/> instances that handle serialization of specific types.
        /// Default includes <see cref="PrimitiveConverter"/> for basic types.
        /// </value>
        /// <remarks>
        /// <para>
        /// Custom converters allow you to control how specific .NET types are serialized to YAML.
        /// You can add converters for custom types or override the default serialization behavior
        /// for built-in types.
        /// </para>
        /// <para>
        /// Converters are evaluated in order, and the first converter that can handle a type is used.
        /// </para>
        /// </remarks>
        /// <example>
        /// Adding a custom converter for a DateTime format:
        /// <code>
        /// var settings = new YamlSerializerSettings();
        /// settings.Converters.Add(new CustomDateTimeConverter("yyyy-MM-dd"));
        /// 
        /// string yaml = YamlSerializer.Serialize(obj, settings);
        /// </code>
        /// 
        /// Adding a converter for a custom type:
        /// <code>
        /// public class ColorConverter : IYamlTypeConverter
        /// {
        ///     public bool CanConvert(Type type) => type == typeof(Color);
        ///     
        ///     public void WriteYaml(YamlWriter writer, object value)
        ///     {
        ///         var color = (Color)value;
        ///         writer.WriteValue($"#{color.R:X2}{color.G:X2}{color.B:X2}");
        ///     }
        /// }
        /// </code>
        /// </example>
        public List<IYamlTypeConverter> Converters { get; } = new() { new PrimitiveConverter() };
    }
}