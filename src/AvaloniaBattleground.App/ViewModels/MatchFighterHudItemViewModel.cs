using AvaloniaBattleground.Core;
using System;
using System.Globalization;

namespace AvaloniaBattleground.App.ViewModels;

public sealed class MatchFighterHudItemViewModel
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private MatchFighterHudItemViewModel(
        int clientId,
        string displayName,
        string teamDisplay,
        string roleDisplay,
        string healthDisplay,
        string primaryCooldownDisplay,
        string abilityCooldownDisplay,
        string stateDisplay)
    {
        ClientId = clientId;
        DisplayName = displayName;
        TeamDisplay = teamDisplay;
        RoleDisplay = roleDisplay;
        HealthDisplay = healthDisplay;
        PrimaryCooldownDisplay = primaryCooldownDisplay;
        AbilityCooldownDisplay = abilityCooldownDisplay;
        StateDisplay = stateDisplay;
    }

    public int ClientId { get; }

    public string DisplayName { get; }

    public string TeamDisplay { get; }

    public string RoleDisplay { get; }

    public string HealthDisplay { get; }

    public string PrimaryCooldownDisplay { get; }

    public string AbilityCooldownDisplay { get; }

    public string StateDisplay { get; }

    public static MatchFighterHudItemViewModel FromFighter(FighterState fighter)
    {
        return new MatchFighterHudItemViewModel(
            fighter.ClientId,
            fighter.DisplayName,
            fighter.Team.ToString(),
            fighter.Role.ToString(),
            $"{fighter.Health}/{MatchRules.GetStartingHealth(fighter.Role)}",
            FormatCooldown("Primary", fighter.PrimaryAttackCooldownSeconds),
            FormatCooldown("Ability", fighter.RoleAbilityCooldownSeconds),
            fighter.IsDefeated ? "Spectator" : "Active");
    }

    private static string FormatCooldown(string label, double remainingSeconds)
    {
        if (remainingSeconds <= 0)
        {
            return $"{label} ready";
        }

        var roundedUpSeconds = Math.Ceiling(remainingSeconds * 10) / 10;
        return $"{label} {roundedUpSeconds.ToString("0.0", InvariantCulture)}s";
    }
}
