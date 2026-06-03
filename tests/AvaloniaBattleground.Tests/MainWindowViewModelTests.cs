using AvaloniaBattleground.App.ViewModels;
using AvaloniaBattleground.App.Audio;
using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;

namespace AvaloniaBattleground.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Shell_starts_on_main_menu_screen()
    {
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher());

        Assert.Equal(AppScreen.MainMenu, viewModel.CurrentScreen);
        Assert.Equal("Main Menu", viewModel.CurrentScreenTitle);
    }

    [Fact]
    public void Shell_initializes_from_stored_local_display_name()
    {
        var profilePath = CreateProfilePath();
        new LocalProfileStore(profilePath).Save(new LocalProfile("Acryteryx"));

        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(profilePath),
            new RecordingApplicationShell(),
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher());

        Assert.Equal(AppScreen.MainMenu, viewModel.CurrentScreen);
        Assert.Equal("Acryteryx", viewModel.CurrentDisplayName);
        Assert.Equal("Acryteryx", viewModel.DisplayNameInput);
    }

    [Fact]
    public void Shell_starts_Lobby_music_on_main_menu()
    {
        var audio = new RecordingGameAudio();

        _ = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher(),
            audio);

        Assert.Equal([GameMusicTrack.Lobby], audio.MusicTracks);
    }

    [Fact]
    public async Task Shell_saves_edited_display_name_before_host_lobby_navigation()
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var hostSession = new FakeHostLobbySession(
            ["192.168.1.10"],
            54321,
            new LobbyState([new LobbyClient(1, "Acryteryx", true)]));
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            store,
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher())
        {
            DisplayNameInput = "  Acryteryx  ",
        };

        await viewModel.HostMatchCommand.ExecuteAsync(null);

        Assert.Equal(AppScreen.Lobby, viewModel.CurrentScreen);
        Assert.True(viewModel.IsHostLobby);
        Assert.Equal("Lobby", viewModel.CurrentScreenTitle);
        Assert.Equal("192.168.1.10", viewModel.HostAddressesDisplay);
        Assert.Equal("54321", viewModel.HostPortDisplay);
        Assert.Equal("Acryteryx", viewModel.CurrentDisplayName);
        Assert.Equal("Acryteryx", store.Load().DisplayName);
        Assert.Equal("Acryteryx", Assert.Single(networkService.StartedHostDisplayNames));
        Assert.Equal("Acryteryx", Assert.Single(viewModel.LobbyClients).DisplayName);
    }

    [Fact]
    public void Shell_saves_edited_display_name_before_showing_join_screen()
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var viewModel = new MainWindowViewModel(
            store,
            new RecordingApplicationShell(),
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher())
        {
            DisplayNameInput = "  Joining Player  ",
        };

        viewModel.JoinMatchCommand.Execute(null);

        Assert.Equal(AppScreen.Join, viewModel.CurrentScreen);
        Assert.Equal("Join Match", viewModel.CurrentScreenTitle);
        Assert.Equal("Joining Player", viewModel.CurrentDisplayName);
        Assert.Equal("Joining Player", store.Load().DisplayName);
    }

    [Fact]
    public async Task Shell_saves_display_name_before_joining_lobby()
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var clientSession = new FakeClientLobbySession(
            new LobbyState(
            [
                new LobbyClient(1, "Host Player", true),
                new LobbyClient(2, "Joining Player", false),
            ]));
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            store,
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher())
        {
            DisplayNameInput = "Joining Player",
        };

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        var request = Assert.Single(networkService.JoinRequests);
        Assert.Equal("127.0.0.1", request.HostAddress);
        Assert.Equal(54321, request.Port);
        Assert.Equal("Joining Player", request.DisplayName);
        Assert.Equal(AppScreen.Lobby, viewModel.CurrentScreen);
        Assert.False(viewModel.IsHostLobby);
        Assert.Equal("Joining Player", store.Load().DisplayName);
        Assert.Equal(["Host Player", "Joining Player"], viewModel.LobbyClients.Select(client => client.DisplayName));
    }

    [Fact]
    public async Task Shell_keeps_join_screen_with_failure_feedback_when_join_fails()
    {
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "Could not connect to the hosted Lobby."),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        Assert.Equal(AppScreen.Join, viewModel.CurrentScreen);
        Assert.True(viewModel.HasConnectionFeedback);
        Assert.Equal("Could not connect to the hosted Lobby.", viewModel.ConnectionFeedback);
    }

    [Fact]
    public async Task Shell_plays_connection_error_cue_when_join_fails()
    {
        var audio = new RecordingGameAudio();
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "Could not connect to the hosted Lobby."),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        Assert.Equal([GameAudioCue.ConnectionError], audio.Cues);
    }

    [Fact]
    public async Task Shell_keeps_join_screen_with_failure_feedback_when_network_join_throws()
    {
        var networkService = new RecordingLobbyNetworkService
        {
            JoinException = new InvalidOperationException("socket setup failed"),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        Assert.Equal(AppScreen.Join, viewModel.CurrentScreen);
        Assert.True(viewModel.HasConnectionFeedback);
        Assert.Contains("Could not connect", viewModel.ConnectionFeedback);
    }

    [Fact]
    public async Task Shell_reflects_lobby_snapshot_updates()
    {
        var clientSession = new FakeClientLobbySession(
            new LobbyState([new LobbyClient(1, "Host Player", true)]));
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "54321";
        await viewModel.JoinHostCommand.ExecuteAsync(null);

        clientSession.Publish(
            new LobbyState(
            [
                new LobbyClient(1, "Host Player", true),
                new LobbyClient(2, "Joining Player", false),
            ]));

        Assert.Equal(["Host Player", "Joining Player"], viewModel.LobbyClients.Select(client => client.DisplayName));
    }

    [Fact]
    public async Task Shell_reflects_team_role_selection_and_start_eligibility()
    {
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        await viewModel.HostMatchCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanStartMatch);
        Assert.Equal("Ready to start.", viewModel.StartLockStatus);
        Assert.Equal(
            [("Player 1", "Red", "Melee"), ("Player 2", "Red", "Ranged"), ("Player 3", "Blue", "Melee"), ("Player 4", "Blue", "Ranged")],
            viewModel.LobbyClients.Select(client => (client.DisplayName, client.TeamDisplay, client.RoleDisplay)));
    }

    [Fact]
    public async Task Shell_applies_local_team_role_selection()
    {
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            new LobbyState([new LobbyClient(1, "Host Player", true)]));
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        viewModel.SelectedTeam = Team.Red;
        viewModel.SelectedRole = FighterRole.Melee;
        await viewModel.ApplySelectionCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasSelectionFeedback);
        var hostClient = Assert.Single(viewModel.LobbyClients);
        Assert.Equal("Red", hostClient.TeamDisplay);
        Assert.Equal("Melee", hostClient.RoleDisplay);
    }

    [Fact]
    public async Task Shell_shows_selection_conflict_feedback()
    {
        var clientSession = new FakeClientLobbySession(
            new LobbyState(
            [
                new LobbyClient(1, "Host Player", true, Team.Red, FighterRole.Melee),
                new LobbyClient(2, "Joining Player", false),
            ]));
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "5000";
        await viewModel.JoinHostCommand.ExecuteAsync(null);
        viewModel.SelectedTeam = Team.Red;
        viewModel.SelectedRole = FighterRole.Melee;
        await viewModel.ApplySelectionCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasSelectionFeedback);
        Assert.Contains("already selected", viewModel.SelectionFeedback);
    }

    [Fact]
    public async Task Shell_starts_match_from_valid_host_lobby()
    {
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);

        Assert.Equal(AppScreen.Match, viewModel.CurrentScreen);
        Assert.Equal("Match", viewModel.CurrentScreenTitle);
        Assert.NotNull(viewModel.MatchSnapshot);
        Assert.Equal(4, viewModel.MatchSnapshot.Fighters.Count);
    }

    [Fact]
    public async Task Shell_switches_from_lobby_music_to_battle_music_when_Match_starts()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);

        Assert.Equal(
            [GameMusicTrack.Lobby, GameMusicTrack.Battle],
            audio.MusicTracks);
    }

    [Fact]
    public async Task Shell_switches_from_battle_music_to_lobby_music_when_returning_to_main_menu()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        await viewModel.BackToMainMenuCommand.ExecuteAsync(null);

        Assert.Equal(
            [GameMusicTrack.Lobby, GameMusicTrack.Battle, GameMusicTrack.Lobby],
            audio.MusicTracks);
    }

    [Fact]
    public async Task Shell_plays_primary_attack_cue_from_new_Match_effect()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Effects =
            [
                new CombatEffect(
                    CombatEffectKind.MeleeFrontalStrike,
                    GameVector.Zero,
                    new GameVector(1, 0),
                    Team.Red,
                    MatchRules.MeleeFrontalStrikeRange,
                    MatchRules.CombatEffectLifetimeSeconds,
                    SourceClientId: 1),
            ],
        });

        Assert.Equal([GameAudioCue.PrimaryAttack], audio.Cues);
    }

    [Fact]
    public async Task Shell_does_not_repeat_cue_for_same_active_Match_effect()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);
        var effect = new CombatEffect(
            CombatEffectKind.MeleeFrontalStrike,
            GameVector.Zero,
            new GameVector(1, 0),
            Team.Red,
            MatchRules.MeleeFrontalStrikeRange,
            MatchRules.CombatEffectLifetimeSeconds,
            SourceClientId: 1);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with { Effects = [effect] });
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Effects = [effect with { RemainingSeconds = effect.RemainingSeconds - MatchRules.FixedDeltaSeconds }],
        });

        Assert.Equal([GameAudioCue.PrimaryAttack], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_role_ability_cue_from_new_Match_effect()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Effects =
            [
                new CombatEffect(
                    CombatEffectKind.MeleeAreaSlash,
                    GameVector.Zero,
                    new GameVector(1, 0),
                    Team.Red,
                    MatchRules.MeleeAreaSlashRadius,
                    MatchRules.CombatEffectLifetimeSeconds,
                    SourceClientId: 1),
            ],
        });

        Assert.Equal([GameAudioCue.RoleAbility], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_Universal_Dash_cue_from_new_Match_effect()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Effects =
            [
                new CombatEffect(
                    CombatEffectKind.UniversalDash,
                    GameVector.Zero,
                    new GameVector(1, 0),
                    Team.Red,
                    MatchRules.MeleeFighterRadius + 8,
                    MatchRules.CombatEffectLifetimeSeconds,
                    SourceClientId: 1),
            ],
        });

        Assert.Equal([GameAudioCue.UniversalDash], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_hit_cue_from_new_Match_effect()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Effects =
            [
                new CombatEffect(
                    CombatEffectKind.Hit,
                    new GameVector(120, -80),
                    new GameVector(1, 0),
                    Team.Blue,
                    MatchRules.MeleeFighterRadius + 4,
                    MatchRules.CombatEffectLifetimeSeconds,
                    SourceClientId: 1,
                    TargetClientId: 3),
            ],
        });

        Assert.Equal([GameAudioCue.Hit], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_death_and_Kill_Announcement_cues_when_enemy_Fighter_is_defeated()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Fighters = viewModel.MatchSnapshot.Fighters
                .Select(fighter => fighter.ClientId == 3 ? fighter with { Health = 0 } : fighter)
                .ToArray(),
            Effects =
            [
                new CombatEffect(
                    CombatEffectKind.Death,
                    new GameVector(120, -80),
                    new GameVector(1, 0),
                    Team.Blue,
                    MatchRules.MeleeFighterRadius + 8,
                    MatchRules.CombatEffectLifetimeSeconds,
                    SourceClientId: 1,
                    TargetClientId: 3),
            ],
        });

        Assert.Equal([GameAudioCue.FighterDefeated, GameAudioCue.KillAnnouncement], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_Round_complete_cue_from_Round_result_snapshot()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Phase = MatchPhase.RoundComplete,
            RedRoundWins = 1,
            RoundResult = new RoundResult(Team.Red, RoundWinReason.TeamElimination, 1),
        });

        Assert.Equal([GameAudioCue.RoundComplete], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_Match_complete_cue_from_Match_result_snapshot()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(viewModel.MatchSnapshot! with
        {
            Phase = MatchPhase.MatchComplete,
            RedRoundWins = MatchRules.RoundsToWinMatch,
            MatchWinner = Team.Red,
            RoundResult = new RoundResult(Team.Red, RoundWinReason.TeamElimination, 2),
        });

        Assert.Equal([GameAudioCue.MatchComplete], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_Lobby_join_cue_when_a_Client_joins_after_initial_Lobby_snapshot()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            new LobbyState([new LobbyClient(1, "Host Player", true)]));
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        hostSession.Publish(
            new LobbyState(
            [
                new LobbyClient(1, "Host Player", true),
                new LobbyClient(2, "Joining Player", false),
            ]));

        Assert.Equal([GameAudioCue.LobbyClientJoined], audio.Cues);
    }

    [Fact]
    public async Task Shell_plays_Lobby_leave_cue_when_a_Client_leaves_Lobby()
    {
        var audio = new RecordingGameAudio();
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            new LobbyState(
            [
                new LobbyClient(1, "Host Player", true),
                new LobbyClient(2, "Joining Player", false),
            ]));
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher(),
            audio);

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        hostSession.Publish(new LobbyState([new LobbyClient(1, "Host Player", true)]));

        Assert.Equal([GameAudioCue.LobbyClientLeft], audio.Cues);
    }

    [Fact]
    public async Task Shell_sends_keyboard_only_input_during_match()
    {
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        await viewModel.StartMatchCommand.ExecuteAsync(null);
        viewModel.SetMatchKeyState(MatchInputKey.MoveRight, true);
        viewModel.SetMatchKeyState(MatchInputKey.AimUp, true);
        viewModel.SetMatchKeyState(MatchInputKey.Dash, true);
        viewModel.SetMatchKeyState(MatchInputKey.PrimaryAttack, true);
        viewModel.SetMatchKeyState(MatchInputKey.RoleAbility, true);

        Assert.Equal(new GameVector(1, 0), hostSession.LastPlayerInput!.MoveDirection);
        Assert.Equal(new GameVector(0, -1), hostSession.LastPlayerInput.AimDirection);
        Assert.True(hostSession.LastPlayerInput.Dash);
        Assert.True(hostSession.LastPlayerInput.PrimaryAttack);
        Assert.True(hostSession.LastPlayerInput.RoleAbility);
    }

    [Fact]
    public async Task Shell_updates_match_hud_from_received_match_snapshot()
    {
        var hostSession = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            HostSession = hostSession,
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());
        var snapshot = new MatchSnapshot(
            [
                new FighterState(1, "Player 1", Team.Red, FighterRole.Melee, GameVector.Zero, new GameVector(1, 0), 150, 0, 0.25, 3.1),
                new FighterState(2, "Player 2", Team.Red, FighterRole.Ranged, GameVector.Zero, new GameVector(1, 0), 100, 0, 0, 0),
                new FighterState(3, "Player 3", Team.Blue, FighterRole.Melee, GameVector.Zero, new GameVector(1, 0), 0, 0, 0, 0),
                new FighterState(4, "Player 4", Team.Blue, FighterRole.Ranged, GameVector.Zero, new GameVector(1, 0), 0, 0, 0, 0),
            ],
            [],
            [],
            2,
            12.2,
            1,
            0,
            MatchPhase.RoundComplete,
            new RoundResult(Team.Red, RoundWinReason.TeamElimination, 1));

        await viewModel.HostMatchCommand.ExecuteAsync(null);
        hostSession.PublishMatchSnapshot(snapshot);

        Assert.Equal(AppScreen.Match, viewModel.CurrentScreen);
        Assert.Equal("Round 2", viewModel.MatchRoundDisplay);
        Assert.Equal("0:13", viewModel.MatchTimerDisplay);
        Assert.Equal("Red 1 - 0 Blue", viewModel.MatchScoreDisplay);
        Assert.True(viewModel.HasMatchResult);
        Assert.Contains("Red wins round 1", viewModel.MatchResultDisplay);
        Assert.Contains("team elimination", viewModel.MatchResultDisplay);
        Assert.Equal(4, viewModel.MatchFighters.Count);

        var damagedMelee = viewModel.MatchFighters.Single(fighter => fighter.ClientId == 1);
        Assert.Equal("150/200", damagedMelee.HealthDisplay);
        Assert.Equal("Primary 0.3s", damagedMelee.PrimaryCooldownDisplay);
        Assert.Equal("Ability 3.1s", damagedMelee.AbilityCooldownDisplay);

        var spectator = viewModel.MatchFighters.Single(fighter => fighter.ClientId == 3);
        Assert.Equal("Spectator", spectator.StateDisplay);
    }

    [Fact]
    public async Task Shell_leaves_match_flow_with_Host_Disconnect_End_feedback()
    {
        var clientSession = new FakeClientLobbySession(
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "5000";
        await viewModel.JoinHostCommand.ExecuteAsync(null);
        clientSession.PublishMatchSnapshot(
            MatchSimulation.Start(CreateValidLobbyState()).Snapshot);

        clientSession.PublishSessionEnded(new LobbySessionEnded(
            LobbySessionEndReason.HostDisconnectEnd,
            "Host Disconnect End: the host disconnected or closed the game."));

        Assert.Equal(AppScreen.Join, viewModel.CurrentScreen);
        Assert.True(viewModel.HasConnectionFeedback);
        Assert.Contains("Host Disconnect End", viewModel.ConnectionFeedback);
    }

    [Fact]
    public async Task Shell_shows_Disconnect_Forfeit_match_result()
    {
        var clientSession = new FakeClientLobbySession(
            CreateValidLobbyState());
        var networkService = new RecordingLobbyNetworkService
        {
            JoinResult = JoinLobbyResult.Success(clientSession),
        };
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            new RecordingApplicationShell(),
            networkService,
            new ImmediateViewDispatcher());
        var snapshot = MatchSimulation.Start(CreateValidLobbyState()).Snapshot with
        {
            Phase = MatchPhase.MatchComplete,
            MatchWinner = Team.Blue,
            BlueRoundWins = MatchRules.RoundsToWinMatch,
            RoundResult = new RoundResult(
                Team.Blue,
                RoundWinReason.DisconnectForfeit,
                1),
        };

        viewModel.JoinMatchCommand.Execute(null);
        viewModel.JoinAddressInput = "127.0.0.1";
        viewModel.JoinPortInput = "5000";
        await viewModel.JoinHostCommand.ExecuteAsync(null);
        clientSession.PublishMatchSnapshot(snapshot);

        Assert.Equal(AppScreen.Match, viewModel.CurrentScreen);
        Assert.True(viewModel.HasMatchResult);
        Assert.Contains("Blue wins the match", viewModel.MatchResultDisplay);
        Assert.Contains("disconnect forfeit", viewModel.MatchResultDisplay);
    }

    [Fact]
    public void Exit_command_requests_application_exit()
    {
        var shell = new RecordingApplicationShell();
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            shell,
            new RecordingLobbyNetworkService(),
            new ImmediateViewDispatcher());

        viewModel.ExitCommand.Execute(null);

        Assert.True(shell.ExitRequested);
    }

    private static string CreateProfilePath()
    {
        return Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "profile.json");
    }

    private static LobbyState CreateValidLobbyState()
    {
        return new LobbyState(
        [
            new LobbyClient(1, "Player 1", true, Team.Red, FighterRole.Melee),
            new LobbyClient(2, "Player 2", false, Team.Red, FighterRole.Ranged),
            new LobbyClient(3, "Player 3", false, Team.Blue, FighterRole.Melee),
            new LobbyClient(4, "Player 4", false, Team.Blue, FighterRole.Ranged),
        ]);
    }

    private sealed class RecordingApplicationShell : IApplicationShell
    {
        public bool ExitRequested { get; private set; }

        public void Exit()
        {
            ExitRequested = true;
        }
    }

    private sealed class RecordingLobbyNetworkService : ILobbyNetworkService
    {
        public IHostLobbySession HostSession { get; init; } = new FakeHostLobbySession(
            ["127.0.0.1"],
            5000,
            new LobbyState([new LobbyClient(1, "Host Player", true)]));

        public JoinLobbyResult JoinResult { get; init; } = JoinLobbyResult.Failure(
            JoinFailureReason.ConnectionFailed,
            "Could not connect to the hosted Lobby.");

        public Exception? JoinException { get; init; }

        public List<string> StartedHostDisplayNames { get; } = [];

        public List<JoinLobbyRequest> JoinRequests { get; } = [];

        public Task<IHostLobbySession> StartHostAsync(
            string displayName,
            CancellationToken cancellationToken = default)
        {
            StartedHostDisplayNames.Add(displayName);
            return Task.FromResult(HostSession);
        }

        public Task<JoinLobbyResult> JoinAsync(
            JoinLobbyRequest request,
            CancellationToken cancellationToken = default)
        {
            if (JoinException is not null)
            {
                throw JoinException;
            }

            JoinRequests.Add(request);
            return Task.FromResult(JoinResult);
        }
    }

    private sealed class FakeHostLobbySession(
        IReadOnlyList<string> shareableAddresses,
        int port,
        LobbyState snapshot)
        : FakeLobbySession(snapshot, 1), IHostLobbySession
    {
        public IReadOnlyList<string> ShareableAddresses { get; } = shareableAddresses;

        public int Port { get; } = port;

        public Task<StartMatchResult> StartMatchAsync(CancellationToken cancellationToken = default)
        {
            if (!Snapshot.StartEligibility.CanStart)
            {
                return Task.FromResult(StartMatchResult.Failure(
                    StartMatchFailureReason.LobbyNotReady,
                    "The Lobby must have exactly four Clients and valid Team roles."));
            }

            var simulation = MatchSimulation.Start(Snapshot);
            PublishMatchSnapshot(simulation.Snapshot);

            return Task.FromResult(StartMatchResult.Success(simulation.Snapshot));
        }
    }

    private sealed class FakeClientLobbySession(LobbyState snapshot)
        : FakeLobbySession(snapshot, 2), IClientLobbySession;

    private abstract class FakeLobbySession(LobbyState snapshot, int localClientId) : ILobbySession
    {
        public event EventHandler<LobbyState>? SnapshotChanged;

        public event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

        public event EventHandler<LobbySessionEnded>? SessionEnded;

        public int LocalClientId { get; } = localClientId;

        public LobbyState Snapshot { get; private set; } = snapshot;

        public MatchSnapshot? MatchSnapshot { get; private set; }

        public PlayerInput? LastPlayerInput { get; private set; }

        public Task<LobbySelectionResult> SelectTeamRoleAsync(
            Team team,
            FighterRole role,
            CancellationToken cancellationToken = default)
        {
            var result = Snapshot.ApplySelection(new LobbySelection(LocalClientId, team, role));

            if (result.Succeeded)
            {
                Publish(result.Lobby);
            }

            return Task.FromResult(result);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task SendPlayerInputAsync(
            PlayerInput input,
            CancellationToken cancellationToken = default)
        {
            LastPlayerInput = input;
            return Task.CompletedTask;
        }

        public void Publish(LobbyState snapshot)
        {
            Snapshot = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }

        public void PublishMatchSnapshot(MatchSnapshot snapshot)
        {
            MatchSnapshot = snapshot;
            MatchSnapshotChanged?.Invoke(this, snapshot);
        }

        public void PublishSessionEnded(LobbySessionEnded sessionEnded)
        {
            SessionEnded?.Invoke(this, sessionEnded);
        }
    }

    private sealed class ImmediateViewDispatcher : IViewDispatcher
    {
        public void Post(Action action)
        {
            action();
        }
    }

    private sealed class RecordingGameAudio : IGameAudio
    {
        public List<GameMusicTrack> MusicTracks { get; } = [];

        public List<GameAudioCue> Cues { get; } = [];

        public void PlayCue(GameAudioCue cue)
        {
            Cues.Add(cue);
        }

        public void SwitchMusic(GameMusicTrack track)
        {
            MusicTracks.Add(track);
        }
    }
}
