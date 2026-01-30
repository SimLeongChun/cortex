using Cortex.Serialization.Yaml;
using Cortex.Serialization.Yaml.Converters;

namespace Cortex.Tests.Serialization.Tests
{
    public class YamlNamingConventionTests
    {
        #region Test Models

        public class PersonModel
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? EmailAddress { get; set; }
            public string? PhoneNumber { get; set; }
        }

        #endregion

        #region CamelCase Convention Tests

        [Fact]
        public void CamelCaseConvention_Convert_ConvertsCorrectly()
        {
            // Arrange
            var convention = new CamelCaseConvention();

            // Act & Assert
            Assert.Equal("firstName", convention.Convert("FirstName"));
            Assert.Equal("lastName", convention.Convert("LastName"));
            Assert.Equal("emailAddress", convention.Convert("EmailAddress"));
        }

        [Fact]
        public void Serialize_WithCamelCaseConvention_ProducesCorrectYaml()
        {
            // Arrange
            var person = new PersonModel
            {
                FirstName = "John",
                LastName = "Doe",
                EmailAddress = "john@example.com"
            };

            var settings = new YamlSerializerSettings
            {
                NamingConvention = new CamelCaseConvention()
            };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("firstName: John", yaml);
            Assert.Contains("lastName: Doe", yaml);
            Assert.Contains("emailAddress: john@example.com", yaml);
        }

        [Fact]
        public void Deserialize_WithCamelCaseConvention_ParsesCorrectly()
        {
            // Arrange
            var yaml = @"
firstName: John
lastName: Doe
emailAddress: john@example.com";

            var settings = new YamlDeserializerSettings
            {
                NamingConvention = new CamelCaseConvention()
            };

            // Act
            var person = YamlDeserializer.Deserialize<PersonModel>(yaml, settings);

            // Assert
            Assert.Equal("John", person.FirstName);
            Assert.Equal("Doe", person.LastName);
            Assert.Equal("john@example.com", person.EmailAddress);
        }

        #endregion

        #region SnakeCase Convention Tests

        [Fact]
        public void SnakeCaseConvention_Convert_ConvertsCorrectly()
        {
            // Arrange
            var convention = new SnakeCaseConvention();

            // Act & Assert
            Assert.Equal("first_name", convention.Convert("FirstName"));
            Assert.Equal("last_name", convention.Convert("LastName"));
            Assert.Equal("email_address", convention.Convert("EmailAddress"));
        }

        [Fact]
        public void Serialize_WithSnakeCaseConvention_ProducesCorrectYaml()
        {
            // Arrange
            var person = new PersonModel
            {
                FirstName = "John",
                LastName = "Doe",
                EmailAddress = "john@example.com"
            };

            var settings = new YamlSerializerSettings
            {
                NamingConvention = new SnakeCaseConvention()
            };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("first_name: John", yaml);
            Assert.Contains("last_name: Doe", yaml);
            Assert.Contains("email_address: john@example.com", yaml);
        }

        [Fact]
        public void Deserialize_WithSnakeCaseConvention_ParsesCorrectly()
        {
            // Arrange
            var yaml = @"
first_name: John
last_name: Doe
email_address: john@example.com";

            var settings = new YamlDeserializerSettings
            {
                NamingConvention = new SnakeCaseConvention()
            };

            // Act
            var person = YamlDeserializer.Deserialize<PersonModel>(yaml, settings);

            // Assert
            Assert.Equal("John", person.FirstName);
            Assert.Equal("Doe", person.LastName);
            Assert.Equal("john@example.com", person.EmailAddress);
        }

        #endregion

        #region KebabCase Convention Tests

        [Fact]
        public void KebabCaseConvention_Convert_ConvertsCorrectly()
        {
            // Arrange
            var convention = new KebabCaseConvention();

            // Act & Assert
            Assert.Equal("first-name", convention.Convert("FirstName"));
            Assert.Equal("last-name", convention.Convert("LastName"));
            Assert.Equal("email-address", convention.Convert("EmailAddress"));
        }

        [Fact]
        public void Serialize_WithKebabCaseConvention_ProducesCorrectYaml()
        {
            // Arrange
            var person = new PersonModel
            {
                FirstName = "John",
                LastName = "Doe",
                EmailAddress = "john@example.com"
            };

            var settings = new YamlSerializerSettings
            {
                NamingConvention = new KebabCaseConvention()
            };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("first-name: John", yaml);
            Assert.Contains("last-name: Doe", yaml);
            Assert.Contains("email-address: john@example.com", yaml);
        }

        [Fact]
        public void Deserialize_WithKebabCaseConvention_ParsesCorrectly()
        {
            // Arrange
            var yaml = @"
first-name: John
last-name: Doe
email-address: john@example.com";

            var settings = new YamlDeserializerSettings
            {
                NamingConvention = new KebabCaseConvention()
            };

            // Act
            var person = YamlDeserializer.Deserialize<PersonModel>(yaml, settings);

            // Assert
            Assert.Equal("John", person.FirstName);
            Assert.Equal("Doe", person.LastName);
            Assert.Equal("john@example.com", person.EmailAddress);
        }

        #endregion

        #region PascalCase Convention Tests

        [Fact]
        public void PascalCaseConvention_Convert_ConvertsCorrectly()
        {
            // Arrange
            var convention = new PascalCaseConvention();

            // Act & Assert - PascalCase keeps first letter uppercase
            Assert.Equal("FirstName", convention.Convert("FirstName"));
            Assert.Equal("FirstName", convention.Convert("firstName"));
        }

        [Fact]
        public void Serialize_WithPascalCaseConvention_ProducesCorrectYaml()
        {
            // Arrange
            var person = new PersonModel
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var settings = new YamlSerializerSettings
            {
                NamingConvention = new PascalCaseConvention()
            };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("FirstName: John", yaml);
            Assert.Contains("LastName: Doe", yaml);
        }

        #endregion

        #region RoundTrip Tests with Different Conventions

        [Fact]
        public void RoundTrip_WithSnakeCaseConvention_PreservesData()
        {
            // Arrange
            var original = new PersonModel
            {
                FirstName = "John",
                LastName = "Doe",
                EmailAddress = "john@example.com",
                PhoneNumber = "555-1234"
            };

            var serializerSettings = new YamlSerializerSettings
            {
                NamingConvention = new SnakeCaseConvention()
            };

            var deserializerSettings = new YamlDeserializerSettings
            {
                NamingConvention = new SnakeCaseConvention()
            };

            // Act
            var yaml = YamlSerializer.Serialize(original, serializerSettings);
            var deserialized = YamlDeserializer.Deserialize<PersonModel>(yaml, deserializerSettings);

            // Assert
            Assert.Equal(original.FirstName, deserialized.FirstName);
            Assert.Equal(original.LastName, deserialized.LastName);
            Assert.Equal(original.EmailAddress, deserialized.EmailAddress);
            Assert.Equal(original.PhoneNumber, deserialized.PhoneNumber);
        }

        [Fact]
        public void RoundTrip_WithKebabCaseConvention_PreservesData()
        {
            // Arrange
            var original = new PersonModel
            {
                FirstName = "John",
                LastName = "Doe",
                EmailAddress = "john@example.com",
                PhoneNumber = "555-1234"
            };

            var serializerSettings = new YamlSerializerSettings
            {
                NamingConvention = new KebabCaseConvention()
            };

            var deserializerSettings = new YamlDeserializerSettings
            {
                NamingConvention = new KebabCaseConvention()
            };

            // Act
            var yaml = YamlSerializer.Serialize(original, serializerSettings);
            var deserialized = YamlDeserializer.Deserialize<PersonModel>(yaml, deserializerSettings);

            // Assert
            Assert.Equal(original.FirstName, deserialized.FirstName);
            Assert.Equal(original.LastName, deserialized.LastName);
            Assert.Equal(original.EmailAddress, deserialized.EmailAddress);
            Assert.Equal(original.PhoneNumber, deserialized.PhoneNumber);
        }

        #endregion
    }
}
