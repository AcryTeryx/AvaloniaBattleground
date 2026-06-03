using System.Text.Json.Serialization;

namespace AvaloniaBattleground.Core;

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
