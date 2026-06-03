namespace AvaloniaBattleground.Core;

public sealed record RoundResult(
    Team WinningTeam,
    RoundWinReason WinReason,
    int RoundNumber);
