namespace AvaloniaBattleground.Core;

public sealed record CombatEffect(
    CombatEffectKind Kind,
    GameVector Position,
    GameVector Direction,
    Team Team,
    double Radius,
    double RemainingSeconds,
    int? SourceClientId = null,
    int? TargetClientId = null);
