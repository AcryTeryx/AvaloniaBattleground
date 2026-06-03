using System;

namespace AvaloniaBattleground.App.Audio;

public enum GameMusicTrack
{
    Lobby,
    Battle,
}

public enum GameAudioCue
{
    LobbyClientJoined,
    LobbyClientLeft,
    ConnectionError,
    UniversalDash,
    PrimaryAttack,
    RoleAbility,
    Hit,
    FighterDefeated,
    KillAnnouncement,
    RoundComplete,
    MatchComplete,
}

public interface IGameAudio : IDisposable
{
    void SwitchMusic(GameMusicTrack track);

    void PlayCue(GameAudioCue cue);
}

public sealed class SilentGameAudio : IGameAudio
{
    public static SilentGameAudio Instance { get; } = new();

    private SilentGameAudio()
    {
    }

    public void SwitchMusic(GameMusicTrack track)
    {
    }

    public void PlayCue(GameAudioCue cue)
    {
    }

    public void Dispose()
    {
    }
}
