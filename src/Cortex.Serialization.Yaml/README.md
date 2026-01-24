# Cortex.Serialization.Yaml 🧠

**Cortex.Serialization.Yaml** A lightweight, dependency‑free YAML serializer/deserializer for .NET 8+.

Built as part of the [Cortex Data Framework](https://github.com/buildersoftio/cortex), this library simplifies serializer/deserializer for YAML:


- ✅ **Serialize & Deserialize** POCOs, collections, and dictionaries
- ✅ **Naming conventions**: CamelCase, PascalCase, SnakeCase, KebabCase, Original
- ✅ **Attributes** `[YamlProperty(Name=…)]`, `[YamlIgnore]`
- ✅ **Custom type converters** via `IYamlTypeConverter` (primitive/date/guid built‑ins included)
- ✅ **Settings**: indentation, emit nulls/defaults, sort properties, case‑insensitive matching

This version doesnot include: flow style ([], {}), comments preservation, anchors/aliases & merge keys, custom tags, streaming APIs

---

[![GitHub License](https://img.shields.io/github/license/buildersoftio/cortex)](https://github.com/buildersoftio/cortex/blob/master/LICENSE)
[![NuGet Version](https://img.shields.io/nuget/v/Cortex.Serialization.Yaml?label=Cortex.Serialization.Yaml)](https://www.nuget.org/packages/Cortex.Serialization.Yaml)
[![GitHub contributors](https://img.shields.io/github/contributors/buildersoftio/cortex)](https://github.com/buildersoftio/cortex)
[![Discord Shield](https://discord.com/api/guilds/1310034212371566612/widget.png?style=shield)](https://discord.gg/JnMJV33QHu)


## 🚀 Getting Started

### Install via NuGet

```bash
dotnet add package Cortex.Serialization.Yaml
```

## 🛠️ Quick Start
```csharp
using Cortex.Serialization.Yaml.Serialization;
using Cortex.Serialization.Yaml.Serialization.Conventions;

public sealed record Address(string Street, string City);
public sealed class Person
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public int Age { get; set; }
    public List<string> Tags { get; set; } = new();
    public Address? Address { get; set; }
}

var person = new Person
{
    FirstName = "Ada",
    LastName  = "Lovelace",
    Age = 36,
    Tags = ["math", "poet"],
    Address = new("12 St James's Sq", "London")
};

var serializer = new YamlSerializer(new YamlSerializerSettings
{
    NamingConvention = new SnakeCaseConvention(),
    EmitNulls = false
});

string yaml = serializer.Serialize(person);
Console.WriteLine(yaml);

var deserializer = new YamlDeserializer(new YamlDeserializerSettings
{
    NamingConvention = new SnakeCaseConvention()
});

var model = deserializer.Deserialize<Person>(yaml);
Console.WriteLine($"Hello {model.FirstName} {model.LastName}, {model.Age}");
```

## 🔧 Configuration & Options

### Serializer settings
```csharp
var settings = new YamlSerializerSettings
{
    NamingConvention = new CamelCaseConvention(), // how CLR names map to YAML keys
    EmitNulls = true,                             // include null properties
    EmitDefaults = true,                          // include default(T) values
    SortProperties = false,                       // keep reflection order
    Indentation = 2                               // spaces per indent level
};
```

### Deserializer settings

```csharp
var settings = new YamlDeserializerSettings
{
    NamingConvention = new SnakeCaseConvention(),
    CaseInsensitive = true,
    IgnoreUnmatchedProperties = true
};
```

## 📚 Examples

### 1) Lists and nested objects

```csharp
var yaml = """
first_name: Ada
last_name: Lovelace
age: 36
tags:
  - math
  - poet
address:
  street: 12 St James's Sq
  city: London
""";

var des = new YamlDeserializer(new YamlDeserializerSettings { NamingConvention = new SnakeCaseConvention() });
var p = des.Deserialize<Person>(yaml);

var s = new YamlSerializer(new YamlSerializerSettings { NamingConvention = new SnakeCaseConvention(), EmitNulls = false });
var outYaml = s.Serialize(p);
```

### 2) Block scalars (| and >)

```yaml
description: |
  First line kept
  Second line kept
note: >
  Lines are folded
  into a single paragraph
```
These map to string properties on your CLR model.

### 3) Attributes and explicit names

```csharp
public sealed class Product
{
    [YamlProperty(Name = "product_id")] // explicit YAML key
    public Guid Id { get; set; }

    [YamlIgnore]
    public string? InternalNotes { get; set; }
}
```

### 4) Custom converters

```csharp
public sealed class YesNoBoolConverter : IYamlTypeConverter
{
    public bool CanConvert(Type t) => t == typeof(bool);
    public object? Read(object? node, Type targetType) => string.Equals(node?.ToString(), "yes", StringComparison.OrdinalIgnoreCase);
    public object? Write(object? value, Type declared) => (bool?)value == true ? "yes" : "no";
}

var s = new YamlSerializer(new YamlSerializerSettings());
s.Converters.Add(new YesNoBoolConverter());
```

## ⚠️ Limits (current version)
- No flow style (`[]`, `{}`) collections
- No **comments** preservation/round‑trip of `# …`
- No **anchors/aliases/merge keys**
- No **custom tags**
- **Pragmatic YAML subset**; quoting/escaping is intentionally simple


## 💬 Contributing
We welcome contributions from the community! Whether it's reporting bugs, suggesting features, or submitting pull requests, your involvement helps improve Cortex for everyone.

### 💬 How to Contribute
1. **Fork the Repository**
2. **Create a Feature Branch**
```bash
git checkout -b feature/YourFeature
```
3. **Commit Your Changes**
```bash
git commit -m "Add your feature"
```
4. **Push to Your Fork**
```bash
git push origin feature/YourFeature
```
5. **Open a Pull Request**

Describe your changes and submit the pull request for review.

## 📄 License
This project is licensed under the MIT License.

## 📚 Sponsorship
Cortex is an open-source project maintained by BuilderSoft. Your support helps us continue developing and improving Cortex. Consider sponsoring us to contribute to the future of resilient streaming platforms.

### How to Sponsor
* **Financial Contributions**: Support us through [GitHub Sponsors](https://github.com/sponsors/buildersoftio) or other preferred platforms.
* **Corporate Sponsorship**: If your organization is interested in sponsoring Cortex, please contact us directly.

Contact Us: cortex@buildersoft.io


## Contact
We'd love to hear from you! Whether you have questions, feedback, or need support, feel free to reach out.

- Email: cortex@buildersoft.io
- Website: https://buildersoft.io
- GitHub Issues: [Cortex Data Framework Issues](https://github.com/buildersoftio/cortex/issues)
- Join our Discord Community: [![Discord Shield](https://discord.com/api/guilds/1310034212371566612/widget.png?style=shield)](https://discord.gg/JnMJV33QHu)


Thank you for using Cortex Data Framework! We hope it empowers you to build scalable and efficient data processing pipelines effortlessly.

Built with ❤️ by the Buildersoft team.
