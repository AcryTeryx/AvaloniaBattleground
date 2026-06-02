using AvaloniaBattleground.Core;
using System.Text.Json;

namespace AvaloniaBattleground.Tests;

public sealed class MatchSimulationTests
{
    [Fact]
    public void Valid_lobby_creates_one_fighter_per_client()
    {
        var match = MatchSimulation.Start(CreateValidLobby());

        Assert.Equal([1, 2, 3, 4], match.Snapshot.Fighters.Select(fighter => fighter.ClientId));
        Assert.All(match.Snapshot.Fighters, fighter => Assert.Equal(0, fighter.DashCooldownSeconds));
        Assert.Equal(200, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1).Health);
        Assert.Equal(100, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 2).Health);
        Assert.Equal(200, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 3).Health);
        Assert.Equal(100, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 4).Health);
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

    [Fact]
    public void Melee_frontal_strike_damages_enemy_in_aim_arc_and_starts_cooldown()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(1, GameVector.Zero);
        match.OverrideFighterPositionForTesting(2, new GameVector(24, 0));
        match.OverrideFighterPositionForTesting(3, new GameVector(32, 0));

        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, true, false));
        match.Tick();

        Assert.Equal(100, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 2).Health);
        Assert.Equal(182, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 3).Health);
        Assert.Equal(MatchRules.MeleeFrontalStrikeCooldownSeconds, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1).PrimaryAttackCooldownSeconds);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.MeleeFrontalStrike);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.Hit && effect.TargetClientId == 3);
    }

    [Fact]
    public void Melee_area_slash_damages_nearby_enemies_only_and_starts_ability_cooldown()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(1, GameVector.Zero);
        match.OverrideFighterPositionForTesting(2, new GameVector(20, 0));
        match.OverrideFighterPositionForTesting(3, new GameVector(48, 0));
        match.OverrideFighterPositionForTesting(4, new GameVector(90, 0));

        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();

        Assert.Equal(100, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 2).Health);
        Assert.Equal(165, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 3).Health);
        Assert.Equal(100, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 4).Health);
        Assert.Equal(MatchRules.MeleeAreaSlashCooldownSeconds, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 1).RoleAbilityCooldownSeconds);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.MeleeAreaSlash);
    }

    [Fact]
    public void Melee_attacks_are_cooldown_gated_and_create_death_feedback()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(1, GameVector.Zero);
        match.OverrideFighterPositionForTesting(4, new GameVector(32, 0));

        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, true, false));
        match.Tick();
        match.Tick();

        Assert.Equal(82, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 4).Health);

        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();
        AdvanceTicks(match, MatchRules.MeleeAreaSlashCooldownSeconds);
        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();
        AdvanceTicks(match, MatchRules.MeleeAreaSlashCooldownSeconds);
        match.SetInput(1, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();

        var target = match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 4);
        Assert.Equal(0, target.Health);
        Assert.True(target.IsDefeated);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.Death && effect.TargetClientId == 4);
    }

    [Fact]
    public void Ranged_single_arrow_shot_damages_enemy_and_removes_projectile_on_hit()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(2, GameVector.Zero);
        match.OverrideFighterPositionForTesting(3, new GameVector(20, 0));

        match.SetInput(2, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, true, false));
        match.Tick();

        Assert.Equal(186, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 3).Health);
        Assert.Empty(match.Snapshot.Projectiles);
        Assert.Equal(MatchRules.RangedSingleArrowShotCooldownSeconds, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 2).PrimaryAttackCooldownSeconds);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.RangedSingleArrowShot);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.Hit && effect.TargetClientId == 3);
    }

    [Fact]
    public void Ranged_cone_volley_fires_five_arrows()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(2, GameVector.Zero);
        match.OverrideFighterPositionForTesting(3, new GameVector(180, 0));

        match.SetInput(2, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();

        Assert.Equal(MatchRules.RangedConeVolleyArrowCount, match.Snapshot.Projectiles.Count(projectile => projectile.Kind == ProjectileKind.RangedConeVolleyArrow));
        Assert.Equal(MatchRules.RangedConeVolleyCooldownSeconds, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 2).RoleAbilityCooldownSeconds);
        Assert.Contains(match.Snapshot.Effects, effect => effect.Kind == CombatEffectKind.RangedConeVolley);
    }

    [Fact]
    public void Ranged_cone_volley_damages_each_fighter_once_even_when_multiple_arrows_overlap()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(2, GameVector.Zero);
        match.OverrideFighterPositionForTesting(3, new GameVector(20, 0));

        match.SetInput(2, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();

        Assert.Equal(176, match.Snapshot.Fighters.Single(fighter => fighter.ClientId == 3).Health);
        Assert.Single(
            match.Snapshot.Effects,
            effect => effect.Kind == CombatEffectKind.Hit && effect.TargetClientId == 3);
    }

    [Fact]
    public void Ranged_projectiles_disappear_when_they_hit_the_circular_arena_boundary()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.OverrideFighterPositionForTesting(2, new GameVector(MatchRules.ArenaRadius - 14, 0));

        match.SetInput(2, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, true, false));
        match.Tick();

        Assert.Empty(match.Snapshot.Projectiles);
    }

    [Fact]
    public void Match_snapshot_round_trips_through_network_json()
    {
        var match = MatchSimulation.Start(CreateValidLobby());
        match.SetInput(2, new PlayerInput(GameVector.Zero, new GameVector(1, 0), false, false, true));
        match.Tick();

        var json = JsonSerializer.Serialize(match.Snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTripped = JsonSerializer.Deserialize<MatchSnapshot>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTripped);
        Assert.Equal(match.Snapshot.Fighters.Count, roundTripped.Fighters.Count);
        Assert.Equal(match.Snapshot.Projectiles.Count, roundTripped.Projectiles.Count);
        Assert.Equal(match.Snapshot.Effects.Count, roundTripped.Effects.Count);
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

    private static void AdvanceTicks(MatchSimulation match, double seconds)
    {
        var ticks = (int)Math.Ceiling(seconds / MatchRules.FixedDeltaSeconds);
        for (var index = 0; index < ticks; index++)
        {
            match.Tick();
        }
    }
}
