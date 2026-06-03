using System.Text.Json.Serialization;

namespace AvaloniaBattleground.Core;

public readonly record struct GameVector(double X, double Y)
{
    [JsonIgnore]
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public static GameVector Zero { get; } = new(0, 0);

    public double Dot(GameVector other)
    {
        return (X * other.X) + (Y * other.Y);
    }

    public double DistanceTo(GameVector other)
    {
        return (this - other).Length;
    }

    public GameVector NormalizeOrZero()
    {
        var length = Length;
        return length == 0
            ? Zero
            : new GameVector(X / length, Y / length);
    }

    public static GameVector operator +(GameVector left, GameVector right)
    {
        return new GameVector(left.X + right.X, left.Y + right.Y);
    }

    public static GameVector operator -(GameVector left, GameVector right)
    {
        return new GameVector(left.X - right.X, left.Y - right.Y);
    }

    public static GameVector operator *(GameVector vector, double scale)
    {
        return new GameVector(vector.X * scale, vector.Y * scale);
    }
}

public static class MatchRules
{
    public const int FixedTickRate = 60;
    public const double FixedDeltaSeconds = 1.0 / FixedTickRate;
    public const double ArenaRadius = 240;
    public const double FighterMoveSpeed = 120;
    public const int MeleeFighterHealth = 200;
    public const int RangedFighterHealth = 100;
    public const double MeleeFighterRadius = 12;
    public const double RangedFighterRadius = 10;
    public const double UniversalDashCooldownSeconds = 2.5;
    public const double UniversalDashDistance = 36;
    public const int MeleeFrontalStrikeDamage = 18;
    public const double MeleeFrontalStrikeCooldownSeconds = 0.45;
    public const double MeleeFrontalStrikeRange = 44;
    public const double MeleeFrontalStrikeHalfAngleDegrees = 45;
    public const int MeleeAreaSlashDamage = 35;
    public const double MeleeAreaSlashCooldownSeconds = 5;
    public const double MeleeAreaSlashRadius = 52;
    public const int RangedSingleArrowShotDamage = 14;
    public const double RangedSingleArrowShotCooldownSeconds = 0.6;
    public const int RangedConeVolleyDamage = 24;
    public const double RangedConeVolleyCooldownSeconds = 6;
    public const int RangedConeVolleyArrowCount = 5;
    public const double RangedConeVolleySpreadDegrees = 60;
    public const double ProjectileSpeed = 300;
    public const double ProjectileRadius = 4;
    public const double CombatEffectLifetimeSeconds = 0.18;
    public const double RoundDurationSeconds = 90;
    public const double RoundTransitionSeconds = 3;
    public const int RoundsToWinMatch = 2;

    public static int GetStartingHealth(FighterRole role)
    {
        return role == FighterRole.Melee
            ? MeleeFighterHealth
            : RangedFighterHealth;
    }

    public static double GetFighterRadius(FighterRole role)
    {
        return role == FighterRole.Melee
            ? MeleeFighterRadius
            : RangedFighterRadius;
    }
}

public sealed record PlayerInput(
    GameVector MoveDirection,
    GameVector AimDirection,
    bool Dash,
    bool PrimaryAttack = false,
    bool RoleAbility = false);

public static class KeyboardInputMapper
{
    public static PlayerInput Map(
        bool moveUp,
        bool moveDown,
        bool moveLeft,
        bool moveRight,
        bool aimUp,
        bool aimDown,
        bool aimLeft,
        bool aimRight,
        bool dash,
        bool primaryAttack = false,
        bool roleAbility = false)
    {
        var move = new GameVector(
            BoolToAxis(moveRight, moveLeft),
            BoolToAxis(moveDown, moveUp)).NormalizeOrZero();
        var aim = new GameVector(
            BoolToAxis(aimRight, aimLeft),
            BoolToAxis(aimDown, aimUp)).NormalizeOrZero();

        return new PlayerInput(move, aim, dash, primaryAttack, roleAbility);
    }

    private static int BoolToAxis(bool positive, bool negative)
    {
        return positive == negative
            ? 0
            : positive ? 1 : -1;
    }
}

public enum ProjectileKind
{
    RangedSingleArrowShot,
    RangedConeVolleyArrow,
}

public enum CombatEffectKind
{
    MeleeFrontalStrike,
    MeleeAreaSlash,
    RangedSingleArrowShot,
    RangedConeVolley,
    Hit,
    Death,
}

public enum MatchPhase
{
    InRound,
    RoundComplete,
    MatchComplete,
}

public enum RoundWinReason
{
    TeamElimination,
    HealthTiebreaker,
    DisconnectForfeit,
}

public sealed record RoundResult(
    Team WinningTeam,
    RoundWinReason WinReason,
    int RoundNumber);

public sealed record FighterState(
    int ClientId,
    string DisplayName,
    Team Team,
    FighterRole Role,
    GameVector Position,
    GameVector AimDirection,
    int Health,
    double DashCooldownSeconds,
    double PrimaryAttackCooldownSeconds,
    double RoleAbilityCooldownSeconds)
{
    [JsonIgnore]
    public bool IsDefeated => Health <= 0;
}

public sealed record ProjectileState(
    int ProjectileId,
    int OwnerClientId,
    Team Team,
    ProjectileKind Kind,
    GameVector Position,
    GameVector Direction,
    int Damage,
    double Radius,
    int? VolleyId = null);

public sealed record CombatEffect(
    CombatEffectKind Kind,
    GameVector Position,
    GameVector Direction,
    Team Team,
    double Radius,
    double RemainingSeconds,
    int? SourceClientId = null,
    int? TargetClientId = null);

[method: JsonConstructor]
public sealed record MatchSnapshot(
    IReadOnlyList<FighterState> Fighters,
    IReadOnlyList<ProjectileState> Projectiles,
    IReadOnlyList<CombatEffect> Effects,
    int RoundNumber = 1,
    double RoundTimeRemainingSeconds = MatchRules.RoundDurationSeconds,
    int RedRoundWins = 0,
    int BlueRoundWins = 0,
    MatchPhase Phase = MatchPhase.InRound,
    RoundResult? RoundResult = null,
    Team? MatchWinner = null)
{
    public MatchSnapshot(IReadOnlyList<FighterState> fighters)
        : this(fighters, [], [])
    {
    }
}

public sealed class MatchSimulation
{
    private static readonly GameVector[] SpawnPositions =
    [
        new(-120, -80),
        new(-120, 80),
        new(120, -80),
        new(120, 80),
    ];

    private readonly Dictionary<int, PlayerInput> _inputs = [];
    private readonly Dictionary<int, HashSet<int>> _volleyHits = [];
    private int _nextProjectileId = 1;
    private int _nextVolleyId = 1;
    private double _roundTransitionRemainingSeconds;

    private MatchSimulation(MatchSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public MatchSnapshot Snapshot { get; private set; }

    public static MatchSimulation Start(LobbyState lobby)
    {
        if (!lobby.StartEligibility.CanStart)
        {
            throw new InvalidOperationException("A Match can start only from a valid Lobby.");
        }

        var fighters = lobby.Clients
            .OrderBy(client => client.ClientId)
            .Select((client, index) => new FighterState(
                client.ClientId,
                client.DisplayName,
                client.Team!.Value,
                client.Role!.Value,
                SpawnPositions[index],
                new GameVector(1, 0),
                MatchRules.GetStartingHealth(client.Role.Value),
                0,
                0,
                0))
            .ToArray();

        return new MatchSimulation(new MatchSnapshot(fighters));
    }

    public void SetInput(int clientId, PlayerInput input)
    {
        _inputs[clientId] = input with
        {
            MoveDirection = input.MoveDirection.NormalizeOrZero(),
            AimDirection = input.AimDirection.NormalizeOrZero(),
        };
    }

    public void Tick()
    {
        if (Snapshot.Phase == MatchPhase.RoundComplete)
        {
            _roundTransitionRemainingSeconds = Math.Max(
                0,
                _roundTransitionRemainingSeconds - MatchRules.FixedDeltaSeconds);

            if (_roundTransitionRemainingSeconds <= 0.000001)
            {
                StartNextRound();
            }

            return;
        }

        if (Snapshot.Phase == MatchPhase.MatchComplete)
        {
            return;
        }

        var effects = Snapshot.Effects
            .Select(effect => effect with
            {
                RemainingSeconds = effect.RemainingSeconds - MatchRules.FixedDeltaSeconds,
            })
            .Where(effect => effect.RemainingSeconds > 0)
            .ToList();
        var fighters = Snapshot.Fighters
            .Select(TickFighterMovementAndCooldowns)
            .ToDictionary(fighter => fighter.ClientId);
        var projectiles = Snapshot.Projectiles.ToList();

        foreach (var fighter in fighters.Values.OrderBy(fighter => fighter.ClientId).ToArray())
        {
            if (fighter.IsDefeated)
            {
                continue;
            }

            var input = GetInput(fighter);

            if (input.PrimaryAttack && fighter.PrimaryAttackCooldownSeconds <= 0)
            {
                if (fighter.Role == FighterRole.Melee)
                {
                    ApplyMeleeFrontalStrike(fighter, fighters, effects);
                    fighters[fighter.ClientId] = fighters[fighter.ClientId] with
                    {
                        PrimaryAttackCooldownSeconds = MatchRules.MeleeFrontalStrikeCooldownSeconds,
                    };
                }
                else
                {
                    SpawnRangedSingleArrowShot(fighter, projectiles, effects);
                    fighters[fighter.ClientId] = fighters[fighter.ClientId] with
                    {
                        PrimaryAttackCooldownSeconds = MatchRules.RangedSingleArrowShotCooldownSeconds,
                    };
                }
            }

            var currentFighter = fighters[fighter.ClientId];
            if (input.RoleAbility && currentFighter.RoleAbilityCooldownSeconds <= 0)
            {
                if (currentFighter.Role == FighterRole.Melee)
                {
                    ApplyMeleeAreaSlash(currentFighter, fighters, effects);
                    fighters[currentFighter.ClientId] = fighters[currentFighter.ClientId] with
                    {
                        RoleAbilityCooldownSeconds = MatchRules.MeleeAreaSlashCooldownSeconds,
                    };
                }
                else
                {
                    SpawnRangedConeVolley(currentFighter, projectiles, effects);
                    fighters[currentFighter.ClientId] = fighters[currentFighter.ClientId] with
                    {
                        RoleAbilityCooldownSeconds = MatchRules.RangedConeVolleyCooldownSeconds,
                    };
                }
            }
        }

        projectiles = TickProjectiles(projectiles, fighters, effects);
        Snapshot = CompleteRoundIfNeeded(Snapshot with
        {
            Fighters = fighters.Values.OrderBy(fighter => fighter.ClientId).ToArray(),
            Projectiles = projectiles.ToArray(),
            Effects = effects.ToArray(),
            RoundTimeRemainingSeconds = Math.Max(
                0,
                Snapshot.RoundTimeRemainingSeconds - MatchRules.FixedDeltaSeconds),
        });
    }

    public void OverrideFighterPositionForTesting(int clientId, GameVector position)
    {
        Snapshot = Snapshot with
        {
            Fighters = Snapshot.Fighters
                .Select(fighter => fighter.ClientId == clientId
                    ? fighter with { Position = ClampToArena(position) }
                    : fighter)
                .ToArray(),
        };
    }

    public void CompleteMatchByDisconnectForfeit(int disconnectedClientId)
    {
        if (Snapshot.Phase == MatchPhase.MatchComplete)
        {
            return;
        }

        var disconnectedFighter = Snapshot.Fighters.SingleOrDefault(fighter =>
            fighter.ClientId == disconnectedClientId);
        if (disconnectedFighter is null)
        {
            return;
        }

        var winningTeam = disconnectedFighter.Team == Team.Red
            ? Team.Blue
            : Team.Red;

        Snapshot = Snapshot with
        {
            Projectiles = [],
            Phase = MatchPhase.MatchComplete,
            RoundResult = new RoundResult(
                winningTeam,
                RoundWinReason.DisconnectForfeit,
                Snapshot.RoundNumber),
            RedRoundWins = winningTeam == Team.Red
                ? MatchRules.RoundsToWinMatch
                : Snapshot.RedRoundWins,
            BlueRoundWins = winningTeam == Team.Blue
                ? MatchRules.RoundsToWinMatch
                : Snapshot.BlueRoundWins,
            MatchWinner = winningTeam,
        };
    }

    private FighterState TickFighterMovementAndCooldowns(FighterState fighter)
    {
        var input = GetInput(fighter);
        var aimDirection = input.AimDirection.Length == 0
            ? fighter.AimDirection
            : input.AimDirection;
        var dashCooldown = Math.Max(
            0,
            fighter.DashCooldownSeconds - MatchRules.FixedDeltaSeconds);
        var primaryCooldown = Math.Max(
            0,
            fighter.PrimaryAttackCooldownSeconds - MatchRules.FixedDeltaSeconds);
        var abilityCooldown = Math.Max(
            0,
            fighter.RoleAbilityCooldownSeconds - MatchRules.FixedDeltaSeconds);

        if (fighter.IsDefeated)
        {
            return fighter with
            {
                AimDirection = aimDirection,
                DashCooldownSeconds = dashCooldown,
                PrimaryAttackCooldownSeconds = primaryCooldown,
                RoleAbilityCooldownSeconds = abilityCooldown,
            };
        }

        var position = fighter.Position +
            (input.MoveDirection * MatchRules.FighterMoveSpeed * MatchRules.FixedDeltaSeconds);

        if (input.Dash && fighter.DashCooldownSeconds <= 0)
        {
            position += aimDirection * MatchRules.UniversalDashDistance;
            dashCooldown = MatchRules.UniversalDashCooldownSeconds;
        }

        return fighter with
        {
            Position = ClampToArena(position),
            AimDirection = aimDirection,
            DashCooldownSeconds = dashCooldown,
            PrimaryAttackCooldownSeconds = primaryCooldown,
            RoleAbilityCooldownSeconds = abilityCooldown,
        };
    }

    private PlayerInput GetInput(FighterState fighter)
    {
        return _inputs.GetValueOrDefault(
            fighter.ClientId,
            new PlayerInput(GameVector.Zero, fighter.AimDirection, false));
    }

    private static void ApplyMeleeFrontalStrike(
        FighterState source,
        Dictionary<int, FighterState> fighters,
        List<CombatEffect> effects)
    {
        var direction = source.AimDirection.NormalizeOrZero();
        if (direction.Length == 0)
        {
            direction = new GameVector(1, 0);
        }

        effects.Add(new CombatEffect(
            CombatEffectKind.MeleeFrontalStrike,
            source.Position,
            direction,
            source.Team,
            MatchRules.MeleeFrontalStrikeRange,
            MatchRules.CombatEffectLifetimeSeconds,
            source.ClientId));

        var minimumDot = Math.Cos(
            MatchRules.MeleeFrontalStrikeHalfAngleDegrees * Math.PI / 180);

        foreach (var target in fighters.Values.ToArray())
        {
            if (!CanDamage(source, target))
            {
                continue;
            }

            var toTarget = target.Position - source.Position;
            var distance = toTarget.Length;
            if (distance > MatchRules.MeleeFrontalStrikeRange + MatchRules.GetFighterRadius(target.Role))
            {
                continue;
            }

            if (distance > 0 &&
                direction.Dot(toTarget.NormalizeOrZero()) < minimumDot)
            {
                continue;
            }

            DamageFighter(
                source,
                target,
                MatchRules.MeleeFrontalStrikeDamage,
                fighters,
                effects);
        }
    }

    private static void ApplyMeleeAreaSlash(
        FighterState source,
        Dictionary<int, FighterState> fighters,
        List<CombatEffect> effects)
    {
        effects.Add(new CombatEffect(
            CombatEffectKind.MeleeAreaSlash,
            source.Position,
            source.AimDirection,
            source.Team,
            MatchRules.MeleeAreaSlashRadius,
            MatchRules.CombatEffectLifetimeSeconds,
            source.ClientId));

        foreach (var target in fighters.Values.ToArray())
        {
            if (!CanDamage(source, target))
            {
                continue;
            }

            if (source.Position.DistanceTo(target.Position) >
                MatchRules.MeleeAreaSlashRadius + MatchRules.GetFighterRadius(target.Role))
            {
                continue;
            }

            DamageFighter(
                source,
                target,
                MatchRules.MeleeAreaSlashDamage,
                fighters,
                effects);
        }
    }

    private void SpawnRangedSingleArrowShot(
        FighterState source,
        List<ProjectileState> projectiles,
        List<CombatEffect> effects)
    {
        var direction = source.AimDirection.NormalizeOrZero();
        if (direction.Length == 0)
        {
            direction = new GameVector(1, 0);
        }

        projectiles.Add(CreateProjectile(
            source,
            ProjectileKind.RangedSingleArrowShot,
            direction,
            MatchRules.RangedSingleArrowShotDamage,
            null));
        effects.Add(new CombatEffect(
            CombatEffectKind.RangedSingleArrowShot,
            source.Position,
            direction,
            source.Team,
            18,
            MatchRules.CombatEffectLifetimeSeconds,
            source.ClientId));
    }

    private void SpawnRangedConeVolley(
        FighterState source,
        List<ProjectileState> projectiles,
        List<CombatEffect> effects)
    {
        var direction = source.AimDirection.NormalizeOrZero();
        if (direction.Length == 0)
        {
            direction = new GameVector(1, 0);
        }

        var volleyId = _nextVolleyId++;
        _volleyHits[volleyId] = [];
        var firstAngle = -MatchRules.RangedConeVolleySpreadDegrees / 2;
        var angleStep = MatchRules.RangedConeVolleySpreadDegrees /
            (MatchRules.RangedConeVolleyArrowCount - 1);

        for (var index = 0; index < MatchRules.RangedConeVolleyArrowCount; index++)
        {
            projectiles.Add(CreateProjectile(
                source,
                ProjectileKind.RangedConeVolleyArrow,
                Rotate(direction, firstAngle + (angleStep * index)),
                MatchRules.RangedConeVolleyDamage,
                volleyId));
        }

        effects.Add(new CombatEffect(
            CombatEffectKind.RangedConeVolley,
            source.Position,
            direction,
            source.Team,
            48,
            MatchRules.CombatEffectLifetimeSeconds,
            source.ClientId));
    }

    private ProjectileState CreateProjectile(
        FighterState source,
        ProjectileKind kind,
        GameVector direction,
        int damage,
        int? volleyId)
    {
        var normalizedDirection = direction.NormalizeOrZero();
        var startPosition = source.Position +
            (normalizedDirection * (MatchRules.GetFighterRadius(source.Role) + 2));

        return new ProjectileState(
            _nextProjectileId++,
            source.ClientId,
            source.Team,
            kind,
            startPosition,
            normalizedDirection,
            damage,
            MatchRules.ProjectileRadius,
            volleyId);
    }

    private List<ProjectileState> TickProjectiles(
        List<ProjectileState> projectiles,
        Dictionary<int, FighterState> fighters,
        List<CombatEffect> effects)
    {
        var activeProjectiles = new List<ProjectileState>();

        foreach (var projectile in projectiles)
        {
            var moved = projectile with
            {
                Position = projectile.Position +
                    (projectile.Direction * MatchRules.ProjectileSpeed * MatchRules.FixedDeltaSeconds),
            };

            if (moved.Position.Length >= MatchRules.ArenaRadius)
            {
                continue;
            }

            var hitTarget = fighters.Values
                .Where(target =>
                    target.Team != moved.Team &&
                    !target.IsDefeated &&
                    moved.Position.DistanceTo(target.Position) <=
                    moved.Radius + MatchRules.GetFighterRadius(target.Role))
                .OrderBy(target => moved.Position.DistanceTo(target.Position))
                .FirstOrDefault();

            if (hitTarget is null)
            {
                activeProjectiles.Add(moved);
                continue;
            }

            if (moved.VolleyId is null ||
                _volleyHits.GetValueOrDefault(moved.VolleyId.Value)?.Add(hitTarget.ClientId) == true)
            {
                DamageFighter(
                    fighters[moved.OwnerClientId],
                    hitTarget,
                    moved.Damage,
                    fighters,
                    effects);
            }
        }

        return activeProjectiles;
    }

    private static bool CanDamage(FighterState source, FighterState target)
    {
        return source.ClientId != target.ClientId &&
            source.Team != target.Team &&
            !source.IsDefeated &&
            !target.IsDefeated;
    }

    private static void DamageFighter(
        FighterState source,
        FighterState target,
        int damage,
        Dictionary<int, FighterState> fighters,
        List<CombatEffect> effects)
    {
        var newHealth = Math.Max(0, target.Health - damage);
        var updatedTarget = target with { Health = newHealth };
        fighters[target.ClientId] = updatedTarget;

        effects.Add(new CombatEffect(
            CombatEffectKind.Hit,
            target.Position,
            source.AimDirection,
            target.Team,
            MatchRules.GetFighterRadius(target.Role) + 4,
            MatchRules.CombatEffectLifetimeSeconds,
            source.ClientId,
            target.ClientId));

        if (target.Health > 0 && newHealth == 0)
        {
            effects.Add(new CombatEffect(
                CombatEffectKind.Death,
                target.Position,
                source.AimDirection,
                target.Team,
                MatchRules.GetFighterRadius(target.Role) + 8,
                MatchRules.CombatEffectLifetimeSeconds,
                source.ClientId,
                target.ClientId));
        }
    }

    private void StartNextRound()
    {
        _roundTransitionRemainingSeconds = 0;
        _volleyHits.Clear();

        Snapshot = Snapshot with
        {
            Fighters = Snapshot.Fighters
                .OrderBy(fighter => fighter.ClientId)
                .Select((fighter, index) => fighter with
                {
                    Position = SpawnPositions[index],
                    AimDirection = new GameVector(1, 0),
                    Health = MatchRules.GetStartingHealth(fighter.Role),
                    DashCooldownSeconds = 0,
                    PrimaryAttackCooldownSeconds = 0,
                    RoleAbilityCooldownSeconds = 0,
                })
                .ToArray(),
            Projectiles = [],
            Effects = [],
            RoundNumber = Snapshot.RoundNumber + 1,
            RoundTimeRemainingSeconds = MatchRules.RoundDurationSeconds,
            Phase = MatchPhase.InRound,
            RoundResult = null,
            MatchWinner = null,
        };
    }

    private MatchSnapshot CompleteRoundIfNeeded(MatchSnapshot snapshot)
    {
        var redAlive = snapshot.Fighters.Any(fighter =>
            fighter.Team == Team.Red &&
            !fighter.IsDefeated);
        var blueAlive = snapshot.Fighters.Any(fighter =>
            fighter.Team == Team.Blue &&
            !fighter.IsDefeated);

        return (redAlive, blueAlive) switch
        {
            (true, false) => CompleteRound(snapshot, Team.Red, RoundWinReason.TeamElimination),
            (false, true) => CompleteRound(snapshot, Team.Blue, RoundWinReason.TeamElimination),
            (true, true) when snapshot.RoundTimeRemainingSeconds <= 0 =>
                CompleteRound(snapshot, GetHealthTiebreakerWinner(snapshot), RoundWinReason.HealthTiebreaker),
            _ => snapshot,
        };
    }

    private static Team GetHealthTiebreakerWinner(MatchSnapshot snapshot)
    {
        var redHealth = GetCombinedHealth(snapshot, Team.Red);
        var blueHealth = GetCombinedHealth(snapshot, Team.Blue);

        return redHealth >= blueHealth
            ? Team.Red
            : Team.Blue;
    }

    private static int GetCombinedHealth(MatchSnapshot snapshot, Team team)
    {
        return snapshot.Fighters
            .Where(fighter => fighter.Team == team)
            .Sum(fighter => fighter.Health);
    }

    private MatchSnapshot CompleteRound(
        MatchSnapshot snapshot,
        Team winningTeam,
        RoundWinReason reason)
    {
        var redRoundWins = snapshot.RedRoundWins + (winningTeam == Team.Red ? 1 : 0);
        var blueRoundWins = snapshot.BlueRoundWins + (winningTeam == Team.Blue ? 1 : 0);
        var matchWinner = redRoundWins >= MatchRules.RoundsToWinMatch
            ? Team.Red
            : blueRoundWins >= MatchRules.RoundsToWinMatch
                ? Team.Blue
                : (Team?)null;

        _roundTransitionRemainingSeconds = matchWinner is null
            ? MatchRules.RoundTransitionSeconds
            : 0;

        return snapshot with
        {
            Projectiles = [],
            Phase = matchWinner is null
                ? MatchPhase.RoundComplete
                : MatchPhase.MatchComplete,
            RoundResult = new RoundResult(winningTeam, reason, snapshot.RoundNumber),
            RedRoundWins = redRoundWins,
            BlueRoundWins = blueRoundWins,
            MatchWinner = matchWinner,
        };
    }

    private static GameVector Rotate(GameVector vector, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new GameVector(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos)).NormalizeOrZero();
    }

    private static GameVector ClampToArena(GameVector position)
    {
        return position.Length <= MatchRules.ArenaRadius
            ? position
            : position.NormalizeOrZero() * MatchRules.ArenaRadius;
    }
}
