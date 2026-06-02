namespace AvaloniaBattleground.Networking;

public sealed record LobbyClientInfo(int ClientId, string DisplayName, bool IsHost);

public sealed record LobbySnapshot(IReadOnlyList<LobbyClientInfo> Clients)
{
    public static LobbySnapshot Empty { get; } = new([]);
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

public interface ILobbySession : IAsyncDisposable
{
    event EventHandler<LobbySnapshot>? SnapshotChanged;

    LobbySnapshot Snapshot { get; }
}

public interface IHostLobbySession : ILobbySession
{
    IReadOnlyList<string> ShareableAddresses { get; }

    int Port { get; }
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
