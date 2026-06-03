using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Networking;

public interface IMatchHost
{
    bool IsRunning { get; }

    MatchSnapshot? Snapshot { get; }

    StartMatchResult TryStart(LobbyState lobby);

    void SetInput(int clientId, PlayerInput input);

    MatchSnapshot? Tick();

    MatchSnapshot? HandleClientDisconnected(int clientId);
}
