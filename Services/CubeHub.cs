using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using kuv_api.Models;

namespace kuv_api.Services;

/// <summary>
/// Routes messages between the cubes and the clients connected to them: commands go from a client to its cube, events
/// go from a cube to all its clients. It lives as long as the server and keeps everything in memory.
/// </summary>
public class CubeHub(ILogger<CubeHub> logger)
{
    private readonly ILogger<CubeHub> logger = logger;

    /// <summary>
    /// The connected cube of each cube id.
    /// </summary>
    private readonly ConcurrentDictionary<string, SocketLink> cubes = new();

    /// <summary>
    /// The connected clients of each cube id, by connection.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, SocketLink>> clients = new();

    /// <summary>
    /// Serves a cube connection until it closes: its events are forwarded to the clients of that cube. A second
    /// connection with the same cube id replaces the first one.
    /// </summary>
    /// <param name="cubeId">
    /// Identifier of the cube.
    /// </param>
    /// <param name="socket">
    /// Accepted socket of the cube.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancelled when the connection or the server goes away.
    /// </param>
    public async Task RunCube(string cubeId, WebSocket socket, CancellationToken cancellationToken)
    {
        var link = new SocketLink(socket);
        var previous = cubes.GetValueOrDefault(cubeId);

        cubes[cubeId] = link;
        logger.LogInformation("Cube {CubeId} connected.", cubeId);

        if (previous is not null)
        {
            await previous.Close(WebSocketCloseStatus.PolicyViolation, "Replaced by a newer connection.");
        }

        await Broadcast(cubeId, CubeStatus(true));

        await link.Receive(
            message => Broadcast(cubeId, message),
            reason =>
            {
                logger.LogWarning("Cube {CubeId} sent an invalid message: {Reason}", cubeId, reason);

                return Task.CompletedTask;
            },
            cancellationToken
        );

        // Only forget the cube if it was not already replaced.
        if (cubes.TryRemove(new KeyValuePair<string, SocketLink>(cubeId, link)))
        {
            logger.LogInformation("Cube {CubeId} disconnected.", cubeId);

            await Broadcast(cubeId, CubeStatus(false));
        }
    }

    /// <summary>
    /// Serves a client connection until it closes: its commands are forwarded to the cube, and it is told whether the
    /// cube is online.
    /// </summary>
    /// <param name="cubeId">
    /// Identifier of the cube the client talks to.
    /// </param>
    /// <param name="socket">
    /// Accepted socket of the client.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancelled when the connection or the server goes away.
    /// </param>
    public async Task RunClient(string cubeId, WebSocket socket, CancellationToken cancellationToken)
    {
        var link = new SocketLink(socket);
        var connectionId = Guid.NewGuid();
        var group = clients.GetOrAdd(cubeId, _ => new ConcurrentDictionary<Guid, SocketLink>());

        group[connectionId] = link;

        try
        {
            await link.Send(CubeStatus(cubes.ContainsKey(cubeId)));

            await link.Receive(
                message => Forward(cubeId, link, message),
                reason => link.Send(Error(null, "invalid_message", reason)),
                cancellationToken
            );
        }
        finally
        {
            group.TryRemove(connectionId, out _);
        }
    }

    /// <summary>
    /// Sends a client's command to its cube, or answers the client with an error when the cube is offline.
    /// </summary>
    private async Task Forward(string cubeId, SocketLink client, CubeMessage command)
    {
        var id = command.Id ?? Guid.NewGuid().ToString();

        if (!cubes.TryGetValue(cubeId, out var cube) || !await cube.Send(command with { Id = id }))
        {
            await client.Send(Error(id, "cube_offline", "The cube is not connected."));
        }
    }

    /// <summary>
    /// Sends a message to every client of a cube.
    /// </summary>
    private async Task Broadcast(string cubeId, CubeMessage message)
    {
        if (!clients.TryGetValue(cubeId, out var group))
        {
            return;
        }

        await Task.WhenAll(group.Values.Select(client => client.Send(message)));
    }

    /// <summary>
    /// The notice that tells clients whether their cube is connected.
    /// </summary>
    private static CubeMessage CubeStatus(bool online)
    {
        return new CubeMessage("cube_status", Payload: JsonSerializer.SerializeToElement(new { online }));
    }

    /// <summary>
    /// The notice that tells a client a command could not be delivered.
    /// </summary>
    private static CubeMessage Error(string? replyTo, string code, string message)
    {
        return new CubeMessage("error", ReplyTo: replyTo, Payload: JsonSerializer.SerializeToElement(new { code, message }));
    }
}
