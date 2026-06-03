using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Networking;

public sealed class MatchHost : IMatchHost
{
    private MatchSimulation? _simulation;

    public bool IsRunning => _simulation is not null;

    public MatchSnapshot? Snapshot => _simulation?.Snapshot;

    public StartMatchResult TryStart(LobbyState lobby)
    {
        if (_simulation is not null)
        {
            return StartMatchResult.Failure(
                StartMatchFailureReason.AlreadyStarted,
                "The Match has already started.");
        }

        if (!lobby.StartEligibility.CanStart)
        {
            return StartMatchResult.Failure(
                StartMatchFailureReason.LobbyNotReady,
                "The Lobby must have exactly four Clients and valid Team roles.");
        }

        _simulation = MatchSimulation.Start(lobby);
        return StartMatchResult.Success(_simulation.Snapshot);
    }

    public void SetInput(int clientId, PlayerInput input)
    {
        _simulation?.SetInput(clientId, input);
    }

    public MatchSnapshot? Tick()
    {
        if (_simulation is null)
        {
            return null;
        }

        _simulation.Tick();
        return _simulation.Snapshot;
    }

    public MatchSnapshot? HandleClientDisconnected(int clientId)
    {
        if (_simulation is null)
        {
            return null;
        }

        _simulation.CompleteMatchByDisconnectForfeit(clientId);
        return _simulation.Snapshot;
    }
}
