using System.Text.Json.Serialization;

namespace HiveMind.Core.Journal;

/// <summary>
/// Source-generated JSON for journal bodies: camelCase, nulls omitted, no reflection.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JournalBody))]
internal sealed partial class JournalJsonContext : JsonSerializerContext
{
}
