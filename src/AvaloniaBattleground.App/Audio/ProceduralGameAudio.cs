using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaBattleground.App.Audio;

public sealed class ProceduralGameAudio : IGameAudio
{
    private static readonly TimeSpan CueThrottle = TimeSpan.FromMilliseconds(80);

    private readonly ProceduralAudioAssets _assets = new();
    private readonly PlatformAudioPlayer _player = new();
    private readonly Dictionary<GameAudioCue, DateTimeOffset> _lastCueStartedAt = [];
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _musicStopping;
    private GameMusicTrack? _currentMusicTrack;

    public void SwitchMusic(GameMusicTrack track)
    {
        CancellationTokenSource musicStopping;
        string musicPath;

        lock (_syncRoot)
        {
            if (_currentMusicTrack == track)
            {
                return;
            }

            _musicStopping?.Cancel();
            _musicStopping?.Dispose();
            _musicStopping = new CancellationTokenSource();
            _currentMusicTrack = track;
            musicStopping = _musicStopping;
            musicPath = _assets.GetMusicPath(track);
        }

        _ = Task.Run(() => RunMusicLoopAsync(musicPath, musicStopping.Token));
    }

    public void PlayCue(GameAudioCue cue)
    {
        string cuePath;

        lock (_syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            if (_lastCueStartedAt.TryGetValue(cue, out var lastStartedAt) &&
                now - lastStartedAt < CueThrottle)
            {
                return;
            }

            _lastCueStartedAt[cue] = now;
            cuePath = _assets.GetCuePath(cue);
        }

        _ = Task.Run(() => _player.PlayAsync(cuePath, CancellationToken.None));
    }

    private async Task RunMusicLoopAsync(string musicPath, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _player.PlayAsync(musicPath, cancellationToken);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private sealed class ProceduralAudioAssets
    {
        private const int SampleRate = 44100;
        private readonly string _assetDirectory;

        public ProceduralAudioAssets()
        {
            _assetDirectory = Path.Combine(
                Path.GetTempPath(),
                "AvaloniaBattleground",
                "procedural-audio-v1");
            Directory.CreateDirectory(_assetDirectory);
            EnsureAssets();
        }

        public string GetMusicPath(GameMusicTrack track)
        {
            return Path.Combine(_assetDirectory, $"music-{track}.wav");
        }

        public string GetCuePath(GameAudioCue cue)
        {
            return Path.Combine(_assetDirectory, $"cue-{cue}.wav");
        }

        private void EnsureAssets()
        {
            WriteWavIfMissing(
                GetMusicPath(GameMusicTrack.Lobby),
                CreateMusicLoop([196, 246.94, 293.66, 246.94], 0.18, 4.0));
            WriteWavIfMissing(
                GetMusicPath(GameMusicTrack.Battle),
                CreateMusicLoop([220, 277.18, 329.63, 392], 0.24, 3.2));

            WriteCue(GameAudioCue.LobbyClientJoined, [(523.25, 0.08), (659.25, 0.10)]);
            WriteCue(GameAudioCue.LobbyClientLeft, [(392, 0.08), (293.66, 0.12)]);
            WriteCue(GameAudioCue.ConnectionError, [(146.83, 0.10), (138.59, 0.10), (130.81, 0.14)]);
            WriteCue(GameAudioCue.UniversalDash, [(880, 0.08), (1174.66, 0.08)]);
            WriteCue(GameAudioCue.PrimaryAttack, [(440, 0.07), (659.25, 0.05)]);
            WriteCue(GameAudioCue.RoleAbility, [(349.23, 0.10), (523.25, 0.10), (698.46, 0.12)]);
            WriteCue(GameAudioCue.Hit, [(196, 0.08)]);
            WriteCue(GameAudioCue.FighterDefeated, [(220, 0.10), (174.61, 0.16)]);
            WriteCue(GameAudioCue.KillAnnouncement, [(523.25, 0.10), (659.25, 0.10), (783.99, 0.16)]);
            WriteCue(GameAudioCue.RoundComplete, [(329.63, 0.12), (392, 0.12), (493.88, 0.18)]);
            WriteCue(GameAudioCue.MatchComplete, [(392, 0.12), (493.88, 0.12), (587.33, 0.12), (783.99, 0.22)]);
        }

        private void WriteCue(GameAudioCue cue, IReadOnlyList<(double Frequency, double DurationSeconds)> tones)
        {
            WriteWavIfMissing(GetCuePath(cue), CreateToneSequence(tones, 0.35));
        }

        private static short[] CreateMusicLoop(
            IReadOnlyList<double> notes,
            double volume,
            double durationSeconds)
        {
            var sampleCount = (int)(SampleRate * durationSeconds);
            var samples = new short[sampleCount];
            var beatSamples = SampleRate / 2;

            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var note = notes[(sampleIndex / beatSamples) % notes.Count];
                var time = (double)sampleIndex / SampleRate;
                var envelope = 0.72 + (0.28 * Math.Sin(2 * Math.PI * time / durationSeconds));
                var carrier = Math.Sin(2 * Math.PI * note * time);
                var upper = Math.Sin(2 * Math.PI * note * 1.5 * time) * 0.35;
                var bass = Math.Sin(2 * Math.PI * note / 2 * time) * 0.22;
                samples[sampleIndex] = ToSample((carrier + upper + bass) * volume * envelope);
            }

            return samples;
        }

        private static short[] CreateToneSequence(
            IReadOnlyList<(double Frequency, double DurationSeconds)> tones,
            double volume)
        {
            var totalSamples = tones.Sum(tone => (int)(tone.DurationSeconds * SampleRate));
            var samples = new short[totalSamples];
            var offset = 0;

            foreach (var (frequency, durationSeconds) in tones)
            {
                var toneSamples = (int)(durationSeconds * SampleRate);
                for (var sampleIndex = 0; sampleIndex < toneSamples; sampleIndex++)
                {
                    var time = (double)sampleIndex / SampleRate;
                    var position = toneSamples == 0 ? 0 : (double)sampleIndex / toneSamples;
                    var envelope = Math.Min(1, Math.Min(position * 10, (1 - position) * 8));
                    samples[offset + sampleIndex] = ToSample(
                        Math.Sin(2 * Math.PI * frequency * time) * volume * envelope);
                }

                offset += toneSamples;
            }

            return samples;
        }

        private static short ToSample(double value)
        {
            return (short)(Math.Clamp(value, -1, 1) * short.MaxValue);
        }

        private static void WriteWavIfMissing(string path, short[] samples)
        {
            if (File.Exists(path))
            {
                return;
            }

            using var file = File.Create(path);
            using var writer = new BinaryWriter(file);
            var dataSize = samples.Length * sizeof(short);

            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(dataSize);

            foreach (var sample in samples)
            {
                writer.Write(sample);
            }
        }
    }

    private sealed class PlatformAudioPlayer
    {
        private readonly string? _playerCommand = FindPlayerCommand();

        public async Task PlayAsync(string path, CancellationToken cancellationToken)
        {
            if (_playerCommand is null)
            {
                return;
            }

            using var process = StartPlayer(path);
            if (process is null)
            {
                return;
            }

            using var cancellation = cancellationToken.Register(() => KillProcess(process));

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Process? StartPlayer(string path)
        {
            var playerCommand = _playerCommand;
            if (playerCommand is null)
            {
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = playerCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                if (OperatingSystem.IsWindows())
                {
                    var escapedPath = path.Replace("'", "''", StringComparison.Ordinal);
                    startInfo.ArgumentList.Add("-NoProfile");
                    startInfo.ArgumentList.Add("-ExecutionPolicy");
                    startInfo.ArgumentList.Add("Bypass");
                    startInfo.ArgumentList.Add("-Command");
                    startInfo.ArgumentList.Add(
                        $"(New-Object System.Media.SoundPlayer '{escapedPath}').PlaySync()");
                }
                else if (Path.GetFileName(playerCommand).Equals("aplay", StringComparison.Ordinal))
                {
                    startInfo.ArgumentList.Add("-q");
                    startInfo.ArgumentList.Add(path);
                }
                else
                {
                    startInfo.ArgumentList.Add(path);
                }

                return Process.Start(startInfo);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        private static void KillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static string? FindPlayerCommand()
        {
            if (OperatingSystem.IsWindows())
            {
                return FindExecutable("pwsh.exe", "powershell.exe");
            }

            return FindExecutable("pw-play", "paplay", "aplay");
        }

        private static string? FindExecutable(params string[] names)
        {
            var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var name in names)
            {
                if (Path.IsPathRooted(name) && File.Exists(name))
                {
                    return name;
                }

                foreach (var directory in pathDirectories)
                {
                    var candidate = Path.Combine(directory, name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
