using System.Text;

namespace XtreamApi.Http;

/// <summary>
/// A response stream whose first bytes have already been read.
/// <para>
/// The transport inspects the leading bytes to tell a genuine JSON response
/// from an HTML error page -- panels very often return 200 with HTML in the
/// body. Those bytes cannot be pushed back into the network stream, so this
/// class replays them first and then continues on the original stream.
/// </para>
/// <para>
/// It also owns the lifetime of the HTTP response: disposing the stream
/// disposes the response, which returns the connection to the pool.
/// </para>
/// </summary>
internal sealed class PeekedResponseStream : Stream
{
    private readonly byte[] _prefix;
    private readonly int _prefixLength;
    private readonly Stream _inner;
    private readonly IDisposable? _owner;
    private int _prefixPosition;
    private bool _disposed;

    internal PeekedResponseStream(
        byte[] prefix,
        int prefixLength,
        Stream inner,
        IDisposable? owner,
        long? totalBytes = null)
    {
        _prefix = prefix;
        _prefixLength = prefixLength;
        _inner = inner;
        _owner = owner;
        TotalBytes = totalBytes;
    }

    /// <summary>
    /// Size announced by the server, <c>null</c> when it announces none.
    /// </summary>
    internal long? TotalBytes { get; }

    /// <summary>
    /// Start of the response decoded as text, for error messages.
    /// </summary>
    internal string Snippet => Encoding.UTF8.GetString(_prefix, 0, _prefixLength).Trim();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var fromPrefix = CopyFromPrefix(buffer);
        return fromPrefix > 0 ? fromPrefix : _inner.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var fromPrefix = CopyFromPrefix(buffer.Span);
        return fromPrefix > 0
            ? ValueTask.FromResult(fromPrefix)
            : _inner.ReadAsync(buffer, cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Serves the already-read bytes first. Returns 0 once the prefix is
    /// exhausted, which switches reading over to the original stream.
    /// </summary>
    private int CopyFromPrefix(Span<byte> buffer)
    {
        var remaining = _prefixLength - _prefixPosition;
        if (remaining <= 0 || buffer.IsEmpty)
        {
            return 0;
        }

        var count = Math.Min(remaining, buffer.Length);
        _prefix.AsSpan(_prefixPosition, count).CopyTo(buffer);
        _prefixPosition += count;
        return count;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            _inner.Dispose();
            _owner?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _inner.DisposeAsync().ConfigureAwait(false);
            _owner?.Dispose();
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
