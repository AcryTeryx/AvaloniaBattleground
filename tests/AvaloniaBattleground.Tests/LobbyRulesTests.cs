using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Tests;

public sealed class LobbyRulesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Start_is_locked_until_exactly_four_clients_are_connected(int connectedClientCount)
    {
        var lobby = CreateLobby(connectedClientCount);

        Assert.False(lobby.StartEligibility.CanStart);
        Assert.Contains(LobbyStartLockReason.FullLobbyRequirement, lobby.StartEligibility.LockReasons);
    }

    [Fact]
    public void Client_can_choose_team_and_fighter_role()
    {
        var lobby = CreateLobby(4);

        var result = lobby.ApplySelection(new LobbySelection(2, Team.Red, FighterRole.Ranged));

        Assert.True(result.Succeeded, result.Message);
        var selectedClient = Assert.Single(
            result.Lobby.Clients,
            client => client.ClientId == 2);
        Assert.Equal(Team.Red, selectedClient.Team);
        Assert.Equal(FighterRole.Ranged, selectedClient.Role);
    }

    [Fact]
    public void Team_role_conflicts_are_rejected()
    {
        var lobby = CreateLobby(4)
            .ApplySelection(new LobbySelection(1, Team.Red, FighterRole.Melee)).Lobby;

        var result = lobby.ApplySelection(new LobbySelection(2, Team.Red, FighterRole.Melee));

        Assert.False(result.Succeeded);
        Assert.Equal(LobbySelectionFailureReason.TeamRoleConflict, result.FailureReason);
        Assert.Contains("already selected", result.Message);
        Assert.Null(result.Lobby.Clients.Single(client => client.ClientId == 2).Team);
        Assert.Null(result.Lobby.Clients.Single(client => client.ClientId == 2).Role);
    }

    [Fact]
    public void Start_is_locked_until_both_teams_satisfy_role_constraint()
    {
        var lobby = CreateLobby(4)
            .ApplySelection(new LobbySelection(1, Team.Red, FighterRole.Melee)).Lobby
            .ApplySelection(new LobbySelection(2, Team.Red, FighterRole.Ranged)).Lobby
            .ApplySelection(new LobbySelection(3, Team.Blue, FighterRole.Melee)).Lobby;

        Assert.False(lobby.StartEligibility.CanStart);
        Assert.Contains(LobbyStartLockReason.RoleConstraint, lobby.StartEligibility.LockReasons);
    }

    [Fact]
    public void Valid_four_client_team_role_composition_unlocks_start()
    {
        var lobby = CreateLobby(4)
            .ApplySelection(new LobbySelection(1, Team.Red, FighterRole.Melee)).Lobby
            .ApplySelection(new LobbySelection(2, Team.Red, FighterRole.Ranged)).Lobby
            .ApplySelection(new LobbySelection(3, Team.Blue, FighterRole.Melee)).Lobby
            .ApplySelection(new LobbySelection(4, Team.Blue, FighterRole.Ranged)).Lobby;

        Assert.True(lobby.StartEligibility.CanStart);
        Assert.Empty(lobby.StartEligibility.LockReasons);
    }

    private static LobbyState CreateLobby(int clientCount)
    {
        return new LobbyState(
            Enumerable.Range(1, clientCount)
                .Select(clientId => new LobbyClient(clientId, $"Player {clientId}", clientId == 1))
                .ToArray());
    }
}
