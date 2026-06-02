namespace AvaloniaBattleground.Core;

public readonly record struct GameVector(double X, double Y)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public static GameVector Zero { get; } = new(0, 0);

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
    public const double UniversalDashCooldownSeconds = 2.5;
    public const double UniversalDashDistance = 36;
}

public sealed record PlayerInput(
    GameVector MoveDirection,
    GameVector AimDirection,
    bool Dash);

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
        bool dash)
    {
        var move = new GameVector(
            BoolToAxis(moveRight, moveLeft),
            BoolToAxis(moveDown, moveUp)).NormalizeOrZero();
        var aim = new GameVector(
            BoolToAxis(aimRight, aimLeft),
            BoolToAxis(aimDown, aimUp)).NormalizeOrZero();

        return new PlayerInput(move, aim, dash);
    }

    private static int BoolToAxis(bool positive, bool negative)
    {
        return positive == negative
            ? 0
            : positive ? 1 : -1;
    }
}

public sealed record FighterState(
    int ClientId,
    string DisplayName,
    Team Team,
    FighterRole Role,
    GameVector Position,
    GameVector AimDirection,
    double DashCooldownSeconds);

public sealed record MatchSnapshot(IReadOnlyList<FighterState> Fighters);

public sealed class MatchSimulation
{
    private readonly Dictionary<int, PlayerInput> _inputs = [];

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

        var spawnPositions = new[]
        {
            new GameVector(-120, -80),
            new GameVector(-120, 80),
            new GameVector(120, -80),
            new GameVector(120, 80),
        };

        var fighters = lobby.Clients
            .OrderBy(client => client.ClientId)
            .Select((client, index) => new FighterState(
                client.ClientId,
                client.DisplayName,
                client.Team!.Value,
                client.Role!.Value,
                spawnPositions[index],
                new GameVector(1, 0),
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
        Snapshot = Snapshot with
        {
            Fighters = Snapshot.Fighters
                .Select(TickFighter)
                .ToArray(),
        };
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

    private FighterState TickFighter(FighterState fighter)
    {
        var input = _inputs.GetValueOrDefault(
            fighter.ClientId,
            new PlayerInput(GameVector.Zero, fighter.AimDirection, false));
        var aimDirection = input.AimDirection.Length == 0
            ? fighter.AimDirection
            : input.AimDirection;
        var dashCooldown = Math.Max(
            0,
            fighter.DashCooldownSeconds - MatchRules.FixedDeltaSeconds);
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
        };
    }

    private static GameVector ClampToArena(GameVector position)
    {
        return position.Length <= MatchRules.ArenaRadius
            ? position
            : position.NormalizeOrZero() * MatchRules.ArenaRadius;
    }
}
