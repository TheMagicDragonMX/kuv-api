using System.Text.Json;

namespace kuv_api.Models;

/// <summary>
/// The one message shape spoken on the cube sockets, in both directions: a client sends commands to a cube, a cube
/// sends events to its clients, and the server sends its own notices (<c>cube_status</c>, <c>error</c>).
/// </summary>
/// <param name="Name">
/// What the message is, e.g. <c>message</c>, <c>reply</c>, <c>cube_status</c> or <c>error</c>.
/// </param>
/// <param name="Id">
/// Identifier of this message. The server fills it in when a client does not send one.
/// </param>
/// <param name="ReplyTo">
/// Id of the message this one answers, or null when it does not answer any.
/// </param>
/// <param name="Payload">
/// The data of the message, whose shape depends on <paramref name="Name" />, or null when it has none.
/// </param>
public sealed record CubeMessage(string Name, string? Id = null, string? ReplyTo = null, JsonElement? Payload = null);
