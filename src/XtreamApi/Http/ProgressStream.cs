namespace XtreamApi.Http;

/// <summary>
/// Counts the bytes read and reports them as they go.
/// <para>
/// The response is not read by the transport but by the deserialiser, which
/// pulls from the stream at its own pace. Measuring progress therefore means
/// sitting inside the stream itself.
/// </para>
/// </summary>
internal sealed class ProgressStream : Stream
{
    /// <summary>
    /// How much is read between two reports. Reporting every block would drown
    /// the subscriber under thousands of events on a catalogue of several tens
    /// of megabytes, for an imperceptible movement of the bar.
    /// </summary>
    private const long ReportThreshold = 256 * 1024;

    private readonly Stream _inner;
    private readonly long? _totalBytes;
    private readonly Action<XtreamProgress> _report;

    private long _received;
    private long _lastReported;
    private bool _completed;
    private bool _disposed;

    internal ProgressStream(Stream inner, long? totalBytes, Action<XtreamProgress> report)
    {
        _inner = inner;
        _totalBytes = totalBytes;
        _report = report;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        Advance(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Advance(read);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Accounts for a block that was read and reports when there is enough to
    /// report.
    /// <para>
    /// The end of the stream always produces a report, so the subscriber gets a
    /// final complete state even when the remainder was tiny.
    /// </para>
    /// </summary>
    private void Advance(int read)
    {
        if (read <= 0)
        {
            if (!_completed)
            {
                _completed = true;
                Report();
            }

            return;
        }

        _received += read;

        if (_received - _lastReported >= ReportThreshold)
        {
            Report();
        }
    }

    private void Report()
    {
        _lastReported = _received;

        try
        {
            _report(new XtreamProgress(_received, _totalBytes));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A faulty subscriber must not interrupt reading the response:
            // progress is a convenience, not the purpose.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
