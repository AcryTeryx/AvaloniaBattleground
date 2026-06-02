using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Networking;

public sealed record LobbyClientInfo(
    int ClientId,
    string DisplayName,
    bool IsHost,
    Team? Team = null,
    FighterRole? Role = null);

public sealed record LobbySnapshot(
    IReadOnlyList<LobbyClientInfo> Clients,
    LobbyStartEligibility StartEligibility)
{
    public LobbySnapshot(IReadOnlyList<LobbyClientInfo> clients)
        : this(clients, ToLobbyState(clients).StartEligibility)
    {
    }

    public static LobbySnapshot Empty { get; } = new([]);

    public LobbyState ToLobbyState()
    {
        return ToLobbyState(Clients);
    }

    public static LobbySnapshot FromLobbyState(LobbyState lobby)
    {
        return new LobbySnapshot(
            lobby.Clients
                .Select(client => new LobbyClientInfo(
                    client.ClientId,
                    client.DisplayName,
                    client.IsHost,
                    client.Team,
                    client.Role))
                .ToArray(),
            lobby.StartEligibility);
    }

    private static LobbyState ToLobbyState(IReadOnlyList<LobbyClientInfo> clients)
    {
        return new LobbyState(
            clients
                .Select(client => new LobbyClient(
                    client.ClientId,
                    client.DisplayName,
                    client.IsHost,
                    client.Team,
                    client.Role))
                .ToArray());
    }
}

public sealed record JoinLobbyRequest(
    string HostAddress,
    int Port,
    string DisplayName,
    int ProtocolVersion = LobbyProtocol.CurrentVersion,
    TimeSpan? Timeout = null);

public enum JoinFailureReason
{
    InvalidAddress,
    InvalidPort,
    ConnectionFailed,
    ProtocolVersionMismatch,
}

public enum StartMatchFailureReason
{
    HostOnly,
    LobbyNotReady,
    AlreadyStarted,
}

public sealed record JoinLobbyResult(
    IClientLobbySession? Session,
    JoinFailureReason? FailureReason,
    string FailureMessage)
{
    public bool Succeeded => Session is not null;

    public static JoinLobbyResult Success(IClientLobbySession session)
    {
        return new JoinLobbyResult(session, null, string.Empty);
    }

    public static JoinLobbyResult Failure(JoinFailureReason reason, string message)
    {
        return new JoinLobbyResult(null, reason, message);
    }
}

public sealed record StartMatchResult(
    MatchSnapshot? MatchSnapshot,
    StartMatchFailureReason? FailureReason,
    string Message)
{
    public bool Succeeded => MatchSnapshot is not null;

    public static StartMatchResult Success(MatchSnapshot matchSnapshot)
    {
        return new StartMatchResult(matchSnapshot, null, string.Empty);
    }

    public static StartMatchResult Failure(StartMatchFailureReason reason, string message)
    {
        return new StartMatchResult(null, reason, message);
    }
}

public interface ILobbySession : IAsyncDisposable
{
    event EventHandler<LobbySnapshot>? SnapshotChanged;

    event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

    int LocalClientId { get; }

    LobbySnapshot Snapshot { get; }

    MatchSnapshot? MatchSnapshot { get; }

    Task<LobbySelectionResult> SelectTeamRoleAsync(
        Team team,
        FighterRole role,
        CancellationToken cancellationToken = default);

    Task SendPlayerInputAsync(
        PlayerInput input,
        CancellationToken cancellationToken = default);
}

public interface IHostLobbySession : ILobbySession
{
    IReadOnlyList<string> ShareableAddresses { get; }

    int Port { get; }

    Task<StartMatchResult> StartMatchAsync(CancellationToken cancellationToken = default);
}

public interface IClientLobbySession : ILobbySession
{
}

public interface ILobbyNetworkService
{
    Task<IHostLobbySession> StartHostAsync(
        string displayName,
        CancellationToken cancellationToken = default);

    Task<JoinLobbyResult> JoinAsync(
        JoinLobbyRequest request,
        CancellationToken cancellationToken = default);
}
