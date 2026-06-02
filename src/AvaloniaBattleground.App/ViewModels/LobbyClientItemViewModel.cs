namespace AvaloniaBattleground.App.ViewModels;

public sealed class LobbyClientItemViewModel(string displayName, bool isHost)
{
    public string DisplayName { get; } = displayName;

    public bool IsHost { get; } = isHost;

    public string HostMarker => IsHost ? "Host" : string.Empty;
}
