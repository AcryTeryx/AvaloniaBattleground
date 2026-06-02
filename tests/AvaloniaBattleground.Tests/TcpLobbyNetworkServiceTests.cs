using AvaloniaBattleground.Networking;
using System.Net;
using System.Net.Sockets;

namespace AvaloniaBattleground.Tests;

public sealed class TcpLobbyNetworkServiceTests
{
    [Fact]
    public async Task Host_started_lobby_contains_host_display_name()
    {
        var networkService = new TcpLobbyNetworkService();

        await using var host = await networkService.StartHostAsync("Host Player");

        Assert.True(host.Port > 0);
        Assert.NotEmpty(host.ShareableAddresses);
        var client = Assert.Single(host.Snapshot.Clients);
        Assert.Equal("Host Player", client.DisplayName);
        Assert.True(client.IsHost);
    }

    [Fact]
    public async Task Client_can_join_hosted_lobby_and_both_sessions_show_connected_clients()
    {
        var networkService = new TcpLobbyNetworkService();
        await using var host = await networkService.StartHostAsync("Host Player");

        var joinResult = await networkService.JoinAsync(
            new JoinLobbyRequest("127.0.0.1", host.Port, "Joining Player"));

        Assert.True(joinResult.Succeeded, joinResult.FailureMessage);
        await using var client = joinResult.Session!;

        var hostSnapshot = await WaitForSnapshotAsync(host, snapshot => snapshot.Clients.Count == 2);
        var clientSnapshot = await WaitForSnapshotAsync(client, snapshot => snapshot.Clients.Count == 2);

        Assert.Equal(
            ["Host Player", "Joining Player"],
            hostSnapshot.Clients.Select(lobbyClient => lobbyClient.DisplayName));
        Assert.Equal(
            hostSnapshot.Clients.Select(lobbyClient => lobbyClient.DisplayName),
            clientSnapshot.Clients.Select(lobbyClient => lobbyClient.DisplayName));
    }

    [Fact]
    public async Task Join_to_unavailable_endpoint_returns_clear_failure()
    {
        var networkService = new TcpLobbyNetworkService();
        var unusedPort = GetUnusedTcpPort();

        var joinResult = await networkService.JoinAsync(
            new JoinLobbyRequest(
                "127.0.0.1",
                unusedPort,
                "Joining Player",
                Timeout: TimeSpan.FromMilliseconds(250)));

        Assert.False(joinResult.Succeeded);
        Assert.Equal(JoinFailureReason.ConnectionFailed, joinResult.FailureReason);
        Assert.Contains("Could not connect", joinResult.FailureMessage);
    }

    [Fact]
    public async Task Protocol_version_mismatch_is_rejected_cleanly()
    {
        var networkService = new TcpLobbyNetworkService();
        await using var host = await networkService.StartHostAsync("Host Player");

        var joinResult = await networkService.JoinAsync(
            new JoinLobbyRequest(
                "127.0.0.1",
                host.Port,
                "Joining Player",
                ProtocolVersion: LobbyProtocol.CurrentVersion + 1));

        Assert.False(joinResult.Succeeded);
        Assert.Equal(JoinFailureReason.ProtocolVersionMismatch, joinResult.FailureReason);
        Assert.Contains("protocol version", joinResult.FailureMessage);
    }

    [Theory]
    [InlineData("not an address", 5000, JoinFailureReason.InvalidAddress)]
    [InlineData("127.0.0.1", 0, JoinFailureReason.InvalidPort)]
    public async Task Invalid_manual_join_input_returns_clear_failure(
        string hostAddress,
        int port,
        JoinFailureReason expectedFailureReason)
    {
        var networkService = new TcpLobbyNetworkService();

        var joinResult = await networkService.JoinAsync(
            new JoinLobbyRequest(hostAddress, port, "Joining Player"));

        Assert.False(joinResult.Succeeded);
        Assert.Equal(expectedFailureReason, joinResult.FailureReason);
        Assert.NotEmpty(joinResult.FailureMessage);
    }

    private static async Task<LobbySnapshot> WaitForSnapshotAsync(
        ILobbySession session,
        Predicate<LobbySnapshot> predicate)
    {
        if (predicate(session.Snapshot))
        {
            return session.Snapshot;
        }

        var completion = new TaskCompletionSource<LobbySnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleSnapshotChanged(object? sender, LobbySnapshot snapshot)
        {
            if (predicate(snapshot))
            {
                completion.TrySetResult(snapshot);
            }
        }

        session.SnapshotChanged += HandleSnapshotChanged;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var registration = timeout.Token.Register(
            () => completion.TrySetCanceled(timeout.Token));

        try
        {
            return await completion.Task;
        }
        finally
        {
            session.SnapshotChanged -= HandleSnapshotChanged;
        }
    }

    private static int GetUnusedTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
