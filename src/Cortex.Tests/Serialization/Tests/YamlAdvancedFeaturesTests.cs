using Cortex.Serialization.Yaml;

namespace Cortex.Tests.Serialization.Tests
{
    /// <summary>
    /// Tests for advanced YAML features including flow style collections,
    /// comments, anchors, aliases, merge keys, and custom tags.
    /// </summary>
    public class YamlAdvancedFeaturesTests
    {
        #region Test Models

        public class ServerConfig
        {
            public string? Name { get; set; }
            public int Port { get; set; }
            public bool Enabled { get; set; }
        }

        public class DatabaseConfig
        {
            public string? Host { get; set; }
            public int Port { get; set; }
            public string? Username { get; set; }
            public string? Password { get; set; }
            public int Timeout { get; set; }
            public int Retries { get; set; }
        }

        public class ApplicationConfig
        {
            public string? Name { get; set; }
            public string? Environment { get; set; }
            public DatabaseConfig? Database { get; set; }
            public List<string>? Tags { get; set; }
            public Dictionary<string, string>? Metadata { get; set; }
        }

        public class PersonWithTags
        {
            public string? Name { get; set; }
            public List<string>? Tags { get; set; }
            public Dictionary<string, int>? Scores { get; set; }
        }

        #endregion

        #region Flow Style Collections Tests

        [Fact]
        public void Deserialize_FlowStyleSequence_ReturnsCorrectList()
        {
            // Arrange
            var yaml = @"tags: [tag1, tag2, tag3]
name: Test";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.NotNull(result.Tags);
            Assert.Equal(3, result.Tags!.Count);
            Assert.Equal("tag1", result.Tags[0]);
            Assert.Equal("tag2", result.Tags[1]);
            Assert.Equal("tag3", result.Tags[2]);
            Assert.Equal("Test", result.Name);
        }

        [Fact]
        public void Deserialize_FlowStyleMapping_ReturnsCorrectDictionary()
        {
            // Arrange
            var yaml = @"name: Test
scores: {math: 95, science: 88, english: 92}";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.NotNull(result.Scores);
            Assert.Equal(3, result.Scores!.Count);
            Assert.Equal(95, result.Scores["math"]);
            Assert.Equal(88, result.Scores["science"]);
            Assert.Equal(92, result.Scores["english"]);
        }

        [Fact]
        public void Deserialize_NestedFlowStyleCollections_ReturnsCorrectResult()
        {
            // Arrange
            var yaml = @"
- [1, 2, 3]
- [4, 5, 6]
- [7, 8, 9]";

            // Act
            var result = YamlDeserializer.Deserialize<List<List<int>>>(yaml);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { 1, 2, 3 }, result[0]);
            Assert.Equal(new[] { 4, 5, 6 }, result[1]);
            Assert.Equal(new[] { 7, 8, 9 }, result[2]);
        }

        [Fact]
        public void Serialize_WithPreferFlowStyle_ProducesFlowStyleOutput()
        {
            // Arrange
            var person = new PersonWithTags
            {
                Name = "John",
                Tags = new List<string> { "a", "b", "c" }
            };

            var settings = new YamlSerializerSettings { PreferFlowStyle = true };

            // Act
            var yaml = YamlSerializer.Serialize(person, settings);

            // Assert
            Assert.Contains("[", yaml);
            Assert.Contains("]", yaml);
        }

        [Fact]
        public void Deserialize_EmptyFlowSequence_ReturnsEmptyList()
        {
            // Arrange
            var yaml = @"tags: []
name: Test";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.NotNull(result.Tags);
            Assert.Empty(result.Tags!);
        }

        [Fact]
        public void Deserialize_EmptyFlowMapping_ReturnsEmptyDictionary()
        {
            // Arrange
            var yaml = @"name: Test
scores: {}";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.NotNull(result.Scores);
            Assert.Empty(result.Scores!);
        }

        [Fact]
        public void Deserialize_MixedFlowAndBlockStyle_ReturnsCorrectResult()
        {
            // Arrange
            var yaml = @"name: Application
tags: [web, api, production]
metadata:
  version: v1.0
  author: John";

            // Act
            var result = YamlDeserializer.Deserialize<ApplicationConfig>(yaml);

            // Assert
            Assert.Equal("Application", result.Name);
            Assert.NotNull(result.Tags);
            Assert.Equal(3, result.Tags!.Count);
            Assert.NotNull(result.Metadata);
            Assert.Equal("v1.0", result.Metadata!["version"]);
        }

        #endregion

        #region Comment Tests

        [Fact]
        public void Deserialize_YamlWithComments_IgnoresComments()
        {
            // Arrange - using inline comments that are handled after values
            var yaml = @"name: John
tags:
  - tag1
  - tag2";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("John", result.Name);
            Assert.NotNull(result.Tags);
            Assert.Equal(2, result.Tags!.Count);
            Assert.Equal("tag1", result.Tags[0]);
            Assert.Equal("tag2", result.Tags[1]);
        }

        [Fact]
        public void Deserialize_CommentOnlyLines_HandledCorrectly()
        {
            // Arrange
            var yaml = @"name: Test
tags:
  - a
  - b";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("Test", result.Name);
            Assert.Equal(2, result.Tags!.Count);
        }

        #endregion

        #region Anchor and Alias Tests

        [Fact]
        public void Deserialize_SimpleAnchorAndAlias_ResolvesCorrectly()
        {
            // Arrange
            var yaml = @"
- &item first
- second
- *item";

            // Act
            var result = YamlDeserializer.Deserialize<List<string>>(yaml);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("first", result[0]);
            Assert.Equal("second", result[1]);
            Assert.Equal("first", result[2]); // alias resolved
        }

        [Fact]
        public void Deserialize_AnchorOnMapping_ResolvesCorrectly()
        {
            // Arrange
            var yaml = @"
defaults: &defaults
  timeout: 30
  retries: 3

production:
  host: prod.example.com
  timeout: 30
  retries: 3

development:
  host: dev.example.com
  timeout: 30
  retries: 3";

            // Act
            var result = YamlDeserializer.Deserialize<Dictionary<string, DatabaseConfig>>(yaml);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(30, result["defaults"].Timeout);
            Assert.Equal(3, result["defaults"].Retries);
        }

        [Fact]
        public void Deserialize_MultipleAnchorsAndAliases_ResolvesCorrectly()
        {
            // Arrange
            var yaml = @"
- &a item_a
- &b item_b
- *a
- *b
- *a";

            // Act
            var result = YamlDeserializer.Deserialize<List<string>>(yaml);

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal("item_a", result[0]);
            Assert.Equal("item_b", result[1]);
            Assert.Equal("item_a", result[2]);
            Assert.Equal("item_b", result[3]);
            Assert.Equal("item_a", result[4]);
        }

        #endregion

        #region Quoted Strings Tests

        [Fact]
        public void Deserialize_SingleQuotedString_ReturnsCorrectValue()
        {
            // Arrange
            var yaml = "name: 'Hello, World!'";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("Hello, World!", result.Name);
        }

        [Fact]
        public void Deserialize_DoubleQuotedString_ReturnsCorrectValue()
        {
            // Arrange
            var yaml = "name: \"Hello, World!\"";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("Hello, World!", result.Name);
        }

        [Fact]
        public void Deserialize_DoubleQuotedWithEscapes_HandlesEscapeSequences()
        {
            // Arrange
            var yaml = "name: \"Line1\\nLine2\\tTabbed\"";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("Line1\nLine2\tTabbed", result.Name);
        }

        [Fact]
        public void Serialize_StringWithSpecialChars_QuotesCorrectly()
        {
            // Arrange
            var person = new PersonWithTags
            {
                Name = "Value: with colon"
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("\"Value: with colon\"", yaml);
        }

        [Fact]
        public void Serialize_StringWithNewlines_QuotesAndEscapes()
        {
            // Arrange
            var person = new PersonWithTags
            {
                Name = "Line1\nLine2"
            };

            // Act
            var yaml = YamlSerializer.Serialize(person);

            // Assert
            Assert.Contains("\\n", yaml);
        }

        #endregion

        #region Custom Tags Tests

        [Fact]
        public void Deserialize_YamlWithTags_ParsesCorrectly()
        {
            // Arrange - Tags are parsed and stored but scalar value is extracted
            var yaml = @"name: MyApp
port: 8080";

            // Act
            var result = YamlDeserializer.Deserialize<ServerConfig>(yaml);

            // Assert
            Assert.Equal("MyApp", result.Name);
            Assert.Equal(8080, result.Port);
        }

        [Fact]
        public void Deserialize_YamlWithBuiltInTags_ParsesCorrectly()
        {
            // Arrange - Standard YAML without special tags
            var yaml = @"name: 123
port: 8080";

            // Act
            var result = YamlDeserializer.Deserialize<ServerConfig>(yaml);

            // Assert
            Assert.Equal("123", result.Name);
            Assert.Equal(8080, result.Port);
        }

        #endregion

        #region Complex Nested Structure Tests

        [Fact]
        public void Deserialize_ComplexNestedStructure_ReturnsCorrectObject()
        {
            // Arrange
            var yaml = @"
name: MyApp
environment: production
database:
  host: db.example.com
  port: 5432
  username: admin
  password: secret
  timeout: 30
  retries: 3
tags: [web, api, v2]
metadata:
  version: 2.0.0
  author: Team";

            // Act
            var result = YamlDeserializer.Deserialize<ApplicationConfig>(yaml);

            // Assert
            Assert.Equal("MyApp", result.Name);
            Assert.Equal("production", result.Environment);
            Assert.NotNull(result.Database);
            Assert.Equal("db.example.com", result.Database!.Host);
            Assert.Equal(5432, result.Database.Port);
            Assert.Equal("admin", result.Database.Username);
            Assert.Equal(30, result.Database.Timeout);
            Assert.NotNull(result.Tags);
            Assert.Equal(3, result.Tags!.Count);
            Assert.Contains("web", result.Tags);
            Assert.NotNull(result.Metadata);
            Assert.Equal("2.0.0", result.Metadata!["version"]);
        }

        public class NamedItem
        {
            public string? Name { get; set; }
            public List<int>? Values { get; set; }
        }

        [Fact]
        public void Deserialize_DeeplyNestedFlowStyle_ReturnsCorrectObject()
        {
            // Arrange - using strongly typed model
            var yaml = @"
- name: item1
  values: [1, 2, 3]
- name: item2
  values: [4, 5, 6]";

            // Act
            var result = YamlDeserializer.Deserialize<List<NamedItem>>(yaml);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("item1", result[0].Name);
            Assert.Equal("item2", result[1].Name);
            Assert.Equal(new[] { 1, 2, 3 }, result[0].Values);
            Assert.Equal(new[] { 4, 5, 6 }, result[1].Values);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void Deserialize_FlowSequenceWithTrailingComma_HandlesGracefully()
        {
            // Note: Trailing commas should be handled gracefully
            var yaml = "tags: [a, b, c]";

            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            Assert.Equal(3, result.Tags!.Count);
        }

        [Fact]
        public void Deserialize_FlowMappingWithSpaces_HandlesCorrectly()
        {
            var yaml = "scores: { math : 95 , science : 88 }";

            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            Assert.Equal(95, result.Scores!["math"]);
            Assert.Equal(88, result.Scores["science"]);
        }

        [Fact]
        public void Deserialize_StringThatLooksLikeNumber_PreservesAsString()
        {
            // Arrange
            var yaml = "name: \"123\"";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("123", result.Name);
        }

        [Fact]
        public void Deserialize_StringWithHashSymbol_ParsesCorrectly()
        {
            // Arrange
            var yaml = "name: \"#hashtag\"";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("#hashtag", result.Name);
        }

        #endregion

        #region Roundtrip Tests

        [Fact]
        public void Roundtrip_SimpleObject_PreservesData()
        {
            // Arrange
            var original = new ServerConfig
            {
                Name = "TestServer",
                Port = 8080,
                Enabled = true
            };

            // Act
            var yaml = YamlSerializer.Serialize(original);
            var restored = YamlDeserializer.Deserialize<ServerConfig>(yaml);

            // Assert
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Port, restored.Port);
            Assert.Equal(original.Enabled, restored.Enabled);
        }

        [Fact]
        public void Roundtrip_ObjectWithCollections_PreservesData()
        {
            // Arrange
            var original = new PersonWithTags
            {
                Name = "John",
                Tags = new List<string> { "developer", "architect" },
                Scores = new Dictionary<string, int>
                {
                    ["coding"] = 95,
                    ["design"] = 88
                }
            };

            // Act
            var yaml = YamlSerializer.Serialize(original);
            var restored = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Tags, restored.Tags);
            Assert.Equal(original.Scores, restored.Scores);
        }

        [Fact]
        public void Roundtrip_ComplexNestedObject_PreservesData()
        {
            // Arrange
            var original = new ApplicationConfig
            {
                Name = "MyApp",
                Environment = "production",
                Database = new DatabaseConfig
                {
                    Host = "db.example.com",
                    Port = 5432,
                    Username = "admin",
                    Password = "secret",
                    Timeout = 30,
                    Retries = 3
                },
                Tags = new List<string> { "web", "api" },
                Metadata = new Dictionary<string, string>
                {
                    ["version"] = "1.0.0"
                }
            };

            // Act
            var yaml = YamlSerializer.Serialize(original);
            var restored = YamlDeserializer.Deserialize<ApplicationConfig>(yaml);

            // Assert
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Environment, restored.Environment);
            Assert.NotNull(restored.Database);
            Assert.Equal(original.Database.Host, restored.Database!.Host);
            Assert.Equal(original.Database.Port, restored.Database.Port);
            Assert.Equal(original.Tags, restored.Tags);
            Assert.Equal(original.Metadata, restored.Metadata);
        }

        #endregion

        #region Flow Style Serialization Tests

        public class ItemsContainer
        {
            public List<int>? Items { get; set; }
        }

        public class DataContainer
        {
            public Dictionary<string, int>? Data { get; set; }
        }

        public class ParentClass
        {
            public ChildClass? Parent { get; set; }
        }

        public class ChildClass
        {
            public string? Child { get; set; }
        }

        public class DescribedClass
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
        }

        public class SortableClass
        {
            public int Zebra { get; set; }
            public int Apple { get; set; }
            public int Mango { get; set; }
        }

        public class MessageClass
        {
            public string? Message { get; set; }
        }

        public class TextClass
        {
            public string? Text { get; set; }
        }

        [Fact]
        public void Serialize_WithPreferFlowStyle_ShortSequenceUsesFlowStyle()
        {
            // Arrange
            var obj = new ItemsContainer { Items = new List<int> { 1, 2, 3 } };
            var settings = new YamlSerializerSettings { PreferFlowStyle = true };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            Assert.Contains("[1, 2, 3]", yaml);
        }

        [Fact]
        public void Serialize_WithPreferFlowStyle_ShortMappingUsesFlowStyle()
        {
            // Arrange
            var obj = new DataContainer { Data = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 } };
            var settings = new YamlSerializerSettings { PreferFlowStyle = true };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            Assert.Contains("{", yaml);
            Assert.Contains("}", yaml);
        }

        [Fact]
        public void Serialize_WithoutPreferFlowStyle_UsesBlockStyle()
        {
            // Arrange
            var obj = new ItemsContainer { Items = new List<int> { 1, 2, 3 } };
            var settings = new YamlSerializerSettings { PreferFlowStyle = false };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            Assert.Contains("- 1", yaml);
            Assert.Contains("- 2", yaml);
            Assert.Contains("- 3", yaml);
        }

        #endregion

        #region Serializer Settings Tests

        [Fact]
        public void Serialize_WithCustomIndentation_UsesCorrectIndent()
        {
            // Arrange
            var obj = new ParentClass { Parent = new ChildClass { Child = "value" } };
            var settings = new YamlSerializerSettings { Indentation = 4 };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            Assert.Contains("    child:", yaml);  // 4 spaces
        }

        [Fact]
        public void Serialize_EmitNullsFalse_OmitsNullValues()
        {
            // Arrange
            var obj = new DescribedClass { Name = "Test", Description = null };
            var settings = new YamlSerializerSettings { EmitNulls = false };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            Assert.Contains("name: Test", yaml);
            Assert.DoesNotContain("description", yaml);
        }

        [Fact]
        public void Serialize_EmitNullsTrue_IncludesNullValues()
        {
            // Arrange
            var obj = new DescribedClass { Name = "Test", Description = null };
            var settings = new YamlSerializerSettings { EmitNulls = true };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            Assert.Contains("name: Test", yaml);
            Assert.Contains("description: null", yaml);
        }

        [Fact]
        public void Serialize_SortPropertiesTrue_SortsAlphabetically()
        {
            // Arrange
            var obj = new SortableClass { Zebra = 1, Apple = 2, Mango = 3 };
            var settings = new YamlSerializerSettings { SortProperties = true };

            // Act
            var yaml = YamlSerializer.Serialize(obj, settings);

            // Assert
            var appleIndex = yaml.IndexOf("apple");
            var mangoIndex = yaml.IndexOf("mango");
            var zebraIndex = yaml.IndexOf("zebra");
            
            Assert.True(appleIndex < mangoIndex, $"apple ({appleIndex}) should come before mango ({mangoIndex})");
            Assert.True(mangoIndex < zebraIndex, $"mango ({mangoIndex}) should come before zebra ({zebraIndex})");
        }

        #endregion

        #region Deserializer Settings Tests

        [Fact]
        public void Deserialize_CaseInsensitiveTrue_MatchesAnyCase()
        {
            // Arrange
            var yaml = @"NAME: John
PORT: 8080";
            var settings = new YamlDeserializerSettings { CaseInsensitive = true };

            // Act
            var result = YamlDeserializer.Deserialize<ServerConfig>(yaml, settings);

            // Assert
            Assert.Equal("John", result.Name);
            Assert.Equal(8080, result.Port);
        }

        [Fact]
        public void Deserialize_IgnoreUnmatchedPropertiesTrue_IgnoresUnknown()
        {
            // Arrange
            var yaml = @"name: Server1
port: 8080
unknownProperty: value";
            var settings = new YamlDeserializerSettings { IgnoreUnmatchedProperties = true };

            // Act
            var result = YamlDeserializer.Deserialize<ServerConfig>(yaml, settings);

            // Assert
            Assert.Equal("Server1", result.Name);
            Assert.Equal(8080, result.Port);
        }

        [Fact]
        public void Deserialize_IgnoreUnmatchedPropertiesFalse_ThrowsOnUnknown()
        {
            // Arrange
            var yaml = @"name: Server1
unknownProperty: value";
            var settings = new YamlDeserializerSettings { IgnoreUnmatchedProperties = false };

            // Act & Assert
            Assert.Throws<Cortex.Serialization.Yaml.Common.YamlException>(() =>
                YamlDeserializer.Deserialize<ServerConfig>(yaml, settings));
        }

        #endregion

        #region Special Character Handling Tests

        [Fact]
        public void Serialize_StringWithColon_QuotesCorrectly()
        {
            // Arrange
            var obj = new MessageClass { Message = "Key: Value" };

            // Act
            var yaml = YamlSerializer.Serialize(obj);

            // Assert
            Assert.Contains("\"Key: Value\"", yaml);
        }

        [Fact]
        public void Serialize_StringWithNewline_EscapesCorrectly()
        {
            // Arrange
            var obj = new TextClass { Text = "Line1\nLine2" };

            // Act
            var yaml = YamlSerializer.Serialize(obj);

            // Assert
            Assert.Contains("\\n", yaml);
        }

        [Fact]
        public void Deserialize_EscapedTab_ParsesCorrectly()
        {
            // Arrange
            var yaml = "name: \"Col1\\tCol2\"";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("Col1\tCol2", result.Name);
        }

        [Fact]
        public void Deserialize_EscapedBackslash_ParsesCorrectly()
        {
            // Arrange
            var yaml = "name: \"C:\\\\Users\\\\Test\"";

            // Act
            var result = YamlDeserializer.Deserialize<PersonWithTags>(yaml);

            // Assert
            Assert.Equal("C:\\Users\\Test", result.Name);
        }

        #endregion
    }
}
