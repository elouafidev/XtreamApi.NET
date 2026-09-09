using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using XtreamApi.Serialization;

namespace XtreamApi.Http;

/// <summary>
/// Default HTTP transport.
/// </summary>
public sealed class XtreamHttpTransport : IXtreamTransport
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly XtreamClientOptions _options;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<XtreamProgress>? Progress;

    /// <summary>
    /// Creates a transport owning its own <see cref="HttpClient"/>, configured
    /// from <paramref name="options"/>.
    /// </summary>
    public XtreamHttpTransport(XtreamClientOptions? options = null)
    {
        _options = options ?? new XtreamClientOptions();
        _httpClient = XtreamHttpClientFactory.Create(_options);
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Creates a transport on top of a supplied <see cref="HttpClient"/> whose
    /// lifetime remains the caller's responsibility.
    /// <para>
    /// Intended for use with <c>IHttpClientFactory</c>. The client must have an
    /// infinite <see cref="HttpClient.Timeout"/>: timeouts are handled here, per
    /// phase, so that reading large responses is never cut short.
    /// </para>
    /// </summary>
    public XtreamHttpTransport(HttpClient httpClient, XtreamClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _options = options ?? new XtreamClientOptions();
        _ownsHttpClient = false;
    }

    /// <inheritdoc />
    public async Task<T?> GetJsonAsync<T>(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default)
    {
        var stream = await OpenValidatedStreamAsync(credentials, request, cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            var measured = new ProgressStream(stream, stream.TotalBytes, RaiseProgress);

            try
            {
                return await JsonSerializer
                    .DeserializeAsync<T>(measured, XtreamJson.Default, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new XtreamProtocolException(
                    "The panel's response is not usable JSON.",
                    stream.Snippet,
                    exception);
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> StreamJsonArrayAsync<T>(
        XtreamCredentials credentials,
        XtreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = await OpenValidatedStreamAsync(credentials, request, cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            var measured = new ProgressStream(stream, stream.TotalBytes, RaiseProgress);

            var items = JsonSerializer
                .DeserializeAsyncEnumerable<T>(measured, XtreamJson.Default, cancellationToken)
                .ConfigureAwait(false);

            await foreach (var item in items)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> GetTextAsync(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(credentials, request, cancellationToken).ConfigureAwait(false);

        using (response)
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(credentials, request, cancellationToken).ConfigureAwait(false);

        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new PeekedResponseStream([], 0, stream, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Opens the response and checks it has the expected shape before handing
    /// it to the deserialiser.
    /// </summary>
    private async Task<PeekedResponseStream> OpenValidatedStreamAsync(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(credentials, request, cancellationToken).ConfigureAwait(false);

        PeekedResponseStream peeked;

        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var buffer = new byte[_options.ErrorSnippetLength];
            var read = await stream
                .ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken)
                .ConfigureAwait(false);

            peeked = new PeekedResponseStream(
                buffer,
                read,
                stream,
                response,
                response.Content.Headers.ContentLength);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        try
        {
            EnsureLooksLikeJson(peeked, request);
            return peeked;
        }
        catch
        {
            await peeked.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Rejects early the responses that plainly are not JSON.
    /// <para>
    /// A failing panel routinely answers 200 with an HTML page, XML, or a plain
    /// "Not Found". Letting the deserialiser choke on that produces a useless
    /// message; stopping here allows the caller to be shown what the server
    /// actually said.
    /// </para>
    /// </summary>
    private static void EnsureLooksLikeJson(PeekedResponseStream stream, XtreamRequest request)
    {
        if (!request.ExpectsJson)
        {
            return;
        }

        var snippet = stream.Snippet;

        if (snippet.Length == 0)
        {
            throw new XtreamProtocolException("The panel returned an empty response.");
        }

        if (snippet[0] is '{' or '[')
        {
            return;
        }

        var kind = snippet[0] == '<' ? "an HTML page or XML" : "text";

        throw new XtreamProtocolException(
            $"The panel returned {kind} instead of the expected JSON.",
            snippet);
    }

    /// <summary>
    /// Issues the request, retrying transient failures only.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var uri = XtreamUrlBuilder.Build(credentials, request);
        var redacted = XtreamUrlBuilder.BuildRedacted(credentials, request);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response;

            try
            {
                response = await SendOnceAsync(uri, redacted, cancellationToken).ConfigureAwait(false);
            }
            catch (XtreamConnectionException) when (attempt < _options.RetryCount)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var statusCode = response.StatusCode;
            response.Dispose();

            if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Retrying would change nothing, and hammering a panel that
                // refuses gets the address banned at some providers.
                throw new XtreamAuthenticationException(
                    $"The panel rejected the credentials (status {(int)statusCode}). "
                    + "Check the username, the password, and the User-Agent the provider expects.",
                    statusCode);
            }

            var httpException = new XtreamHttpException(
                statusCode,
                $"The panel answered with status {(int)statusCode} ({statusCode}) for {redacted}.");

            if (httpException.IsTransient && attempt < _options.RetryCount)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw httpException;
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Uri uri,
        Uri redacted,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);

        // The linked token bounds only the wait for headers. It is disposed as
        // soon as they arrive, so its timer cannot interrupt reading the body,
        // which may legitimately take several minutes.
        using var headersCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        headersCancellation.CancelAfter(_options.ResponseHeadersTimeout);

        try
        {
            return await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, headersCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            var seconds = _options.ResponseHeadersTimeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);

            throw new XtreamConnectionException(
                redacted,
                $"The panel did not answer within {seconds} s.",
                exception)
            {
                IsTimeout = true,
            };
        }
        catch (HttpRequestException exception)
        {
            throw new XtreamConnectionException(
                redacted,
                $"Could not reach the panel: {exception.Message}",
                exception);
        }
    }

    private void RaiseProgress(XtreamProgress progress) => Progress?.Invoke(this, progress);

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = _options.RetryBaseDelay * Math.Pow(2, attempt);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
}
