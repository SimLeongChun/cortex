using Cortex.Serialization.Yaml.Converters;

namespace Cortex.Serialization.Yaml
{
    /// <summary>
    /// Provides configuration options for customizing the behavior of YAML deserialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class allows you to control how YAML content is parsed and converted into .NET objects.
    /// You can customize naming conventions, case sensitivity, and how unknown properties are handled
    /// to match your specific requirements and data format.
    /// </para>
    /// <para>
    /// The default settings are optimized for flexibility and compatibility with common YAML conventions:
    /// <list type="bullet">
    /// <item><see cref="NamingConvention"/>: camelCase (common in YAML/JSON ecosystems)</item>
    /// <item><see cref="CaseInsensitive"/>: true (for robust property matching)</item>
    /// <item><see cref="IgnoreUnmatchedProperties"/>: true (for forward/backward compatibility)</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example shows how to configure custom deserializer settings:
    /// <code>
    /// var settings = new YamlDeserializerSettings
    /// {
    ///     NamingConvention = new SnakeCaseNamingConvention(),
    ///     CaseInsensitive = false, // Require exact case matching
    ///     IgnoreUnmatchedProperties = false // Throw on unknown properties
    /// };
    /// 
    /// var obj = YamlDeserializer.Deserialize&lt;MyClass&gt;(yamlString, settings);
    /// </code>
    /// 
    /// Example with custom naming convention:
    /// <code>
    /// public class KebabCaseNamingConvention : INamingConvention
    /// {
    ///     public string Convert(string name)
    ///     {
    ///         // Convert "FirstName" to "first-name"
    ///         return Regex.Replace(name, @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    ///     }
    /// }
    /// 
    /// var settings = new YamlDeserializerSettings
    /// {
    ///     NamingConvention = new KebabCaseNamingConvention()
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="INamingConvention"/>
    public sealed class YamlDeserializerSettings
    {
        /// <summary>
        /// Gets or sets the naming convention used to map between YAML property names and .NET member names.
        /// </summary>
        /// <value>
        /// An implementation of <see cref="INamingConvention"/> that defines how property names are transformed.
        /// Default is <see cref="CamelCaseConvention"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// This convention is applied bidirectionally during deserialization to match YAML property names
        /// with the corresponding .NET class members. For example, with camelCase convention, a YAML property
        /// named "firstName" would map to a C# property named "FirstName".
        /// </para>
        /// <para>
        /// Set to <c>null</c> to use exact name matching without any transformation.
        /// </para>
        /// </remarks>
        /// <example>
        /// With camelCase convention:
        /// - YAML: "userName" → C#: "UserName"
        /// - YAML: "emailAddress" → C#: "EmailAddress"
        /// </example>
        public INamingConvention NamingConvention { get; init; } = new CamelCaseConvention();

        /// <summary>
        /// Gets or sets a value indicating whether property name matching should be case-insensitive.
        /// </summary>
        /// <value>
        /// <c>true</c> to ignore case when matching YAML properties to .NET members; <c>false</c> to require exact case matching.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>true</c>, the deserializer will match properties regardless of case differences.
        /// This provides flexibility when working with YAML files that may have inconsistent casing.
        /// </para>
        /// <para>
        /// When set to <c>false</c>, property names must match exactly, including case.
        /// </para>
        /// </remarks>
        /// <example>
        /// With CaseInsensitive = true:
        /// - YAML: "username", "UserName", "USERNAME" all map to C# property "UserName"
        /// 
        /// With CaseInsensitive = false:
        /// - Only "UserName" in YAML maps to C# property "UserName"
        /// - "username" and "USERNAME" would be treated as unmatched properties
        /// </example>
        public bool CaseInsensitive { get; init; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether unmatched YAML properties should be ignored during deserialization.
        /// </summary>
        /// <value>
        /// <c>true</c> to silently ignore properties in the YAML that don't have matching members in the target type;
        /// <c>false</c> to throw a <see cref="YamlException"/> when unmatched properties are encountered.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// Setting this to <c>true</c> provides better forward and backward compatibility, allowing your application
        /// to work with YAML files that contain additional properties not defined in your .NET types.
        /// </para>
        /// <para>
        /// Setting this to <c>false</c> is useful for strict validation, ensuring that the YAML structure exactly
        /// matches the expected schema and helping to catch typos or structural errors.
        /// </para>
        /// </remarks>
        /// <example>
        /// Given a class:
        /// <code>
        /// public class User { public string Name { get; set; } }
        /// </code>
        /// And YAML:
        /// <code>
        /// name: John
        /// age: 30
        /// </code>
        /// With IgnoreUnmatchedProperties = true: Deserialization succeeds, "age" is ignored
        /// With IgnoreUnmatchedProperties = false: YamlException is thrown for unmatched property "age"
        /// </example>
        public bool IgnoreUnmatchedProperties { get; init; } = true;
    }
}