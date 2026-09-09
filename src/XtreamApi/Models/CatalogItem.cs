using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// Fields shared by every catalogue item: channels, movies and series.
/// </summary>
public abstract class CatalogItem
{
    /// <summary>Display order imposed by the provider.</summary>
    [JsonPropertyName("num")]
    public int Num { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Primary category. <c>null</c> on some panels.</summary>
    [JsonPropertyName("category_id")]
    public int? CategoryId { get; init; }

    /// <summary>
    /// Multiple categories, on recent panels only. Empty elsewhere.
    /// </summary>
    [JsonPropertyName("category_ids")]
    [JsonConverter(typeof(FlexibleInt32ListConverter))]
    public IReadOnlyList<int> CategoryIds { get; init; } = [];

    [JsonPropertyName("added")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? AddedAt { get; init; }

    [JsonPropertyName("custom_sid")]
    public string? CustomSid { get; init; }

    /// <summary>
    /// Absolute URL imposed by the provider. When present it supersedes the URL
    /// that would otherwise be built from the credentials.
    /// </summary>
    [JsonPropertyName("direct_source")]
    public string? DirectSource { get; init; }

    /// <summary>Kind of stream, which determines the playback URL segment.</summary>
    [JsonIgnore]
    public abstract StreamKind Kind { get; }

    /// <summary>Identifier used in the playback URL.</summary>
    [JsonIgnore]
    public abstract int Id { get; }

    /// <summary>Every category the item belongs to, without duplicates.</summary>
    [JsonIgnore]
    public IReadOnlyList<int> AllCategoryIds =>
        CategoryIds.Count > 0
            ? CategoryIds
            : CategoryId is null ? [] : [CategoryId.Value];
}
