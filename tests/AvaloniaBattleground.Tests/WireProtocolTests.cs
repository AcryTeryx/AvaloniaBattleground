using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AvaloniaBattleground.Tests;

public sealed class WireProtocolTests
{
    [Fact]
    public async Task Wire_message_round_trips_through_newline_delimited_reader()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await listener.AcceptTcpClientAsync();

        var message = WireMessage.JoinRequest(LobbyProtocol.CurrentVersion, "Player 1");
        await WireMessageWriter.WriteAsync(
            client.GetStream(),
            message,
            CancellationToken.None);

        var reader = new WireMessageReader(server.GetStream());
        var roundTripped = await reader.ReadAsync(CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(WireMessageTypes.JoinRequest, roundTripped!.MessageType);
        Assert.Equal("Player 1", roundTripped.DisplayName);
    }

    [Fact]
    public void Lobby_snapshot_wire_message_serializes_client_records()
    {
        var clients = new LobbyClient[]
        {
            new(1, "Host", true, Team.Red, FighterRole.Melee),
        };

        var message = WireMessage.LobbySnapshot(clients);
        var json = JsonSerializer.Serialize(message);

        Assert.Contains("LobbySnapshot", json, StringComparison.Ordinal);
        Assert.Contains("\"Host\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ClientId\":1", json, StringComparison.Ordinal);
    }
}
