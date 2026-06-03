namespace AvaloniaBattleground.Core;

public enum ProjectileKind
{
    RangedSingleArrowShot,
    RangedConeVolleyArrow,
}

public enum CombatEffectKind
{
    UniversalDash,
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
