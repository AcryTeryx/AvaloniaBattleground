using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.App.ViewModels;

public sealed class LobbyClientItemViewModel(
    string displayName,
    bool isHost,
    Team? team,
    FighterRole? role)
{
    public string DisplayName { get; } = displayName;

    public bool IsHost { get; } = isHost;

    public string HostMarker => IsHost ? "Host" : string.Empty;

    public string TeamDisplay => team?.ToString() ?? "Unassigned";

    public string RoleDisplay => role?.ToString() ?? "Unassigned";
}
