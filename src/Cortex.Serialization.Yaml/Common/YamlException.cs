namespace Cortex.Serialization.Yaml.Common
{
    /// <summary>
    /// The exception that is thrown when an error occurs during YAML serialization or deserialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception provides detailed information about YAML processing errors, including the specific
    /// location in the YAML document where the error occurred. The <see cref="Line"/> and <see cref="Column"/>
    /// properties indicate the position in the YAML source, making it easier to diagnose and fix issues
    /// in YAML files.
    /// </para>
    /// <para>
    /// Common scenarios that may throw a <see cref="YamlException"/> include:
    /// <list type="bullet">
    /// <item>Invalid YAML syntax or formatting errors</item>
    /// <item>Type conversion failures during deserialization</item>
    /// <item>Missing required properties or invalid property values</item>
    /// <item>Circular references that cannot be serialized</item>
    /// <item>I/O errors when reading from or writing to streams</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example shows how to catch and handle a YamlException:
    /// <code>
    /// try
    /// {
    ///     var obj = YamlSerializer.Deserialize&lt;MyClass&gt;(yamlString);
    /// }
    /// catch (YamlException ex)
    /// {
    ///     Console.WriteLine($"YAML Error at line {ex.Line}, column {ex.Column}:");
    ///     Console.WriteLine(ex.Message);
    ///     
    ///     if (ex.InnerException != null)
    ///     {
    ///         Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    ///     }
    /// }
    /// </code>
    /// Example error message format:
    /// <code>
    /// "Expected scalar value but found sequence (line 5, col 12)"
    /// </code>
    /// </example>
    /// <seealso cref="System.Exception" />
    public class YamlException : Exception
    {
        /// <summary>
        /// Gets the line number in the YAML document where the error occurred.
        /// </summary>
        /// <value>
        /// The one-based line number where the error was detected. Returns 0 if the line number
        /// is not available or applicable to the specific error.
        /// </value>
        public int Line { get; }

        /// <summary>
        /// Gets the column position in the YAML document where the error occurred.
        /// </summary>
        /// <value>
        /// The one-based column number where the error was detected. Returns 0 if the column position
        /// is not available or applicable to the specific error.
        /// </value>
        public int Column { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlException"/> class with a specified error message,
        /// line and column numbers, and an optional inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="line">The line number in the YAML document where the error occurred.</param>
        /// <param name="column">The column position in the YAML document where the error occurred.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or null if no inner exception is specified.</param>
        /// <remarks>
        /// The exception message is automatically formatted to include the line and column information
        /// in the format: "{message} (line {line}, col {column})".
        /// </remarks>
        public YamlException(string message, int line = 0, int column = 0, Exception? inner = null)
            : base($"{message} (line {line}, col {column})", inner)
        {
            Line = line;
            Column = column;
        }
    }
}