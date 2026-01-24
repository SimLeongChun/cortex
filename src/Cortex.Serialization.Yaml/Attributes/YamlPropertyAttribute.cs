using System;

namespace Cortex.Serialization.Yaml.Attributes
{
    /// <summary>
    /// Specifies a custom YAML property name for a field or property during serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// When applied to a field or property, this attribute allows you to specify an alternative name 
    /// that should be used in the YAML representation, different from the actual member name in code.
    /// This is particularly useful for:
    /// <list type="bullet">
    /// <item>Mapping between different naming conventions (e.g., camelCase in YAML and PascalCase in C#)</item>
    /// <item>Using YAML property names that are not valid C# identifiers</item>
    /// <item>Maintaining compatibility with existing YAML schemas</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// The following example shows how to use YamlPropertyAttribute to specify custom YAML property names:
    /// <code>
    /// public class SerializableObject
    /// {
    ///     [YamlProperty(Name = "output-format")]
    ///     public string OutputFormat { get; set; }
    ///     
    ///     [YamlProperty(Name = "maxItems")]
    ///     public int MaximumItems { get; set; }
    /// }
    /// </code>
    /// This will serialize to YAML as:
    /// <code>
    /// output-format: SomeValue
    /// maxItems: 42
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class YamlPropertyAttribute : Attribute
    {
        /// <summary>
        /// Gets the custom name to use for the YAML property.
        /// If null or empty, the default name (the member name) will be used.
        /// </summary>
        /// <value>
        /// A string representing the custom property name to use in YAML serialization,
        /// or null to use the default behavior.
        /// </value>
        public string? Name { get; init; }
    }
}