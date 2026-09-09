using System.Text.Json.Serialization;

namespace XtreamApi.Models;

/// <summary>
/// A category. The shape is identical for <c>get_live_categories</c>,
/// <c>get_vod_categories</c> and <c>get_series_categories</c>.
/// </summary>
public sealed class XtreamCategory
{
    [JsonPropertyName("category_id")]
    public int Id { get; init; }

    [JsonPropertyName("category_name")]
    public string? Name { get; init; }

    /// <summary>Parent category, 0 when the category sits at the root.</summary>
    [JsonPropertyName("parent_id")]
    public int ParentId { get; init; }
}
