using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Tests;

public sealed class MatchSimulationTests
{
    [Fact]
    public void Valid_lobby_creates_one_fighter_per_client()
    {
        var match = MatchSimulation.Start(CreateValidLobby());

        Assert.Equal([1, 2, 3, 4], match.Snapshot.Fighters.Select(fighter => fighter.ClientId));
        Assert.All(match.Snapshot.Fighters, fighter => Assert.Equal(0, fighter.DashCooldownSeconds));
    }

    [Fact]
    public void Keyboard_input_mapper_normalizes_movement_and_aim()
    {
        var input = KeyboardInputMapper.Map(
            moveUp: true,
            moveDown: false,
            moveLeft: false,
            moveRight: true,
            aimUp: true,
            aimDown: false,
            aimLeft: false,
            aimRight: true,
            dash: false);

        Assert.Equal(1, input.MoveDirection.Length, precision: 6);
        Assert.Equal(1, input.AimDirection.Length, precision: 6);
        Assert.False(input.Dash);
    }

    [Fact]
    public void Fighter_movement_is_deterministic_over_fixed_ticks()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        var startPosition = match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1).Position;

        match.SetInput(1, new PlayerInput(new GameVector(1, 0), new GameVector(1, 0), false));
        match.Tick();
        match.Tick();

        var movedFighter = match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1);
        Assert.Equal(startPosition.X + (MatchRules.FighterMoveSpeed * MatchRules.FixedDeltaSeconds * 2), movedFighter.Position.X, precision: 6);
        Assert.Equal(startPosition.Y, movedFighter.Position.Y, precision: 6);
        Assert.Equal(new GameVector(1, 0), movedFighter.AimDirection);
    }

    [Fact]
    public void Universal_dash_moves_once_and_counts_down_cooldown()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        var startPosition = match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1).Position;

        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), true));
        match.Tick();
        match.Tick();

        var fighter = match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1);
        Assert.Equal(startPosition.X + MatchRules.UniversalDashDistance, fighter.Position.X, precision: 6);
        Assert.Equal(MatchRules.UniversalDashCooldownSeconds - MatchRules.FixedDeltaSeconds, fighter.DashCooldownSeconds, precision: 6);
    }

    [Fact]
    public void Movement_and_dash_are_clamped_to_hard_arena_boundary()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(1, new GameVector(MatchRules.ArenaRadius - 1, 0));

        match.SetInput(1, new PlayerInput(new GameVector(1, 0), new GameVector(1, 0), true));
        match.Tick();

        var fighter = match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1);
        Assert.Equal(MatchRules.ArenaRadius, fighter.Position.Length, precision: 6);
    }

    [Fact]
    public void Fighters_do_not_push_each_other_when_overlapping()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        var overlap = new GameVector(12, -20);
        match.OverrideFighterPositionForTesting(1, overlap);
        match.OverrideFighterPositionForTesting(2, overlap);

        match.Tick();

        Assert.Equal(overlap, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1).Position);
        Assert.Equal(overlap, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 2).Position);
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
