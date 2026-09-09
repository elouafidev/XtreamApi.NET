using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Decodes the text fields that Xtream encodes in Base64.
/// <para>
/// The <c>title</c> and <c>description</c> of EPG entries are Base64-encoded by
/// the panel. Without decoding, the interface shows gibberish.
/// </para>
/// <para>
/// Not every panel does it: some return plain text, and a plain title can
/// accidentally look like Base64 (<c>"Info"</c>, for instance). The value is
/// therefore replaced only when decoding yields valid UTF-8 free of control
/// characters; otherwise the original text is returned untouched.
/// </para>
/// </summary>
public sealed class Base64TextConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            if (reader.TokenType != JsonTokenType.Null)
            {
                reader.Skip();
            }

            return null;
        }

        var raw = reader.GetString();
        return string.IsNullOrEmpty(raw) ? raw : TryDecode(raw) ?? raw;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
        }
    }

    /// <summary>
    /// Returns the decoded text, or <c>null</c> when the input is not Base64
    /// carrying readable text.
    /// </summary>
    internal static string? TryDecode(string candidate)
    {
        // Base64 is aligned on four characters. This check rules out the vast
        // majority of plain titles outright.
        if (candidate.Length < 4 || candidate.Length % 4 != 0)
        {
            return null;
        }

        var buffer = new byte[candidate.Length * 3 / 4];
        if (!Convert.TryFromBase64String(candidate, buffer, out var written) || written == 0)
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(buffer, 0, written);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        foreach (var character in decoded)
        {
            // Text meant for display contains no control bytes. If any appear,
            // the input was binary rather than text: the "Base64" was not.
            if (char.IsControl(character) && character is not ('\r' or '\n' or '\t'))
            {
                return null;
            }
        }

        return decoded;
    }
}
