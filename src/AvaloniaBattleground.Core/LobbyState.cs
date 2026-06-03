namespace AvaloniaBattleground.Core;

public sealed record LobbyClient(
    int ClientId,
    string DisplayName,
    bool IsHost,
    Team? Team = null,
    FighterRole? Role = null);

public enum Team
{
    Blue,
    Red,
}

public enum FighterRole
{
    Melee,
    Ranged,
}

public enum LobbyStartLockReason
{
    FullLobbyRequirement,
    RoleConstraint,
}

public enum LobbySelectionFailureReason
{
    UnknownClient,
    TeamRoleConflict,
}

public sealed record LobbySelection(int ClientId, Team Team, FighterRole Role);

public sealed record LobbySelectionResult(
    LobbyState Lobby,
    bool Succeeded,
    LobbySelectionFailureReason? FailureReason,
    string Message)
{
    public static LobbySelectionResult Success(LobbyState lobby)
    {
        return new LobbySelectionResult(lobby, true, null, string.Empty);
    }

    public static LobbySelectionResult Failure(
        LobbyState lobby,
        LobbySelectionFailureReason failureReason,
        string message)
    {
        return new LobbySelectionResult(lobby, false, failureReason, message);
    }
}

public sealed record LobbyStartEligibility(
    bool CanStart,
    IReadOnlySet<LobbyStartLockReason> LockReasons);

public sealed record LobbyState(
    IReadOnlyList<LobbyClient> Clients,
    LobbyStartEligibility StartEligibility)
{
    public LobbyState(IReadOnlyList<LobbyClient> clients)
        : this(clients, LobbyRules.CalculateStartEligibility(clients))
    {
    }

    public static LobbyState Empty { get; } = new([]);

    public LobbySelectionResult ApplySelection(LobbySelection selection)
    {
        return LobbyRules.ApplySelection(this, selection);
    }
}
