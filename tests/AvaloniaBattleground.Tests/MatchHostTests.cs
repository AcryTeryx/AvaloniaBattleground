using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;

namespace AvaloniaBattleground.Tests;

public sealed class MatchHostTests
{
    [Fact]
    public void Start_from_valid_lobby_yields_four_fighter_snapshot()
    {
        var host = new MatchHost();

        var result = host.TryStart(CreateValidLobby());

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.MatchSnapshot!.Fighters.Count);
        Assert.True(host.IsRunning);
        Assert.Equal(4, host.Snapshot!.Fighters.Count);
    }

    [Fact]
    public void Accepted_input_moves_fighter_on_tick()
    {
        var host = new MatchHost();
        host.TryStart(CreateValidLobby());
        var startPosition = host.Snapshot!.Fighters.Single(fighter => fighter.ClientId == 1).Position;

        host.SetInput(1, new PlayerInput(new GameVector(1, 0), new GameVector(1, 0), false));
        host.Tick();
        host.Tick();

        var movedPosition = host.Snapshot!.Fighters.Single(fighter => fighter.ClientId == 1).Position;
        Assert.True(movedPosition.X > startPosition.X);
    }

    [Fact]
    public void Client_disconnect_triggers_disconnect_forfeit()
    {
        var host = new MatchHost();
        host.TryStart(CreateValidLobby());

        var snapshot = host.HandleClientDisconnected(2);
        host.Tick();

        Assert.NotNull(snapshot);
        Assert.Equal(MatchPhase.MatchComplete, snapshot!.Phase);
        Assert.Equal(RoundWinReason.DisconnectForfeit, snapshot.RoundResult!.WinReason);
        Assert.Equal(Team.Blue, snapshot.MatchWinner);
    }

    [Fact]
    public void TryStart_rejects_lobby_that_is_not_ready()
    {
        var host = new MatchHost();
        var incompleteLobby = new LobbyState(
        [
            new LobbyClient(1, "Player 1", true, Team.Red, FighterRole.Melee),
        ]);

        var result = host.TryStart(incompleteLobby);

        Assert.False(result.Succeeded);
        Assert.Equal(StartMatchFailureReason.LobbyNotReady, result.FailureReason);
        Assert.False(host.IsRunning);
    }

    [Fact]
    public void TryStart_rejects_second_start_while_match_is_running()
    {
        var host = new MatchHost();
        host.TryStart(CreateValidLobby());

        var result = host.TryStart(CreateValidLobby());

        Assert.False(result.Succeeded);
        Assert.Equal(StartMatchFailureReason.AlreadyStarted, result.FailureReason);
    }

    [Fact]
    public void Fixed_delta_ticks_advance_match_time()
    {
        var host = new MatchHost();
        host.TryStart(CreateValidLobby());
        var initialRoundTime = host.Snapshot!.RoundTimeRemainingSeconds;

        for (var tick = 0; tick < 60; tick++)
        {
            host.Tick();
        }

        Assert.Equal(MatchPhase.InRound, host.Snapshot!.Phase);
        Assert.True(host.Snapshot.RoundTimeRemainingSeconds < initialRoundTime);
    }

    private static LobbyState CreateValidLobby()
    {
        return new LobbyState(
        [
            new LobbyClient(1, "Player 1", true, Team.Red, FighterRole.Melee),
            new LobbyClient(2, "Player 2", false, Team.Red, FighterRole.Ranged),
            new LobbyClient(3, "Player 3", false, Team.Blue, FighterRole.Melee),
            new LobbyClient(4, "Player 4", false, Team.Blue, FighterRole.Ranged),
        ]);
    }
}
