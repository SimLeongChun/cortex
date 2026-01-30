# Cortex.Serialization.Yaml - User Guide

A lightweight, high-performance YAML serialization library for .NET that supports essential YAML features while maintaining simplicity and ease of use.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Serialization](#serialization)
- [Deserialization](#deserialization)
- [Configuration Options](#configuration-options)
- [Advanced Features](#advanced-features)
  - [Flow Style Collections](#flow-style-collections)
  - [Comments](#comments)
  - [Anchors and Aliases](#anchors-and-aliases)
  - [Custom Tags](#custom-tags)
  - [Quoting and Escaping](#quoting-and-escaping)
- [Naming Conventions](#naming-conventions)
- [Custom Type Converters](#custom-type-converters)
- [Attributes](#attributes)
- [Best Practices](#best-practices)
- [API Reference](#api-reference)

## Installation

Add the `Cortex.Serialization.Yaml` package to your project:

```bash
dotnet add package Cortex.Serialization.Yaml
```

Or via Package Manager:

```powershell
Install-Package Cortex.Serialization.Yaml
```

## Quick Start

### Basic Serialization

```csharp
using Cortex.Serialization.Yaml;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

var person = new Person 
{ 
    Name = "John Doe", 
    Age = 30, 
    IsActive = true 
};

// Serialize to YAML
string yaml = YamlSerializer.Serialize(person);

// Output:
// name: John Doe
// age: 30
// isActive: true
```

### Basic Deserialization

```csharp
var yaml = @"
name: Jane Smith
age: 25
isActive: true";

var person = YamlDeserializer.Deserialize<Person>(yaml);
Console.WriteLine(person.Name); // "Jane Smith"
```

## Serialization

### Static API

The simplest way to serialize objects:

```csharp
// With default settings
string yaml = YamlSerializer.Serialize(obj);

// With custom settings
var settings = new YamlSerializerSettings { EmitNulls = false };
string yaml = YamlSerializer.Serialize(obj, settings);
```

### Instance API

For multiple serializations with the same settings:

```csharp
var serializer = new YamlSerializer(new YamlSerializerSettings 
{ 
    SortProperties = true,
    Indentation = 4
});

string yaml1 = serializer.Serialize(obj1);
string yaml2 = serializer.Serialize(obj2);
```

### Supported Types

The serializer automatically handles:

| Type | YAML Representation |
|------|---------------------|
| `string` | Scalar (quoted if needed) |
| `bool` | `true` / `false` |
| `int`, `long`, `double`, `decimal` | Numeric scalar |
| `DateTime`, `DateOnly`, `TimeOnly` | ISO 8601 string |
| `Guid` | String representation |
| `List<T>`, `T[]` | Sequence (- item) |
| `Dictionary<K,V>` | Mapping (key: value) |
| Custom objects | Mapping of properties |

## Deserialization

### From String

```csharp
// Generic method
var person = YamlDeserializer.Deserialize<Person>(yaml);

// Non-generic method (runtime type)
var obj = YamlDeserializer.Deserialize(yaml, typeof(Person));
```

### From TextReader

```csharp
using var reader = new StreamReader("config.yaml");
var config = YamlDeserializer.Deserialize<Config>(reader);
```

### Instance API

```csharp
var deserializer = new YamlDeserializer(new YamlDeserializerSettings
{
    CaseInsensitive = true,
    IgnoreUnmatchedProperties = true
});

var obj = deserializer.Deserialize<MyClass>(yaml);
```

## Configuration Options

### YamlSerializerSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `NamingConvention` | `INamingConvention` | `CamelCaseConvention` | Property name transformation |
| `EmitNulls` | `bool` | `true` | Include null properties in output |
| `EmitDefaults` | `bool` | `true` | Include default values in output |
| `SortProperties` | `bool` | `false` | Sort properties alphabetically |
| `Indentation` | `int` | `2` | Spaces per indentation level |
| `PreferFlowStyle` | `bool` | `false` | Use `[...]` and `{...}` for collections |
| `FlowStyleThreshold` | `int` | `80` | Max line length for flow style |
| `EmitComments` | `bool` | `true` | Emit associated comments |

### YamlDeserializerSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `NamingConvention` | `INamingConvention` | `CamelCaseConvention` | Property name transformation |
| `CaseInsensitive` | `bool` | `true` | Ignore case when matching properties |
| `IgnoreUnmatchedProperties` | `bool` | `true` | Silently ignore unknown properties |
| `PreserveComments` | `bool` | `false` | Preserve comments for round-trip |
| `ResolveAnchors` | `bool` | `true` | Automatically resolve YAML aliases |

## Advanced Features

### Flow Style Collections

Flow style provides compact, JSON-like syntax for simple collections:

**Sequences:**
```yaml
# Block style (default)
tags:
  - web
  - api
  - production

# Flow style
tags: [web, api, production]
```

**Mappings:**
```yaml
# Block style (default)
metadata:
  version: 1.0
  author: John

# Flow style
metadata: {version: 1.0, author: John}
```

**Enabling flow style in serialization:**
```csharp
var settings = new YamlSerializerSettings 
{ 
    PreferFlowStyle = true,
    FlowStyleThreshold = 80  // Max line length
};
```

### Comments

Comments are preserved during parsing when enabled:

```yaml
# Database configuration
database:
  host: localhost  # Main server
  port: 5432
```

```csharp
var settings = new YamlDeserializerSettings 
{ 
    PreserveComments = true 
};
```

### Anchors and Aliases

Anchors (`&`) define reusable values, aliases (`*`) reference them:

```yaml
# Define anchor
defaults: &defaults
  timeout: 30
  retries: 3

# Reference with alias
production:
  <<: *defaults      # Merge key
  host: prod.example.com

development:
  <<: *defaults
  host: dev.example.com
```

**Example usage:**
```csharp
var yaml = @"
- &first item1
- second
- *first";

var list = YamlDeserializer.Deserialize<List<string>>(yaml);
// Result: ["item1", "second", "item1"]
```

### Custom Tags

Tags provide type hints in YAML documents:

```yaml
# Built-in tags
name: !!str 123      # Force string
count: !!int "42"    # Force integer

# Custom tags
value: !custom data
```

### Quoting and Escaping

The serializer automatically quotes strings when necessary:

```csharp
var obj = new { 
    Message = "Hello: World",     // Contains colon
    Path = "C:\\Program Files",   // Contains backslash
    Multiline = "Line1\nLine2"    // Contains newline
};

// Output uses proper escaping:
// message: "Hello: World"
// path: "C:\\Program Files"
// multiline: "Line1\nLine2"
```

**Escape sequences supported:**

| Sequence | Character |
|----------|-----------|
| `\\` | Backslash |
| `\"` | Double quote |
| `\n` | Newline |
| `\r` | Carriage return |
| `\t` | Tab |
| `\0` | Null |

## Naming Conventions

Built-in naming conventions:

| Convention | C# Property | YAML Key |
|------------|-------------|----------|
| `CamelCaseConvention` | `FirstName` | `firstName` |
| `PascalCaseConvention` | `firstName` | `FirstName` |
| `SnakeCaseConvention` | `FirstName` | `first_name` |
| `KebabCaseConvention` | `FirstName` | `first-name` |
| `OriginalCaseConvention` | `FirstName` | `FirstName` |

**Example:**
```csharp
var settings = new YamlSerializerSettings 
{ 
    NamingConvention = new SnakeCaseConvention() 
};

// C# property "UserName" becomes YAML key "user_name"
```

## Custom Type Converters

Implement `IYamlTypeConverter` for custom serialization:

```csharp
public class VersionConverter : IYamlTypeConverter
{
    public bool CanConvert(Type type) => type == typeof(Version);

    public object? Read(object? value, Type type)
    {
        return value is string s ? Version.Parse(s) : null;
    }

    public void Write(/* ... */) { /* ... */ }
}

// Register converter
var converters = new List<IYamlTypeConverter> { new VersionConverter() };
var result = YamlDeserializer.Deserialize<Config>(yaml, extra: converters);
```

## Attributes

### YamlIgnore

Exclude a property from serialization:

```csharp
public class User
{
    public string Name { get; set; }
    
    [YamlIgnore]
    public string Password { get; set; }  // Never serialized
}
```

### YamlProperty

Customize the YAML key name:

```csharp
public class Config
{
    [YamlProperty(Name = "api-key")]
    public string ApiKey { get; set; }  // Serialized as "api-key"
}
```

## Best Practices

### 1. Reuse Serializer/Deserializer Instances

```csharp
// Good - reuse instance
var serializer = new YamlSerializer(settings);
foreach (var item in items)
{
    var yaml = serializer.Serialize(item);
}

// Avoid - creating new instance each time
foreach (var item in items)
{
    var yaml = YamlSerializer.Serialize(item, settings); // Creates new instance
}
```

### 2. Handle Configuration Files

```csharp
public class AppConfig
{
    public string Environment { get; set; }
    public DatabaseConfig Database { get; set; }
    public List<string> Features { get; set; }
}

// Load configuration
using var reader = new StreamReader("appsettings.yaml");
var config = YamlDeserializer.Deserialize<AppConfig>(reader);
```

### 3. Validate After Deserialization

```csharp
var config = YamlDeserializer.Deserialize<Config>(yaml);

if (string.IsNullOrEmpty(config.ConnectionString))
    throw new InvalidOperationException("ConnectionString is required");
```

### 4. Use Strongly Typed Models

```csharp
// Preferred - strongly typed
var config = YamlDeserializer.Deserialize<AppConfig>(yaml);

// Avoid - dynamic/dictionary when possible
var dict = YamlDeserializer.Deserialize<Dictionary<string, object>>(yaml);
```

## API Reference

### YamlSerializer

```csharp
// Static methods
static string Serialize(object? obj, YamlSerializerSettings? settings = null)

// Instance methods
YamlSerializer(YamlSerializerSettings? settings = null)
string Serialize(object? obj)
```

### YamlDeserializer

```csharp
// Static methods
static T Deserialize<T>(string input, YamlDeserializerSettings? settings = null, 
    IEnumerable<IYamlTypeConverter>? extra = null)
static object? Deserialize(string input, Type t, ...)
static T Deserialize<T>(TextReader reader, ...)
static object? Deserialize(TextReader reader, Type t, ...)

// Instance methods
YamlDeserializer(YamlDeserializerSettings? settings = null, 
    IEnumerable<IYamlTypeConverter>? extra = null)
T Deserialize<T>(string input)
T Deserialize<T>(TextReader reader)
object? Deserialize(string input, Type t)
object? Deserialize(TextReader reader, Type t)
```

## Error Handling

The library throws `YamlException` for parsing and conversion errors:

```csharp
try
{
    var result = YamlDeserializer.Deserialize<Config>(invalidYaml);
}
catch (YamlException ex)
{
    Console.WriteLine($"YAML Error at line {ex.Line}, column {ex.Column}: {ex.Message}");
}
```

## Limitations

While Cortex.Serialization.Yaml supports many YAML features, some advanced capabilities are intentionally simplified:

- **Multi-document streams**: Only single documents are supported
- **Binary data**: Not directly supported (use base64 encoding)
- **Complex keys**: Only scalar keys are supported in mappings
- **Circular references**: Not detected (may cause stack overflow)

For these advanced scenarios, consider using a full YAML 1.2 compliant library.

---

## License

This library is part of the Cortex framework. See the main repository for license information.
