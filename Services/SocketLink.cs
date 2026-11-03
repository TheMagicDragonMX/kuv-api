using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using kuv_api.Models;

namespace kuv_api.Services;

/// <summary>
/// One open WebSocket that speaks <see cref="CubeMessage" />s. It serializes the sends, because a socket accepts only
/// one at a time.
/// </summary>
public sealed class SocketLink(WebSocket socket)
{
    /// <summary>
    /// Largest message accepted, in bytes. Bigger ones close the socket.
    /// </summary>
    private const int MaxMessageBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly WebSocket socket = socket;

    private readonly SemaphoreSlim sendLock = new(1, 1);

    /// <summary>
    /// Sends a message, unless the socket is no longer open.
    /// </summary>
    /// <param name="message">
    /// Message to send.
    /// </param>
    /// <returns>
    /// True if it was sent, false if the socket is closed or broke while sending.
    /// </returns>
    public async Task<bool> Send(CubeMessage message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await sendLock.WaitAsync();

        try
        {
            if (socket.State != WebSocketState.Open)
            {
                return false;
            }

            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

            return true;
        }
        catch (WebSocketException)
        {
            return false;
        }
        finally
        {
            sendLock.Release();
        }
    }

    /// <summary>
    /// Reads messages until the peer closes or drops, calling <paramref name="onMessage" /> for each valid one and
    /// <paramref name="onInvalid" /> for each one that is not a message.
    /// </summary>
    /// <param name="onMessage">
    /// Called with every message received.
    /// </param>
    /// <param name="onInvalid">
    /// Called with the reason when a received frame is not a valid message.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops reading, e.g. when the server shuts down.
    /// </param>
    public async Task Receive(Func<CubeMessage, Task> onMessage, Func<string, Task> onInvalid, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                using var frame = new MemoryStream();
                ValueWebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await Close(WebSocketCloseStatus.NormalClosure, "bye");

                        return;
                    }

                    frame.Write(buffer, 0, result.Count);

                    if (frame.Length > MaxMessageBytes)
                    {
                        await Close(WebSocketCloseStatus.MessageTooBig, "Message too big.");

                        return;
                    }
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await onInvalid("Only text messages are accepted.");

                    continue;
                }

                await Dispatch(Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length), onMessage, onInvalid);
            }
        }
        catch (WebSocketException)
        {
            // The peer dropped without closing: the same as closing.
        }
        catch (OperationCanceledException)
        {
            await Close(WebSocketCloseStatus.EndpointUnavailable, "Server stopping.");
        }
    }

    /// <summary>
    /// Closes the socket, if it is still open.
    /// </summary>
    /// <param name="status">
    /// Why it closes.
    /// </param>
    /// <param name="description">
    /// Short text for the peer.
    /// </param>
    public async Task Close(WebSocketCloseStatus status, string description)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await socket.CloseOutputAsync(status, description, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // Already gone.
        }
    }

    private static async Task Dispatch(string text, Func<CubeMessage, Task> onMessage, Func<string, Task> onInvalid)
    {
        CubeMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<CubeMessage>(text, JsonOptions);
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            await onInvalid("The message must be a JSON object with a name.");

            return;
        }

        await onMessage(message);
    }
}
