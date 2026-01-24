using System;

namespace Cortex.Serialization.Yaml.Attributes
{
    /// <summary>
    /// Indicates that a field or property should be ignored during YAML serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// When applied to a field or property, this attribute instructs the YAML serializer to completely
    /// exclude the member from both serialization (writing to YAML) and deserialization (reading from YAML).
    /// This is useful for:
    /// <list type="bullet">
    /// <item>Excluding computed or derived properties that shouldn't be persisted</item>
    /// <item>Ignoring internal state or temporary fields that are irrelevant to serialization</item>
    /// <item>Preventing sensitive data (like passwords or tokens) from being serialized</item>
    /// <item>Omitting properties that would cause circular references during serialization</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// The following example shows how to use YamlIgnoreAttribute to exclude properties from YAML serialization:
    /// <code>
    /// public class UserSettings
    /// {
    ///     public string UserName { get; set; }
    ///     public string Email { get; set; }
    ///     
    ///     [YamlIgnore]
    ///     public string PasswordHash { get; set; }
    ///     
    ///     [YamlIgnore]
    ///     public DateTime LastAccess { get; set; } // Computed property
    ///     
    ///     // This property will be serialized normally
    ///     public bool IsActive { get; set; }
    /// }
    /// </code>
    /// When serialized to YAML, the output will not include the PasswordHash and LastAccess properties:
    /// <code>
    /// UserName: john_doe
    /// Email: john@example.com
    /// IsActive: true
    /// </code>
    /// </example>
    /// <seealso cref="YamlPropertyAttribute"/>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class YamlIgnoreAttribute : Attribute { }
}