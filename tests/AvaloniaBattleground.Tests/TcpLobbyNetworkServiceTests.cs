using AvaloniaBattleground.Core;
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

    [Fact]
    public async Task Connected_client_can_choose_team_and_fighter_role()
    {
        var networkService = new TcpLobbyNetworkService();
        await using var host = await networkService.StartHostAsync("Host Player");
        var joinResult = await networkService.JoinAsync(
            new JoinLobbyRequest("127.0.0.1", host.Port, "Joining Player"));
        await using var client = joinResult.Session!;

        var selectionResult = await client.SelectTeamRoleAsync(Team.Red, FighterRole.Melee);

        Assert.True(selectionResult.Succeeded, selectionResult.Message);
        var hostSnapshot = await WaitForSnapshotAsync(
            host,
            snapshot => snapshot.Clients.Any(lobbyClient =>
                lobbyClient.DisplayName == "Joining Player" &&
                lobbyClient.Team == Team.Red &&
                lobbyClient.Role == FighterRole.Melee));
        var clientSnapshot = await WaitForSnapshotAsync(
            client,
            snapshot => snapshot.Clients.Any(lobbyClient =>
                lobbyClient.DisplayName == "Joining Player" &&
                lobbyClient.Team == Team.Red &&
                lobbyClient.Role == FighterRole.Melee));

        Assert.Equal(hostSnapshot.Clients, clientSnapshot.Clients);
    }

    [Fact]
    public async Task Team_role_conflict_selection_is_rejected_cleanly()
    {
        var networkService = new TcpLobbyNetworkService();
        await using var host = await networkService.StartHostAsync("Host Player");
        var joinResult = await networkService.JoinAsync(
            new JoinLobbyRequest("127.0.0.1", host.Port, "Joining Player"));
        await using var client = joinResult.Session!;

        var hostSelection = await host.SelectTeamRoleAsync(Team.Red, FighterRole.Melee);
        var clientSelection = await client.SelectTeamRoleAsync(Team.Red, FighterRole.Melee);

        Assert.True(hostSelection.Succeeded, hostSelection.Message);
        Assert.False(clientSelection.Succeeded);
        Assert.Equal(LobbySelectionFailureReason.TeamRoleConflict, clientSelection.FailureReason);
        Assert.Contains("already selected", clientSelection.Message);
        Assert.Null(client.Snapshot.Clients.Single(lobbyClient => lobbyClient.DisplayName == "Joining Player").Team);
        Assert.Null(client.Snapshot.Clients.Single(lobbyClient => lobbyClient.DisplayName == "Joining Player").Role);
    }

    [Fact]
    public async Task Valid_lobby_start_transitions_all_connected_clients_to_match_snapshot()
    {
        var (host, clients) = await CreateValidFourClientLobbyAsync();
        try
        {
            var startResult = await host.StartMatchAsync();

            Assert.True(startResult.Succeeded, startResult.Message);
            Assert.Equal(4, startResult.MatchSnapshot!.Fighters.Count);
            Assert.Equal(4, (await WaitForMatchSnapshotAsync(host)).Fighters.Count);

            foreach (var client in clients)
            {
                Assert.Equal(4, (await WaitForMatchSnapshotAsync(client)).Fighters.Count);
            }
        }
        finally
        {
            await DisposeSessionsAsync(host, clients);
        }
    }

    [Fact]
    public async Task Client_input_moves_fighter_through_host_authoritative_snapshot()
    {
        var (host, clients) = await CreateValidFourClientLobbyAsync();
        try
        {
            await host.StartMatchAsync();
            var firstClient = clients[0];
            var initialSnapshot = await WaitForMatchSnapshotAsync(firstClient);
            var startPosition = initialSnapshot.Fighters.Single(fighter => fighter.ClientId == firstClient.LocalClientId).Position;

            await firstClient.SendPlayerInputAsync(new PlayerInput(new GameVector(1, 0), new GameVector(1, 0), false));
            var movedSnapshot = await WaitForMatchSnapshotAsync(
                firstClient,
                snapshot => snapshot.Fighters.Single(fighter => fighter.ClientId == firstClient.LocalClientId).Position.X > startPosition.X);

            Assert.True(movedSnapshot.Fighters.Single(fighter => fighter.ClientId == firstClient.LocalClientId).Position.X > startPosition.X);
        }
        finally
        {
            await DisposeSessionsAsync(host, clients);
        }
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

    private static async Task<MatchSnapshot> WaitForMatchSnapshotAsync(
        ILobbySession session,
        Predicate<MatchSnapshot>? predicate = null)
    {
        predicate ??= _ => true;

        if (session.MatchSnapshot is not null && predicate(session.MatchSnapshot))
        {
            return session.MatchSnapshot;
        }

        var completion = new TaskCompletionSource<MatchSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleMatchSnapshotChanged(object? sender, MatchSnapshot snapshot)
        {
            if (predicate(snapshot))
            {
                completion.TrySetResult(snapshot);
            }
        }

        session.MatchSnapshotChanged += HandleMatchSnapshotChanged;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var registration = timeout.Token.Register(
            () => completion.TrySetCanceled(timeout.Token));

        try
        {
            return await completion.Task;
        }
        finally
        {
            session.MatchSnapshotChanged -= HandleMatchSnapshotChanged;
        }
    }

    private static async Task<(IHostLobbySession Host, IClientLobbySession[] Clients)> CreateValidFourClientLobbyAsync()
    {
        var networkService = new TcpLobbyNetworkService();
        var host = await networkService.StartHostAsync("Player 1");
        var clients = new List<IClientLobbySession>();

        for (var index = 2; index <= 4; index++)
        {
            var joinResult = await networkService.JoinAsync(
                new JoinLobbyRequest("127.0.0.1", host.Port, $"Player {index}"));
            Assert.True(joinResult.Succeeded, joinResult.FailureMessage);
            clients.Add(joinResult.Session!);
        }

        await host.SelectTeamRoleAsync(Team.Red, FighterRole.Melee);
        await clients[0].SelectTeamRoleAsync(Team.Red, FighterRole.Ranged);
        await clients[1].SelectTeamRoleAsync(Team.Blue, FighterRole.Melee);
        await clients[2].SelectTeamRoleAsync(Team.Blue, FighterRole.Ranged);

        await WaitForSnapshotAsync(host, snapshot => snapshot.StartEligibility.CanStart);

        return (host, clients.ToArray());
    }

    private static async Task DisposeSessionsAsync(
        IHostLobbySession host,
        IReadOnlyList<IClientLobbySession> clients)
    {
        foreach (var client in clients)
        {
            await client.DisposeAsync();
        }

        await host.DisposeAsync();
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
