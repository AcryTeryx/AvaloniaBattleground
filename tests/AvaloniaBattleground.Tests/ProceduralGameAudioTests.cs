using AvaloniaBattleground.App.Audio;

namespace AvaloniaBattleground.Tests;

public sealed class ProceduralGameAudioTests
{
    [Fact]
    public async Task Dispose_cancels_active_music_playback()
    {
        var player = new BlockingGameAudioPlayer();
        using var audio = new ProceduralGameAudio(new FixedGameAudioAssetCatalog(), player);

        audio.SwitchMusic(GameMusicTrack.Lobby);
        var playback = await player.WaitForPlaybackAsync();

        Assert.Equal("Lobby.wav", playback.Path);

        audio.Dispose();

        await playback.WaitForCancellationAsync();
    }

    [Fact]
    public async Task Switching_music_cancels_previous_music_playback()
    {
        var player = new BlockingGameAudioPlayer();
        using var audio = new ProceduralGameAudio(new FixedGameAudioAssetCatalog(), player);

        audio.SwitchMusic(GameMusicTrack.Lobby);
        var lobbyPlayback = await player.WaitForPlaybackAsync();

        Assert.Equal("Lobby.wav", lobbyPlayback.Path);

        audio.SwitchMusic(GameMusicTrack.Battle);

        await lobbyPlayback.WaitForCancellationAsync();
    }

    private sealed class FixedGameAudioAssetCatalog : IGameAudioAssetCatalog
    {
        public string GetMusicPath(GameMusicTrack track)
        {
            return $"{track}.wav";
        }

        public string GetCuePath(GameAudioCue cue)
        {
            return $"{cue}.wav";
        }
    }

    private sealed class BlockingGameAudioPlayer : IGameAudioPlayer
    {
        private readonly Queue<Playback> _startedPlaybacks = [];
        private readonly Queue<TaskCompletionSource<Playback>> _waitingPlaybacks = [];
        private readonly object _syncRoot = new();

        public Task<Playback> WaitForPlaybackAsync()
        {
            lock (_syncRoot)
            {
                if (_startedPlaybacks.TryDequeue(out var startedPlayback))
                {
                    return Task.FromResult(startedPlayback);
                }

                var playback = new TaskCompletionSource<Playback>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _waitingPlaybacks.Enqueue(playback);
                return playback.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        public async Task PlayAsync(string path, CancellationToken cancellationToken)
        {
            var playback = new Playback(path, cancellationToken);

            lock (_syncRoot)
            {
                if (_waitingPlaybacks.TryDequeue(out var pendingPlayback))
                {
                    pendingPlayback.SetResult(playback);
                }
                else
                {
                    _startedPlaybacks.Enqueue(playback);
                }
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private sealed class Playback
    {
        private readonly TaskCompletionSource _cancellation = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Playback(string path, CancellationToken cancellationToken)
        {
            Path = path;

            if (cancellationToken.IsCancellationRequested)
            {
                _cancellation.SetResult();
                return;
            }

            cancellationToken.Register(() => _cancellation.TrySetResult());
        }

        public string Path { get; }

        public Task WaitForCancellationAsync()
        {
            return _cancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
