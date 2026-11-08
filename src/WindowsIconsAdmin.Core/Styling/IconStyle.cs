using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsIconsAdmin.Core.Styling;

public sealed record IconStyle(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("container")] ContainerSpec Container,
    [property: JsonPropertyName("treatment")] TreatmentSpec Treatment)
{
    public const int CurrentSchemaVersion = 1;

    public static IconStyle CreateDefault(string name = "Soft Lavender") => new(
        SchemaVersion: CurrentSchemaVersion,
        Id: "style-soft-lavender",
        Name: name,
        Container: new ContainerSpec(
            shape: ContainerShape.Squircle,
            backgroundColor: "#E6DBFA",
            padding: 0.16),
        Treatment: new TreatmentSpec(
            saturation: -0.15,
            brightness: 0.05,
            contrast: 0.0,
            tintColor: null)
    );

    public static IReadOnlyList<IconStyle> GetCuratedPresets() =>
    [
        new(
            SchemaVersion: CurrentSchemaVersion,
            Id: "style-dark-slate",
            Name: "Dark Slate (Squircle)",
            Container: new ContainerSpec(shape: ContainerShape.Squircle, backgroundColor: "#202024", padding: 0.16),
            Treatment: new TreatmentSpec(saturation: 0.0, brightness: 0.0, contrast: 0.0)
        ),
        new(
            SchemaVersion: CurrentSchemaVersion,
            Id: "style-frosted-pearl",
            Name: "Frosted Pearl (Squircle)",
            Container: new ContainerSpec(shape: ContainerShape.Squircle, backgroundColor: "#F7F7F8", padding: 0.16),
            Treatment: new TreatmentSpec(saturation: 0.0, brightness: 0.0, contrast: 0.0)
        ),
        new(
            SchemaVersion: CurrentSchemaVersion,
            Id: "style-nordic-slate",
            Name: "Nordic Slate (Squircle)",
            Container: new ContainerSpec(shape: ContainerShape.Squircle, backgroundColor: "#252B35", padding: 0.16),
            Treatment: new TreatmentSpec(saturation: 0.0, brightness: 0.0, contrast: 0.0)
        ),
        new(
            SchemaVersion: CurrentSchemaVersion,
            Id: "style-deep-midnight",
            Name: "Deep Midnight (Squircle)",
            Container: new ContainerSpec(shape: ContainerShape.Squircle, backgroundColor: "#151922", padding: 0.16),
            Treatment: new TreatmentSpec(saturation: 0.0, brightness: 0.0, contrast: 0.0)
        ),
        new(
            SchemaVersion: CurrentSchemaVersion,
            Id: "style-soft-lavender",
            Name: "Soft Lavender (Squircle)",
            Container: new ContainerSpec(shape: ContainerShape.Squircle, backgroundColor: "#E6DBFA", padding: 0.16),
            Treatment: new TreatmentSpec(saturation: -0.15, brightness: 0.05, contrast: 0.0)
        )
    ];

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, StyleJsonOptions);
    }

    public static IconStyle FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var style = JsonSerializer.Deserialize<IconStyle>(json, StyleJsonOptions);
        if (style is null)
        {
            throw new JsonException("Failed to deserialize IconStyle.");
        }
        return style;
    }

    private static readonly JsonSerializerOptions StyleJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
