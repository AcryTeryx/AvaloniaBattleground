using AvaloniaBattleground.App.ViewModels;
using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;

namespace AvaloniaBattleground.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Shell_initializes_from_stored_local_display_name()
    {
        var profilePath = CreateProfilePath();
        new LocalProfileStore(profilePath).Save(new LocalProfile("Acryteryx"));

        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(profilePath),
            new RecordingApplicationShell(),
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher());

        Assert.True(viewModel.IsMainMenu);
        Assert.False(viewModel.IsJoinScreen);
        Assert.False(viewModel.IsLobbyScreen);
        Assert.Equal("Acryteryx", viewModel.CurrentDisplayName);
        Assert.Equal("Acryteryx", viewModel.DisplayNameInput);
    }

    [Fact]
    public async Task Shell_saves_edited_display_name_before_host_lobby_navigation()
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var hostSession = new FakeHostLobbySession(
            ["192.168.1.10"],
            54321,
            new LobbySnapshot([new LobbyClientInfo(1, "Acryteryx", true)]));
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            store,
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher())
        {
            DisplayNameInput = "  Acryteryx  ",
        };

        await viewModel.HostMatchCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsMainMenu);
        Assert.False(viewModel.IsJoinScreen);
        Assert.True(viewModel.IsLobbyScreen);
        Assert.True(viewModel.IsHostLobby);
        Assert.Equal("Lobby", viewModel.CurrentScreenTitle);
        Assert.Equal("192.168.1.10", viewModel.HostAddressesDisplay);
        Assert.Equal("54321", viewModel.HostPortDisplay);
        Assert.Equal("Acryteryx", viewModel.CurrentDisplayName);
        Assert.Equal("Acryteryx", store.Load().DisplayName);
        Assert.Equal("Acryteryx", Assert.Single(networkService.StartedHostDisplayNames));
        Assert.Equal("Acryteryx", Assert.Single(viewModel.LobbyClients).DisplayName);
    }

    [Fact]
    public void Shell_saves_edited_display_name_before_showing_join_screen()
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var viewModel = new MainWindowViewModel(
            store,
            new RecordingApplicationShell(),
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher())
        {
            DisplayNameInput = "  Joining Player  ",
        };

        viewModel.JoinMatchCommand.Execute(null);

        Assert.False(viewModel.IsMainMenu);
        Assert.True(viewModel.IsJoinScreen);
        Assert.False(viewModel.IsLobbyScreen);
        Assert.Equal("Join Match", viewModel.CurrentScreenTitle);
        Assert.Equal("Joining Player", viewModel.CurrentDisplayName);
        Assert.Equal("Joining Player", store.Load().DisplayName);
    }

    [Fact]
    public async Task Shell_saves_display_name_before_joining_lobby()
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var clientSession = new FakeClientLobbySession(
            new LobbySnapshot(
            [
                new LobbyClientInfo(1, "Host Player", true),
                new LobbyClientInfo(2, "Joining Player", false),
            ]));
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            store,
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher())
        {
            DisplayNameInput = "Joining Player",
        };

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        var request = Assert.Single(networkService.JoinRequests);
        Assert.Equal("127.0.0.1", request.HostAddress);
        Assert.Equal(54321, request.Port);
        Assert.Equal("Joining Player", request.DisplayName);
        Assert.False(viewModel.IsMainMenu);
        Assert.False(viewModel.IsJoinScreen);
        Assert.True(viewModel.IsLobbyScreen);
        Assert.False(viewModel.IsHostLobby);
        Assert.Equal("Joining Player", store.Load().DisplayName);
        Assert.Equal(["Host Player", "Joining Player"], viewModel.LobbyClients.Select(client => client.DisplayName));
    }

    [Fact]
    public async Task Shell_keeps_join_screen_with_failure_feedback_when_join_fails()
    {
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "Could not connect to the hosted Lobby."),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsJoinScreen);
        Assert.False(viewModel.IsLobbyScreen);
        Assert.True(viewModel.HasConnectionFeedback);
        Assert.Equal("Could not connect to the hosted Lobby.", viewModel.ConnectionFeedback);
    }

    [Fact]
    public async Task Shell_keeps_join_screen_with_failure_feedback_when_network_join_throws()
    {
        var networkService = new RecordingLobbyNetworkService
        {
            JoinException = new InvalidOperationException("socket setup failed"),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsJoinScreen);
        Assert.False(viewModel.IsLobbyScreen);
        Assert.True(viewModel.HasConnectionFeedback);
        Assert.Contains("Could not connect", viewModel.ConnectionFeedback);
    }

    [Fact]
    public async Task Shell_reflects_lobby_snapshot_updates()
    {
        var clientSession = new FakeClientLobbySession(
            new LobbySnapshot([new LobbyClientInfo(1, "Host Player", true)]));
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        clientSession.Publish(
            new LobbySnapshot(
            [
                new LobbyClientInfo(1, "Host Player", true),
                new LobbyClientInfo(2, "Joining Player", false),
            ]));

        Assert.Equal(["Host Player", "Joining Player"], viewModel.LobbyClients.Select(client => client.DisplayName));
    }

    [Fact]
    public void Exit_command_requests_application_exit()
    {
        var shell = new RecordingApplicationShell();
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            shell,
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher());

        viewModel.ExitCommand.Execute(null);

        Assert.True(shell.ExitRequested);
    }

    private static string CreateProfilePath()
    {
        return Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "profile.json");
    }

    private sealed class RecordingApplicationShell : IApplicationShell
    {
        public bool ExitRequested { get; private set; }

        public void Exit()
        {
            ExitRequested = true;
        }
    }

    private sealed class RecordingLobbyNetworkService : ILobbyNetworkService
    {
        public IHostLobbySession HostSession { get; init; } = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            new LobbySnapshot([new LobbyClientInfo(1, "Host Player", true)]));

        public JoinLobbyResult JoinResult { get; init; } = JoinLobbyResult.Failure(
            JoinFailureReason.ConnectionFailed,
            "Could not connect to the hosted Lobby.");

        public Exception? JoinException { get; init; }

        public List<string> StartedHostDisplayNames { get; } = [];

        public List<JoinLobbyRequest> JoinRequests { get; } = [];

        public Task<IHostLobbySession> StartHostAsync(
            string displayName,
            CancellationToken cancellationToken = default)
        {
            StartedHostDisplayNames.Add(displayName);
            return Task.FromResult(HostSession);
        }

        public Task<JoinLobbyResult> JoinAsync(
            JoinLobbyRequest request,
            CancellationToken cancellationToken = default)
        {
            if (JoinException is not null)
            {
                throw JoinException;
            }

            JoinRequests.Add(request);
            return Task.FromResult(JoinResult);
        }
    }

    private sealed class FakeHostLobbySession(
        IReadOnlyList<string> shareableAddresses,
        int port,
        LobbySnapshot snapshot)
        : FakeLobbySession(snapshot), IHostLobbySession
    {
        public IReadOnlyList<string> ShareableAddresses { get; } = shareableAddresses;

        public int Port { get; } = port;
    }

    private sealed class FakeClientLobbySession(LobbySnapshot snapshot)
        : FakeLobbySession(snapshot), IClientLobbySession;

    private abstract class FakeLobbySession(LobbySnapshot snapshot) : ILobbySession
    {
        public event EventHandler<LobbySnapshot>? SnapshotChanged;

        public LobbySnapshot Snapshot { get; private set; } = snapshot;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void Publish(LobbySnapshot snapshot)
        {
            Snapshot = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }

    private sealed class ImmediateViewDispatcher : IViewDispatcher
    {
        public void Post(Action action)
        {
            action();
        }
    }
}
