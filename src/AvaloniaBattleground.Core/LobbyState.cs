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

public sealed class LobbyState
{
    public LobbyState(IReadOnlyList<LobbyClient> clients)
    {
        Clients = clients;
        StartEligibility = CalculateStartEligibility(clients);
    }

    public IReadOnlyList<LobbyClient> Clients { get; }

    public LobbyStartEligibility StartEligibility { get; }

    public LobbySelectionResult ApplySelection(LobbySelection selection)
    {
        var selectedClient = Clients.SingleOrDefault(client => client.ClientId == selection.ClientId);
        if (selectedClient is null)
        {
            return LobbySelectionResult.Failure(
                this,
                LobbySelectionFailureReason.UnknownClient,
                "Client is not connected to this Lobby.");
        }

        var slotOccupied = Clients.Any(client =>
            client.ClientId != selection.ClientId &&
            client.Team == selection.Team &&
            client.Role == selection.Role);
        if (slotOccupied)
        {
            return LobbySelectionResult.Failure(
                this,
                LobbySelectionFailureReason.TeamRoleConflict,
                $"{selection.Team} {selection.Role} is already selected.");
        }

        var updatedClients = Clients
            .Select(client => client.ClientId == selection.ClientId
                ? client with { Team = selection.Team, Role = selection.Role }
                : client)
            .ToArray();

        return LobbySelectionResult.Success(new LobbyState(updatedClients));
    }

    private static LobbyStartEligibility CalculateStartEligibility(IReadOnlyList<LobbyClient> clients)
    {
        var lockReasons = new HashSet<LobbyStartLockReason>();

        if (clients.Count != 4)
        {
            lockReasons.Add(LobbyStartLockReason.FullLobbyRequirement);
        }

        if (!HasValidTeamRoleComposition(clients))
        {
            lockReasons.Add(LobbyStartLockReason.RoleConstraint);
        }

        return new LobbyStartEligibility(lockReasons.Count == 0, lockReasons);
    }

    private static bool HasValidTeamRoleComposition(IReadOnlyList<LobbyClient> clients)
    {
        if (clients.Count != 4 || clients.Any(client => client.Team is null || client.Role is null))
        {
            return false;
        }

        return HasOneMeleeAndOneRanged(clients, Team.Red) &&
            HasOneMeleeAndOneRanged(clients, Team.Blue);
    }

    private static bool HasOneMeleeAndOneRanged(IReadOnlyList<LobbyClient> clients, Team team)
    {
        var teamClients = clients
            .Where(client => client.Team == team)
            .ToArray();

        return teamClients.Length == 2 &&
            teamClients.Count(client => client.Role == FighterRole.Melee) == 1 &&
            teamClients.Count(client => client.Role == FighterRole.Ranged) == 1;
    }
}
