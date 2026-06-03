using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Networking;

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

public enum LobbySessionEndReason
{
    HostDisconnectEnd,
}

public sealed record LobbySessionEnded(
    LobbySessionEndReason Reason,
    string Message);

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
    event EventHandler<LobbyState>? SnapshotChanged;

    event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

    event EventHandler<LobbySessionEnded>? SessionEnded;

    int LocalClientId { get; }

    LobbyState Snapshot { get; }

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
