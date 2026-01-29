using Cortex.Serialization.Yaml;
using Cortex.Serialization.Yaml.Attributes;
using Cortex.Serialization.Yaml.Converters;

namespace Cortex.Tests.Serialization.Tests
{
    public class YamlSerializerTests
    {
        #region Test Models

        public class Person
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public int Age { get; set; }
            public bool IsActive { get; set; }
        }

        public class Address
        {
            public string? Street { get; set; }
            public string? City { get; set; }
            public string? Country { get; set; }
            public int ZipCode { get; set; }
        }

        public class PersonWithAddress
        {
            public string? Name { get; set; }
            public Address? Address { get; set; }
        }

        public class PersonWithIgnoredProperty
        {
            public string? Name { get; set; }

            [YamlIgnore]
            public string? Password { get; set; }

            public int Age { get; set; }
        }

        public class PersonWithCustomPropertyName
        {
            [YamlProperty(Name = "full-name")]
            public string? FullName { get; set; }

            [YamlProperty(Name = "date-of-birth")]
            public DateTime DateOfBirth { get; set; }
        }

        public class AllPrimitiveTypes
        {
            public string? StringValue { get; set; }
            public bool BoolValue { get; set; }
            public int IntValue { get; set; }
            public long LongValue { get; set; }
            public double DoubleValue { get; set; }
            public decimal DecimalValue { get; set; }
            public Guid GuidValue { get; set; }
            public DateTime DateTimeValue { get; set; }
        }

        public class PersonWithCollection
        {
            public string? Name { get; set; }
            public List<string>? Tags { get; set; }
            public int[]? Scores { get; set; }
        }

        public class PersonWithDictionary
        {
            public string? Name { get; set; }
            public Dictionary<string, string>? Metadata { get; set; }
            public Dictionary<string, int>? Counts { get; set; }
        }

        #endregion

        #region Basic Serialization Tests

        [Fact]
        public void Serialize_NullObject_ReturnsNullYaml()
        {
            // Act
            var yaml = YamlSerializer.Serialize(null);

            // Assert
            Assert.Contains("null", yaml.ToLower());
        }

        [Fact]
        public void Serialize_SimpleObject_ProducesValidYaml()
        {
            // Arrange
            var person = new Person
            {
                FirstName = "John",
                LastName = "Doe",
                Age = 30,
                IsActive = true
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("firstName: John", yaml);
            Assert.Contains("lastName: Doe", yaml);
            Assert.Contains("age: 30", yaml);
            Assert.Contains("isActive: true", yaml);
        }

        [Fact]
        public void Serialize_NestedObject_ProducesValidYaml()
        {
            // Arrange
            var person = new PersonWithAddress
            {
                Name = "John",
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "New York",
                    Country = "USA",
                    ZipCode = 10001
                }
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("name: John", yaml);
            Assert.Contains("address:", yaml);
            Assert.Contains("street: 123 Main St", yaml);
            Assert.Contains("city: New York", yaml);
            Assert.Contains("country: USA", yaml);
            Assert.Contains("zipCode: 10001", yaml);
        }

        #endregion

        #region Primitive Types Tests

        [Fact]
        public void Serialize_StringValue_ProducesValidYaml()
        {
            // Act
            var yaml = YamlSerializer.Serialize("Hello World");

            // Assert
            Assert.Contains("Hello World", yaml);
        }

        [Fact]
        public void Serialize_IntValue_ProducesValidYaml()
        {
            // Act
            var yaml = YamlSerializer.Serialize(42);

            // Assert
            Assert.Contains("42", yaml);
        }

        [Fact]
        public void Serialize_BoolValue_ProducesValidYaml()
        {
            // Act
            var yamlTrue = YamlSerializer.Serialize(true);
            var yamlFalse = YamlSerializer.Serialize(false);

            // Assert
            Assert.Contains("true", yamlTrue.ToLower());
            Assert.Contains("false", yamlFalse.ToLower());
        }

        [Fact]
        public void Serialize_AllPrimitiveTypes_ProducesValidYaml()
        {
            // Arrange
            var obj = new AllPrimitiveTypes
            {
                StringValue = "test",
                BoolValue = true,
                IntValue = 42,
                LongValue = 9999999999L,
                DoubleValue = 3.14,
                DecimalValue = 99.99m,
                GuidValue = Guid.Parse("12345678-1234-1234-1234-123456789012"),
                DateTimeValue = new DateTime(2024, 1, 15, 10, 30, 0)
            };

            // Act
            var yaml = YamlSerializer.Serialize(obj);

            // Assert
            Assert.Contains("stringValue: test", yaml);
            Assert.Contains("boolValue: true", yaml);
            Assert.Contains("intValue: 42", yaml);
            Assert.Contains("longValue: 9999999999", yaml);
            Assert.Contains("doubleValue:", yaml);
            Assert.Contains("decimalValue:", yaml);
            Assert.Contains("guidValue:", yaml);
            Assert.Contains("dateTimeValue:", yaml);
        }

        #endregion

        #region Collection Tests

        [Fact]
        public void Serialize_ListOfStrings_ProducesValidYaml()
        {
            // Arrange
            var list = new List<string> { "apple", "banana", "cherry" };

            // Act
            var yaml = YamlSerializer.Serialize(list);

            // Assert
            Assert.Contains("- apple", yaml);
            Assert.Contains("- banana", yaml);
            Assert.Contains("- cherry", yaml);
        }

        [Fact]
        public void Serialize_Array_ProducesValidYaml()
        {
            // Arrange
            var array = new int[] { 1, 2, 3, 4, 5 };

            // Act
            var yaml = YamlSerializer.Serialize(array);

            // Assert
            Assert.Contains("- 1", yaml);
            Assert.Contains("- 2", yaml);
            Assert.Contains("- 3", yaml);
            Assert.Contains("- 4", yaml);
            Assert.Contains("- 5", yaml);
        }

        [Fact]
        public void Serialize_ObjectWithCollection_ProducesValidYaml()
        {
            // Arrange
            var person = new PersonWithCollection
            {
                Name = "John",
                Tags = new List<string> { "developer", "speaker" },
                Scores = new int[] { 100, 95, 88 }
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("name: John", yaml);
            Assert.Contains("tags:", yaml);
            Assert.Contains("- developer", yaml);
            Assert.Contains("- speaker", yaml);
            Assert.Contains("scores:", yaml);
            Assert.Contains("- 100", yaml);
            Assert.Contains("- 95", yaml);
            Assert.Contains("- 88", yaml);
        }

        [Fact]
        public void Serialize_ListOfObjects_ProducesValidYaml()
        {
            // Arrange
            var people = new List<Person>
            {
                new Person { FirstName = "John", LastName = "Doe", Age = 30 },
                new Person { FirstName = "Jane", LastName = "Smith", Age = 25 }
            };

            // Act
            var yaml = YamlSerializer.Serialize(people);

            // Assert
            Assert.Contains("firstName: John", yaml);
            Assert.Contains("lastName: Doe", yaml);
            Assert.Contains("firstName: Jane", yaml);
            Assert.Contains("lastName: Smith", yaml);
        }

        #endregion

        #region Dictionary Tests

        [Fact]
        public void Serialize_Dictionary_ProducesValidYaml()
        {
            // Arrange
            var dict = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            };

            // Act
            var yaml = YamlSerializer.Serialize(dict);

            // Assert
            Assert.Contains("key1: value1", yaml);
            Assert.Contains("key2: value2", yaml);
        }

        [Fact]
        public void Serialize_DictionaryWithIntValues_ProducesValidYaml()
        {
            // Arrange
            var dict = new Dictionary<string, int>
            {
                ["count"] = 42,
                ["total"] = 100
            };

            // Act
            var yaml = YamlSerializer.Serialize(dict);

            // Assert
            Assert.Contains("count: 42", yaml);
            Assert.Contains("total: 100", yaml);
        }

        [Fact]
        public void Serialize_ObjectWithDictionary_ProducesValidYaml()
        {
            // Arrange
            var person = new PersonWithDictionary
            {
                Name = "John",
                Metadata = new Dictionary<string, string>
                {
                    ["role"] = "admin",
                    ["department"] = "IT"
                },
                Counts = new Dictionary<string, int>
                {
                    ["visits"] = 10,
                    ["purchases"] = 5
                }
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("name: John", yaml);
            Assert.Contains("role: admin", yaml);
            Assert.Contains("department: IT", yaml);
            Assert.Contains("visits: 10", yaml);
            Assert.Contains("purchases: 5", yaml);
        }

        #endregion

        #region Settings Tests

        [Fact]
        public void Serialize_WithEmitNullsFalse_OmitsNullProperties()
        {
            // Arrange
            var person = new Person
            {
                FirstName = "John",
                LastName = null,
                Age = 30
            };

            var settings = new YamlSerializerSettings { EmitNulls = false };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("firstName: John", yaml);
            Assert.Contains("age: 30", yaml);
            Assert.DoesNotContain("lastName", yaml);
        }

        [Fact]
        public void Serialize_WithEmitNullsTrue_IncludesNullProperties()
        {
            // Arrange
            var person = new Person
            {
                FirstName = "John",
                LastName = null,
                Age = 30
            };

            var settings = new YamlSerializerSettings { EmitNulls = true };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("firstName: John", yaml);
            Assert.Contains("lastName:", yaml);
            Assert.Contains("age: 30", yaml);
        }

        [Fact]
        public void Serialize_WithEmitDefaultsFalse_OmitsDefaultValues()
        {
            // Arrange
            var person = new Person
            {
                FirstName = "John",
                LastName = "Doe",
                Age = 0,  // default value
                IsActive = false  // default value
            };

            var settings = new YamlSerializerSettings { EmitDefaults = false };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("firstName: John", yaml);
            Assert.Contains("lastName: Doe", yaml);
            Assert.DoesNotContain("age: 0", yaml);
            Assert.DoesNotContain("isActive: false", yaml);
        }

        [Fact]
        public void Serialize_WithSortPropertiesTrue_SortsPropertiesAlphabetically()
        {
            // Arrange
            var person = new Person
            {
                FirstName = "John",
                LastName = "Doe",
                Age = 30,
                IsActive = true
            };

            var settings = new YamlSerializerSettings { SortProperties = true };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);
            var lines = yaml.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            // Assert - properties should be in alphabetical order: age, firstName, isActive, lastName
            var ageIndex = lines.FindIndex(l => l.Contains("age:"));
            var firstNameIndex = lines.FindIndex(l => l.Contains("firstName:"));
            var isActiveIndex = lines.FindIndex(l => l.Contains("isActive:"));
            var lastNameIndex = lines.FindIndex(l => l.Contains("lastName:"));

            Assert.True(ageIndex < firstNameIndex, "age should come before firstName");
            Assert.True(firstNameIndex < isActiveIndex, "firstName should come before isActive");
            Assert.True(isActiveIndex < lastNameIndex, "isActive should come before lastName");
        }

        [Fact]
        public void Serialize_WithCustomIndentation_UsesCorrectIndentation()
        {
            // Arrange
            var person = new PersonWithAddress
            {
                Name = "John",
                Address = new Address { Street = "123 Main St", City = "NYC" }
            };

            var settings = new YamlSerializerSettings { Indentation = 4 };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("    street:", yaml);  // 4 spaces indentation
        }

        #endregion

        #region Attribute Tests

        [Fact]
        public void Serialize_WithYamlIgnore_OmitsIgnoredProperty()
        {
            // Arrange
            var person = new PersonWithIgnoredProperty
            {
                Name = "John",
                Password = "secret123",
                Age = 30
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("name: John", yaml);
            Assert.Contains("age: 30", yaml);
            Assert.DoesNotContain("password", yaml.ToLower());
            Assert.DoesNotContain("secret123", yaml);
        }

        [Fact]
        public void Serialize_WithYamlProperty_UsesCustomPropertyName()
        {
            // Arrange
            var person = new PersonWithCustomPropertyName
            {
                FullName = "John Doe",
                DateOfBirth = new DateTime(1990, 1, 15)
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("full-name: John Doe", yaml);
            Assert.Contains("date-of-birth:", yaml);
        }

        #endregion

        #region Instance API Tests

        [Fact]
        public void SerializeWithInstance_ProducesSameResultAsStaticMethod()
        {
            // Arrange
            var person = new Person { FirstName = "John", Age = 30 };
            var settings = new YamlSerializerSettings();
            var serializer = new YamlSerializer(settings);

            // Act
            var yamlFromStatic = YamlSerializer.Serialize(person, settings);
            var yamlFromInstance = serializer.Serialize(person);

            // Assert
            Assert.Equal(yamlFromStatic, yamlFromInstance);
        }

        [Fact]
        public void SerializeWithInstance_ReusesSettings()
        {
            // Arrange
            var settings = new YamlSerializerSettings { EmitNulls = false };
            var serializer = new YamlSerializer(settings);

            var person1 = new Person { FirstName = "John", LastName = null };
            var person2 = new Person { FirstName = "Jane", LastName = null };

            // Act
            var yaml1 = serializer.Serialize(person1);
            var yaml2 = serializer.Serialize(person2);

            // Assert
            Assert.DoesNotContain("lastName", yaml1);
            Assert.DoesNotContain("lastName", yaml2);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Serialize_EmptyObject_ProducesValidYaml()
        {
            // Arrange
            var person = new Person();

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert - should produce valid YAML with default values
            Assert.NotNull(yaml);
            Assert.NotEmpty(yaml);
        }

        [Fact]
        public void Serialize_EmptyCollection_ProducesValidYaml()
        {
            // Arrange
            var list = new List<string>();

            // Act
            var yaml = YamlSerializer.Serialize(list);

            // Assert
            Assert.NotNull(yaml);
        }

        [Fact]
        public void Serialize_EmptyDictionary_ProducesValidYaml()
        {
            // Arrange
            var dict = new Dictionary<string, string>();

            // Act
            var yaml = YamlSerializer.Serialize(dict);

            // Assert
            Assert.NotNull(yaml);
        }

        [Fact]
        public void Serialize_StringWithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var person = new Person
            {
                FirstName = "John \"Jack\"",
                LastName = "O'Brien"
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.NotNull(yaml);
            Assert.Contains("John", yaml);
            Assert.Contains("Brien", yaml);
        }

        #endregion
    }
}
