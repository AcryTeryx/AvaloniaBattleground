using System.Text.Json.Serialization;

namespace AvaloniaBattleground.Core;

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
