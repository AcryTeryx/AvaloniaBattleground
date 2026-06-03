namespace AvaloniaBattleground.Core;

public static class LobbyRules
{
    public static LobbySelectionResult ApplySelection(LobbyState lobby, LobbySelection selection)
    {
        var selectedClient = lobby.Clients.SingleOrDefault(client => client.ClientId == selection.ClientId);
        if (selectedClient is null)
        {
            return LobbySelectionResult.Failure(
                lobby,
                LobbySelectionFailureReason.UnknownClient,
                "Client is not connected to this Lobby.");
        }

        var slotOccupied = lobby.Clients.Any(client =>
            client.ClientId != selection.ClientId &&
            client.Team == selection.Team &&
            client.Role == selection.Role);
        if (slotOccupied)
        {
            return LobbySelectionResult.Failure(
                lobby,
                LobbySelectionFailureReason.TeamRoleConflict,
                $"{selection.Team} {selection.Role} is already selected.");
        }

        var updatedClients = lobby.Clients
            .Select(client => client.ClientId == selection.ClientId
                ? client with { Team = selection.Team, Role = selection.Role }
                : client)
            .ToArray();

        return LobbySelectionResult.Success(new LobbyState(updatedClients));
    }

    public static LobbyStartEligibility CalculateStartEligibility(IReadOnlyList<LobbyClient> clients)
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
