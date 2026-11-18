using System.Net;
using System.Net.Http.Headers;

namespace kuv_api.Services;

/// <summary>
/// A read-only stream over a file on the file server. It can seek, which is what lets a video be served in ranges: it
/// asks the server for the bytes from where it is, and a seek only drops the download in progress, so nothing is
/// fetched twice and nothing is fetched that is never read.
/// </summary>
/// <param name="http">
/// The client that reaches the file server.
/// </param>
/// <param name="address">
/// The address of the file, relative to the client.
/// </param>
/// <param name="length">
/// Size of the file, in bytes.
/// </param>
public sealed class HttpRangeStream(HttpClient http, string address, long length) : Stream
{
    /// <summary>
    /// Where the next read starts.
    /// </summary>
    private long position;

    /// <summary>
    /// The answer being read, or null before the first read and after a seek.
    /// </summary>
    private HttpResponseMessage? response;

    /// <summary>
    /// The body of <see cref="response"/>.
    /// </summary>
    private Stream? body;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => length;

    /// <inheritdoc />
    public override long Position
    {
        get => position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (position >= length || buffer.IsEmpty)
        {
            return 0;
        }

        body ??= await Download(cancellationToken);

        var read = await body.ReadAsync(buffer, cancellationToken);
        position += read;

        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <summary>
    /// Reading without waiting is not supported: a download must never block a thread of the server. Callers copy the
    /// stream asynchronously instead.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Always.
    /// </exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Read this stream with ReadAsync.");
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            _ => length + offset
        };

        if (target < 0)
        {
            throw new IOException("Cannot seek before the start of the file.");
        }

        // Only a real move drops the download: a seek to where it already is changes nothing
        if (target != position)
        {
            Release();
            position = target;
        }

        return position;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Release();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Asks the server for the file from the current position on.
    /// </summary>
    private async Task<Stream> Download(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Range = new RangeHeaderValue(position, null);

        response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // A server that ignores the range sends the whole file (200), which is only right when reading from the start
        var understood = HttpStatusCode.PartialContent == response.StatusCode ||
            (HttpStatusCode.OK == response.StatusCode && 0 == position);

        if (!understood)
        {
            var status = (int)response.StatusCode;
            Release();

            throw new IOException($"The file server answered {status} to a range request.");
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <summary>
    /// Drops the download in progress, if any.
    /// </summary>
    private void Release()
    {
        body?.Dispose();
        response?.Dispose();
        body = null;
        response = null;
    }
}
