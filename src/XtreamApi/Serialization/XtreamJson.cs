using System.Text.Json;
using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Serialization;

/// <summary>
/// Serialisation options shared by every Xtream call.
/// <para>
/// All the format tolerance lives here. Deserialising an Xtream response with
/// the default System.Text.Json options fails on the very first numeric field
/// returned as a string, which happens on just about every panel.
/// </para>
/// </summary>
public static class XtreamJson
{
    /// <summary>
    /// Shared, read-only instance. Use it by default: building options on every
    /// call defeats the System.Text.Json metadata cache and is costly on large
    /// responses.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateReadOnly();

    /// <summary>
    /// Creates a mutable set of options, for the cases where a panel requires a
    /// particular setting.
    /// </summary>
    public static JsonSerializerOptions Create() => new()
    {
        // Panels alternate between snake_case, camelCase and whimsical
        // capitalisation on the very same fields.
        PropertyNameCaseInsensitive = true,

        // Safety net: the tolerant converters already cover numbers returned as
        // strings, but this setting protects the types not handled explicitly.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,

        // Some panels append content after the JSON, or leave trailing commas
        // from hand-rolled generation.
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,

        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        Converters =
        {
            new TolerantInt32Converter(),
            new TolerantNullableInt32Converter(),
            new TolerantInt64Converter(),
            new TolerantNullableInt64Converter(),
            new TolerantDoubleConverter(),
            new TolerantNullableDoubleConverter(),
            new TolerantBooleanConverter(),
            new TolerantNullableBooleanConverter(),
            new TolerantStringConverter(),
        },
    };

    private static JsonSerializerOptions CreateReadOnly()
    {
        var options = Create();

        // populateMissingResolver installs the reflection resolver: without it,
        // freezing the options throws on the very first call.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
