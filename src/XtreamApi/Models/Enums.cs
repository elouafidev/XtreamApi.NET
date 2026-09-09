namespace XtreamApi.Models;

/// <summary>
/// Subscription state as reported by the panel.
/// </summary>
public enum AccountStatus
{
    /// <summary>The panel returned an empty or unrecognised value.</summary>
    Unknown = 0,

    Active,
    Expired,
    Disabled,
    Banned,
}

/// <summary>
/// Kind of stream, which determines the playback URL segment
/// (<c>/live/</c>, <c>/movie/</c> or <c>/series/</c>).
/// </summary>
public enum StreamKind
{
    Live = 0,
    Movie,
    Series,
}
