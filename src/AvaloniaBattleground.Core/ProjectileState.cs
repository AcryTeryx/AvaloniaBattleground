namespace AvaloniaBattleground.Core;

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
