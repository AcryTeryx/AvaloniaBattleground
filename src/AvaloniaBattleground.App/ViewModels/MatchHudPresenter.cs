using AvaloniaBattleground.Core;
using System;

namespace AvaloniaBattleground.App.ViewModels;

// Pure formatting helpers that turn match and lobby state into the strings the
// HUD binds to. Keeping these stateless and separate makes the shell view model
// thinner and the display logic independently readable.
internal static class MatchHudPresenter
{
    public static string FormatRoundTimer(double seconds)
    {
        var wholeSeconds = Math.Max(0, (int)Math.Ceiling(seconds));
        return FormattableString.Invariant($"{wholeSeconds / 60}:{wholeSeconds % 60:00}");
    }

    public static string GetMatchResultDisplay(MatchSnapshot snapshot)
    {
        if (snapshot.MatchWinner is not null && snapshot.RoundResult is not null)
        {
            var winReason = FormatWinReason(snapshot.RoundResult.WinReason);
            return FormattableString.Invariant(
                $"{snapshot.MatchWinner.Value} wins the match after round {snapshot.RoundResult.RoundNumber} by {winReason}");
        }

        if (snapshot.MatchWinner is not null)
        {
            return $"{snapshot.MatchWinner.Value} wins the match";
        }

        if (snapshot.RoundResult is null)
        {
            return string.Empty;
        }

        var roundWinReason = FormatWinReason(snapshot.RoundResult.WinReason);
        return FormattableString.Invariant(
            $"{snapshot.RoundResult.WinningTeam} wins round {snapshot.RoundResult.RoundNumber} by {roundWinReason}");
    }

    public static string GetStartLockStatus(LobbyStartEligibility eligibility)
    {
        if (eligibility.CanStart)
        {
            return "Ready to start.";
        }

        if (eligibility.LockReasons.Contains(LobbyStartLockReason.FullLobbyRequirement) &&
            eligibility.LockReasons.Contains(LobbyStartLockReason.RoleConstraint))
        {
            return "Waiting for exactly four Clients and valid Team roles.";
        }

        if (eligibility.LockReasons.Contains(LobbyStartLockReason.FullLobbyRequirement))
        {
            return "Waiting for exactly four Clients.";
        }

        return "Waiting for each Team to choose one Melee and one Ranged Fighter.";
    }

    private static string FormatWinReason(RoundWinReason winReason)
    {
        return winReason switch
        {
            RoundWinReason.TeamElimination => "team elimination",
            RoundWinReason.HealthTiebreaker => "health tiebreaker",
            RoundWinReason.DisconnectForfeit => "disconnect forfeit",
            _ => "unknown reason",
        };
    }
}
