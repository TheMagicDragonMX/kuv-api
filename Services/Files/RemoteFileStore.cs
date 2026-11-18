using System.Net;
using kuv_api.Models.Files;

namespace kuv_api.Services;

/// <summary>
/// Keeps the files on the server, so an API running anywhere else (a laptop, for example) reads and writes the same
/// files as the one on the server. It talks to the WebDAV folder nginx shares at <c>/files/</c>: <c>PUT</c> writes,
/// <c>GET</c> reads and <c>DELETE</c> deletes. The <see cref="HttpClient"/> comes with the address and the password
/// already set.
/// </summary>
/// <param name="http">
/// The client, whose <see cref="HttpClient.BaseAddress"/> ends in <c>/files/</c>.
/// </param>
public sealed class RemoteFileStore(HttpClient http) : IFileStore
{
    /// <inheritdoc />
    public async Task Write(string key, Stream content)
    {
        using var body = new StreamContent(content);
        using var response = await http.PutAsync(AddressOf(key), body);

        EnsureSuccess(response, "write", key);
    }

    /// <inheritdoc />
    public async Task<StoredFile?> Open(string key)
    {
        var head = await Head(key);

        if (head is null)
        {
            return null;
        }

        // The content is fetched while it is read, from where it is read, so seeking in a video does not download it all
        var stream = new HttpRangeStream(http, AddressOf(key), head.Value.Length);

        return new StoredFile(stream, head.Value.Length, head.Value.LastModified);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> LastModified(string key)
    {
        return (await Head(key))?.LastModified;
    }

    /// <inheritdoc />
    public async Task Delete(string key)
    {
        using var response = await http.DeleteAsync(AddressOf(key));

        EnsureSuccess(response, "delete", key);
    }

    /// <inheritdoc />
    public async Task DeleteFolder(string key)
    {
        // A trailing slash is how WebDAV says "the folder"
        using var response = await http.DeleteAsync(AddressOf(key) + "/");

        EnsureSuccess(response, "delete", key);
    }

    /// <summary>
    /// Asks the server about a file without downloading it.
    /// </summary>
    /// <returns>
    /// Its size and last change, or null when the server does not have it.
    /// </returns>
    private async Task<(long Length, DateTimeOffset LastModified)?> Head(string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, AddressOf(key));
        using var response = await http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, "read", key);

        return (
            response.Content.Headers.ContentLength ?? 0,
            response.Content.Headers.LastModified ?? DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// The address of a key, relative to the folder the client points at.
    /// </summary>
    private static string AddressOf(string key)
    {
        return string.Join('/', FileKeys.Check(key).Split('/').Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Stops with a clear message when the server did not do what was asked. Deleting what is already gone is fine.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The server answered with an error. The exception handler logs it and answers 500 without the details.
    /// </exception>
    private static void EnsureSuccess(HttpResponseMessage response, string action, string key)
    {
        var alreadyGone = "delete" == action && HttpStatusCode.NotFound == response.StatusCode;

        if (!response.IsSuccessStatusCode && !alreadyGone)
        {
            throw new InvalidOperationException($"The file server refused to {action} '{key}': {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }
}
