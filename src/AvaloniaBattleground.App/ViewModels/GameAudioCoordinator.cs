using AvaloniaBattleground.App.Audio;
using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaBattleground.App.ViewModels;

// Owns all audio decisions for the shell: music-track switching, lobby
// join/leave cues, per-effect combat cues, and round/match result cues. It
// tracks just enough state to de-duplicate repeated snapshots so a cue plays
// once per real event rather than once per received frame.
internal sealed class GameAudioCoordinator(IGameAudio audio)
{
    private HashSet<CombatEffectAudioKey> _activeCombatEffectKeys = [];
    private RoundResultAudioKey? _lastRoundResultAudioKey;
    private GameMusicTrack? _currentMusicTrack;
    private HashSet<int>? _knownLobbyClientIds;

    public void SwitchMusic(GameMusicTrack track)
    {
        if (_currentMusicTrack == track)
        {
            return;
        }

        audio.SwitchMusic(track);
        _currentMusicTrack = track;
    }

    public void PlayConnectionError()
    {
        audio.PlayCue(GameAudioCue.ConnectionError);
    }

    // Clears per-session combat/lobby tracking when a Lobby session ends. The
    // current music track is intentionally preserved so menu music does not
    // restart on the way back to the main menu.
    public void Reset()
    {
        _activeCombatEffectKeys = [];
        _lastRoundResultAudioKey = null;
        _knownLobbyClientIds = null;
    }

    public void HandleLobbySnapshot(LobbyState snapshot)
    {
        var clientIds = snapshot.Clients
            .Select(client => client.ClientId)
            .ToHashSet();

        if (_knownLobbyClientIds is not null)
        {
            foreach (var clientId in clientIds.Except(_knownLobbyClientIds))
            {
                audio.PlayCue(GameAudioCue.LobbyClientJoined);
            }

            foreach (var clientId in _knownLobbyClientIds.Except(clientIds))
            {
                audio.PlayCue(GameAudioCue.LobbyClientLeft);
            }
        }

        _knownLobbyClientIds = clientIds;
    }

    public void HandleMatchSnapshot(MatchSnapshot snapshot, int? localClientId)
    {
        var activeEffectKeys = snapshot.Effects
            .Select(CombatEffectAudioKey.FromEffect)
            .ToHashSet();

        foreach (var effect in snapshot.Effects)
        {
            var effectKey = CombatEffectAudioKey.FromEffect(effect);
            if (_activeCombatEffectKeys.Contains(effectKey))
            {
                continue;
            }

            PlayCuesForEffect(snapshot, effect, localClientId);
        }

        _activeCombatEffectKeys = activeEffectKeys;
        PlayRoundResultCue(snapshot);
    }

    private void PlayCuesForEffect(MatchSnapshot snapshot, CombatEffect effect, int? localClientId)
    {
        var cue = GetCueForEffect(effect);
        if (cue is not null)
        {
            audio.PlayCue(cue.Value);
        }

        if (effect.Kind == CombatEffectKind.Death &&
            IsEnemyFighter(snapshot, effect.TargetClientId, localClientId))
        {
            audio.PlayCue(GameAudioCue.KillAnnouncement);
        }
    }

    private static GameAudioCue? GetCueForEffect(CombatEffect effect)
    {
        return effect.Kind switch
        {
            CombatEffectKind.UniversalDash => GameAudioCue.UniversalDash,
            CombatEffectKind.MeleeFrontalStrike => GameAudioCue.PrimaryAttack,
            CombatEffectKind.RangedSingleArrowShot => GameAudioCue.PrimaryAttack,
            CombatEffectKind.MeleeAreaSlash => GameAudioCue.RoleAbility,
            CombatEffectKind.RangedConeVolley => GameAudioCue.RoleAbility,
            CombatEffectKind.Hit => GameAudioCue.Hit,
            CombatEffectKind.Death => GameAudioCue.FighterDefeated,
            _ => null,
        };
    }

    private static bool IsEnemyFighter(MatchSnapshot snapshot, int? targetClientId, int? localClientId)
    {
        if (targetClientId is null || localClientId is null)
        {
            return false;
        }

        var localFighter = snapshot.Fighters.SingleOrDefault(fighter =>
            fighter.ClientId == localClientId.Value);
        var targetFighter = snapshot.Fighters.SingleOrDefault(fighter =>
            fighter.ClientId == targetClientId.Value);

        return localFighter is not null &&
            targetFighter is not null &&
            localFighter.Team != targetFighter.Team;
    }

    private void PlayRoundResultCue(MatchSnapshot snapshot)
    {
        if (snapshot.RoundResult is null)
        {
            _lastRoundResultAudioKey = null;
            return;
        }

        var key = RoundResultAudioKey.FromSnapshot(snapshot);
        if (_lastRoundResultAudioKey == key)
        {
            return;
        }

        if (snapshot.Phase == MatchPhase.MatchComplete)
        {
            audio.PlayCue(GameAudioCue.MatchComplete);
        }
        else if (snapshot.Phase == MatchPhase.RoundComplete)
        {
            audio.PlayCue(GameAudioCue.RoundComplete);
        }

        _lastRoundResultAudioKey = key;
    }

    private readonly record struct CombatEffectAudioKey(
        CombatEffectKind Kind,
        int? SourceClientId,
        int? TargetClientId,
        GameVector Position,
        double Radius)
    {
        public static CombatEffectAudioKey FromEffect(CombatEffect effect)
        {
            return new CombatEffectAudioKey(
                effect.Kind,
                effect.SourceClientId,
                effect.TargetClientId,
                effect.Position,
                effect.Radius);
        }
    }

    private readonly record struct RoundResultAudioKey(
        MatchPhase Phase,
        Team WinningTeam,
        RoundWinReason WinReason,
        int RoundNumber,
        Team? MatchWinner)
    {
        public static RoundResultAudioKey FromSnapshot(MatchSnapshot snapshot)
        {
            var roundResult = snapshot.RoundResult!;
            return new RoundResultAudioKey(
                snapshot.Phase,
                roundResult.WinningTeam,
                roundResult.WinReason,
                roundResult.RoundNumber,
                snapshot.MatchWinner);
        }
    }
}
