namespace XtreamApi.Http;

/// <summary>
/// Progress of a response being received.
/// </summary>
/// <param name="BytesReceived">Bytes read so far.</param>
/// <param name="TotalBytes">
/// Size announced by the server, or <c>null</c> when it announces none.
/// <para>
/// Two cases leave it unknown. The server may reply in chunks, with no size
/// declared up front. And when it compresses the response, .NET drops the
/// length header while decompressing, because the announced value covers the
/// compressed bytes and would not match what is read.
/// </para>
/// </param>
public readonly record struct XtreamProgress(long BytesReceived, long? TotalBytes)
{
    /// <summary>
    /// Fraction received, from 0 to 1, or <c>null</c> when the total size is
    /// unknown. Clamped to 1: a full bar beats a nonsensical percentage when the
    /// server announces a wrong size.
    /// </summary>
    public double? Fraction =>
        TotalBytes is > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1) : null;

    /// <summary>Percentage received, or <c>null</c> when the total size is unknown.</summary>
    public double? Percentage => Fraction * 100;

    /// <summary>Progress can be expressed as a percentage.</summary>
    public bool IsMeasurable => TotalBytes is > 0;
}
