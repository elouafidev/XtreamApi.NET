using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Converts a Unix timestamp in seconds into a UTC <see cref="DateTimeOffset"/>.
/// <para>
/// Xtream returns these fields (<c>exp_date</c>, <c>added</c>,
/// <c>created_at</c>, <c>start_timestamp</c>) sometimes as a number, sometimes
/// as a string, and <c>null</c> legitimately means "no date" (an unlimited
/// subscription). The value is always in seconds, never milliseconds.
/// </para>
/// <para>
/// To be applied per property through <c>[JsonConverter]</c>: the target type
/// <c>DateTimeOffset?</c> is shared with
/// <see cref="LooseDateTimeOffsetConverter"/>, so a global registration would be
/// ambiguous.
/// </para>
/// </summary>
public sealed class UnixTimestampConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var seconds = TolerantNumber.ReadInt64(ref reader);
        if (seconds is null or 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Some panels return nonsense values (0, -1, 9999999999999).
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value.ToUnixTimeSeconds());
        }
    }
}

/// <summary>
/// Converts Xtream's textual dates, which are not ISO 8601.
/// <para>
/// You meet <c>"2026-01-01 20:30:00"</c> (EPG, <c>time_now</c>),
/// <c>"2026-01-01"</c> (<c>releasedate</c>), the empty string, and
/// <c>"0000-00-00"</c> inherited from MySQL. The standard System.Text.Json
/// reader rejects every one of these forms.
/// </para>
/// <para>
/// Important: these dates are expressed in the server's time zone, never in
/// UTC. When an equivalent <c>*_timestamp</c> field exists, prefer it.
/// </para>
/// </summary>
public sealed class LooseDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd",
        "dd-MM-yyyy",
    ];

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            // A few panels return an epoch where the others return text.
            var seconds = TolerantNumber.ReadInt64(ref reader);
            return seconds is null or 0 ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith("0000-00-00", StringComparison.Ordinal))
        {
            return null;
        }

        text = text.Trim();

        if (DateTimeOffset.TryParseExact(
                text,
                Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
        {
            return exact;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var loose)
            ? loose
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }
}
