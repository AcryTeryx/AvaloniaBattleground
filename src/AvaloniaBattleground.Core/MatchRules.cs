namespace AvaloniaBattleground.Core;

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
