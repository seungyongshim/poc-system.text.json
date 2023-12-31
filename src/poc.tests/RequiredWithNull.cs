using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using RreparedMinimalApiJsonSerializerOptions;


namespace poc.tests;


public record PersonalInfo
{
    public required string Name { get; init; } = "Hmm";
    public string? FullName { get; init; } = "Hello World";
}

public class RequiredWithNull
{
    [Fact]
    public void problem_required_null()
    {
        var raw = """
        {
            "Name": null,
            "FullName": null
        }
        """;

        var sut = JsonSerializer.Deserialize<PersonalInfo>(raw);

    }

    [Fact]
    public void solved_required_null()
    {
        var raw = """
        {
            "Name": null,
            "FullName": null
        }
        """;

        var sut = JsonSerializer.Deserialize<PersonalInfo>(raw, MinimalApiJsonSerializerOptions.Default);
    }

   
}
