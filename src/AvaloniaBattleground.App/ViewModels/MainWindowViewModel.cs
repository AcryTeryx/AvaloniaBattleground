using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AvaloniaBattleground.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IApplicationShell _applicationShell;
    private readonly ILobbyNetworkService _lobbyNetworkService;
    private readonly LocalProfileStore _profileStore;
    private readonly IViewDispatcher _viewDispatcher;
    private string _connectionFeedback = string.Empty;
    private string _currentDisplayName;
    private string _currentScreenTitle = "Main Menu";
    private string _displayNameInput;
    private bool _canStartMatch;
    private bool _hasConnectionFeedback;
    private bool _hasSelectionFeedback;
    private string _hostAddressesDisplay = string.Empty;
    private string _hostPortDisplay = string.Empty;
    private bool _isBusy;
    private bool _isHostLobby;
    private bool _isJoinScreen;
    private bool _isLobbyScreen;
    private bool _isMainMenu = true;
    private bool _isMatchScreen;
    private string _joinAddressInput = "127.0.0.1";
    private string _joinPortInput = string.Empty;
    private ILobbySession? _lobbySession;
    private MatchSnapshot? _matchSnapshot;
    private bool _moveDown;
    private bool _moveLeft;
    private bool _moveRight;
    private bool _moveUp;
    private bool _aimDown;
    private bool _aimLeft;
    private bool _aimRight;
    private bool _aimUp;
    private bool _dash;
    private bool _primaryAttack;
    private bool _roleAbility;
    private FighterRole _selectedRole = FighterRole.Melee;
    private Team _selectedTeam = Team.Red;
    private string _selectionFeedback = string.Empty;
    private EventHandler<MatchSnapshot>? _matchSnapshotChangedHandler;
    private EventHandler<LobbySnapshot>? _snapshotChangedHandler;
    private string _startLockStatus = "Waiting for exactly four Clients.";

    public MainWindowViewModel()
        : this(
            new LocalProfileStore(LocalProfileStore.GetDefaultProfilePath()),
            new AvaloniaApplicationShell(),
            new TcpLobbyNetworkService(),
            new AvaloniaViewDispatcher())
    {
    }

    public MainWindowViewModel(
        LocalProfileStore profileStore,
        IApplicationShell applicationShell,
        ILobbyNetworkService lobbyNetworkService,
        IViewDispatcher viewDispatcher)
    {
        _profileStore = profileStore;
        _applicationShell = applicationShell;
        _lobbyNetworkService = lobbyNetworkService;
        _viewDispatcher = viewDispatcher;

        var profile = _profileStore.Load();
        _currentDisplayName = profile.DisplayName;
        _displayNameInput = profile.DisplayName;

        SaveDisplayNameCommand = new RelayCommand(SaveDisplayName);
        HostMatchCommand = new AsyncRelayCommand(HostMatchAsync);
        JoinMatchCommand = new RelayCommand(ShowJoinScreen);
        JoinHostCommand = new AsyncRelayCommand(JoinHostAsync);
        ApplySelectionCommand = new AsyncRelayCommand(ApplySelectionAsync);
        StartMatchCommand = new AsyncRelayCommand(StartMatchAsync, () => CanStartMatch);
        BackToMainMenuCommand = new AsyncRelayCommand(ShowMainMenuAsync);
        ExitCommand = new RelayCommand(_applicationShell.Exit);
    }

    public string CurrentDisplayName
    {
        get => _currentDisplayName;
        private set => SetProperty(ref _currentDisplayName, value);
    }

    public string DisplayNameInput
    {
        get => _displayNameInput;
        set => SetProperty(ref _displayNameInput, value);
    }

    public string CurrentScreenTitle
    {
        get => _currentScreenTitle;
        private set => SetProperty(ref _currentScreenTitle, value);
    }

    public bool IsMainMenu
    {
        get => _isMainMenu;
        private set => SetProperty(ref _isMainMenu, value);
    }

    public bool IsJoinScreen
    {
        get => _isJoinScreen;
        private set => SetProperty(ref _isJoinScreen, value);
    }

    public bool IsLobbyScreen
    {
        get => _isLobbyScreen;
        private set => SetProperty(ref _isLobbyScreen, value);
    }

    public bool IsMatchScreen
    {
        get => _isMatchScreen;
        private set => SetProperty(ref _isMatchScreen, value);
    }

    public bool IsHostLobby
    {
        get => _isHostLobby;
        private set => SetProperty(ref _isHostLobby, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public IReadOnlyList<Team> TeamOptions { get; } = [Team.Red, Team.Blue];

    public IReadOnlyList<FighterRole> RoleOptions { get; } = [FighterRole.Melee, FighterRole.Ranged];

    public Team SelectedTeam
    {
        get => _selectedTeam;
        set => SetProperty(ref _selectedTeam, value);
    }

    public FighterRole SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public bool CanStartMatch
    {
        get => _canStartMatch;
        private set
        {
            if (SetProperty(ref _canStartMatch, value))
            {
                StartMatchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StartLockStatus
    {
        get => _startLockStatus;
        private set => SetProperty(ref _startLockStatus, value);
    }

    public MatchSnapshot? MatchSnapshot
    {
        get => _matchSnapshot;
        private set => SetProperty(ref _matchSnapshot, value);
    }

    public string JoinAddressInput
    {
        get => _joinAddressInput;
        set => SetProperty(ref _joinAddressInput, value);
    }

    public string JoinPortInput
    {
        get => _joinPortInput;
        set => SetProperty(ref _joinPortInput, value);
    }

    public string HostAddressesDisplay
    {
        get => _hostAddressesDisplay;
        private set => SetProperty(ref _hostAddressesDisplay, value);
    }

    public string HostPortDisplay
    {
        get => _hostPortDisplay;
        private set => SetProperty(ref _hostPortDisplay, value);
    }

    public string ConnectionFeedback
    {
        get => _connectionFeedback;
        private set => SetProperty(ref _connectionFeedback, value);
    }

    public bool HasConnectionFeedback
    {
        get => _hasConnectionFeedback;
        private set => SetProperty(ref _hasConnectionFeedback, value);
    }

    public string SelectionFeedback
    {
        get => _selectionFeedback;
        private set => SetProperty(ref _selectionFeedback, value);
    }

    public bool HasSelectionFeedback
    {
        get => _hasSelectionFeedback;
        private set => SetProperty(ref _hasSelectionFeedback, value);
    }

    public ObservableCollection<LobbyClientItemViewModel> LobbyClients { get; } = [];

    public ICommand SaveDisplayNameCommand { get; }

    public IAsyncRelayCommand HostMatchCommand { get; }

    public ICommand JoinMatchCommand { get; }

    public IAsyncRelayCommand JoinHostCommand { get; }

    public IAsyncRelayCommand ApplySelectionCommand { get; }

    public IAsyncRelayCommand StartMatchCommand { get; }

    public IAsyncRelayCommand BackToMainMenuCommand { get; }

    public ICommand ExitCommand { get; }

    private void SaveDisplayName()
    {
        var normalizedDisplayName = LocalProfileStore.NormalizeDisplayName(DisplayNameInput);
        var profile = new LocalProfile(normalizedDisplayName);

        _profileStore.Save(profile);
        CurrentDisplayName = normalizedDisplayName;
        DisplayNameInput = normalizedDisplayName;
    }

    private async Task HostMatchAsync()
    {
        SaveDisplayName();
        SetConnectionFeedback(string.Empty);
        IsBusy = true;

        try
        {
            await StopLobbySessionAsync();

            var hostSession = await _lobbyNetworkService.StartHostAsync(CurrentDisplayName);
            AttachLobbySession(hostSession);

            HostAddressesDisplay = string.Join(", ", hostSession.ShareableAddresses);
            HostPortDisplay = hostSession.Port.ToString(CultureInfo.InvariantCulture);
            IsHostLobby = true;
            ShowLobbyScreen();
        }
        catch (Exception exception)
        {
            await StopLobbySessionAsync();
            ShowMainMenu();
            SetConnectionFeedback($"Could not start hosting: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowJoinScreen()
    {
        SaveDisplayName();
        SetConnectionFeedback(string.Empty);
        IsHostLobby = false;
        HostAddressesDisplay = string.Empty;
        HostPortDisplay = string.Empty;
        CanStartMatch = false;
        StartLockStatus = "Waiting for exactly four Clients.";
        LobbyClients.Clear();

        CurrentScreenTitle = "Join Match";
        IsMainMenu = false;
        IsLobbyScreen = false;
        IsMatchScreen = false;
        IsJoinScreen = true;
    }

    private async Task JoinHostAsync()
    {
        SetConnectionFeedback(string.Empty);
        SaveDisplayName();

        if (!int.TryParse(JoinPortInput, NumberStyles.None, CultureInfo.InvariantCulture, out var port))
        {
            SetConnectionFeedback("Enter a port from 1 to 65535.");
            return;
        }

        IsBusy = true;

        try
        {
            await StopLobbySessionAsync();

            var joinResult = await _lobbyNetworkService.JoinAsync(
                new JoinLobbyRequest(JoinAddressInput, port, CurrentDisplayName));

            if (!joinResult.Succeeded)
            {
                SetConnectionFeedback(joinResult.FailureMessage);
                ShowJoinFailureScreen();
                return;
            }

            AttachLobbySession(joinResult.Session!);
            IsHostLobby = false;
            HostAddressesDisplay = string.Empty;
            HostPortDisplay = string.Empty;
            ShowLobbyScreen();
        }
        catch (Exception exception)
        {
            SetConnectionFeedback($"Could not connect to the hosted Lobby: {exception.Message}");
            ShowJoinFailureScreen();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowMainMenuAsync()
    {
        await StopLobbySessionAsync();
        ShowMainMenu();
        SetConnectionFeedback(string.Empty);
        SetSelectionFeedback(string.Empty);
    }

    private void AttachLobbySession(ILobbySession lobbySession)
    {
        _lobbySession = lobbySession;
        _snapshotChangedHandler = (_, snapshot) =>
            _viewDispatcher.Post(() => UpdateLobbyClients(snapshot));
        _matchSnapshotChangedHandler = (_, snapshot) =>
            _viewDispatcher.Post(() => ShowMatchSnapshot(snapshot));
        _lobbySession.SnapshotChanged += _snapshotChangedHandler;
        _lobbySession.MatchSnapshotChanged += _matchSnapshotChangedHandler;
        UpdateLobbyClients(_lobbySession.Snapshot);

        if (_lobbySession.MatchSnapshot is not null)
        {
            ShowMatchSnapshot(_lobbySession.MatchSnapshot);
        }
    }

    private async Task StopLobbySessionAsync()
    {
        if (_lobbySession is null)
        {
            return;
        }

        if (_snapshotChangedHandler is not null)
        {
            _lobbySession.SnapshotChanged -= _snapshotChangedHandler;
        }

        if (_matchSnapshotChangedHandler is not null)
        {
            _lobbySession.MatchSnapshotChanged -= _matchSnapshotChangedHandler;
        }

        await _lobbySession.DisposeAsync();
        _lobbySession = null;
        _snapshotChangedHandler = null;
        _matchSnapshotChangedHandler = null;
        MatchSnapshot = null;
        CanStartMatch = false;
        StartLockStatus = "Waiting for exactly four Clients.";
        LobbyClients.Clear();
    }

    private async Task ApplySelectionAsync()
    {
        if (_lobbySession is null)
        {
            return;
        }

        var result = await _lobbySession.SelectTeamRoleAsync(SelectedTeam, SelectedRole);
        if (result.Succeeded)
        {
            SetSelectionFeedback(string.Empty);
            UpdateLobbyClients(LobbySnapshot.FromLobbyState(result.Lobby));
            return;
        }

        SetSelectionFeedback(result.Message);
    }

    private async Task StartMatchAsync()
    {
        if (_lobbySession is not IHostLobbySession hostLobbySession)
        {
            return;
        }

        var result = await hostLobbySession.StartMatchAsync();
        if (!result.Succeeded)
        {
            SetSelectionFeedback(result.Message);
            return;
        }

        SetSelectionFeedback(string.Empty);
        ShowMatchSnapshot(result.MatchSnapshot!);
    }

    private void ShowLobbyScreen()
    {
        CurrentScreenTitle = "Lobby";
        IsMainMenu = false;
        IsJoinScreen = false;
        IsMatchScreen = false;
        IsLobbyScreen = true;
    }

    private void ShowJoinFailureScreen()
    {
        CurrentScreenTitle = "Join Match";
        IsMainMenu = false;
        IsLobbyScreen = false;
        IsMatchScreen = false;
        IsJoinScreen = true;
    }

    private void ShowMainMenu()
    {
        CurrentScreenTitle = "Main Menu";
        IsJoinScreen = false;
        IsLobbyScreen = false;
        IsMatchScreen = false;
        IsHostLobby = false;
        IsMainMenu = true;
        HostAddressesDisplay = string.Empty;
        HostPortDisplay = string.Empty;
        CanStartMatch = false;
        StartLockStatus = "Waiting for exactly four Clients.";
    }

    private void UpdateLobbyClients(LobbySnapshot snapshot)
    {
        LobbyClients.Clear();

        foreach (var client in snapshot.Clients)
        {
            LobbyClients.Add(new LobbyClientItemViewModel(
                client.DisplayName,
                client.IsHost,
                client.Team,
                client.Role));
        }

        var localClient = snapshot.Clients.SingleOrDefault(client =>
            client.ClientId == _lobbySession?.LocalClientId);
        if (localClient?.Team is not null)
        {
            SelectedTeam = localClient.Team.Value;
        }

        if (localClient?.Role is not null)
        {
            SelectedRole = localClient.Role.Value;
        }

        CanStartMatch = snapshot.StartEligibility.CanStart;
        StartLockStatus = GetStartLockStatus(snapshot.StartEligibility);
    }

    private void ShowMatchSnapshot(MatchSnapshot snapshot)
    {
        MatchSnapshot = snapshot;
        CurrentScreenTitle = "Match";
        IsMainMenu = false;
        IsJoinScreen = false;
        IsLobbyScreen = false;
        IsMatchScreen = true;
    }

    public void SetMatchKeyState(MatchInputKey key, bool isPressed)
    {
        switch (key)
        {
            case MatchInputKey.MoveUp:
                _moveUp = isPressed;
                break;
            case MatchInputKey.MoveDown:
                _moveDown = isPressed;
                break;
            case MatchInputKey.MoveLeft:
                _moveLeft = isPressed;
                break;
            case MatchInputKey.MoveRight:
                _moveRight = isPressed;
                break;
            case MatchInputKey.AimUp:
                _aimUp = isPressed;
                break;
            case MatchInputKey.AimDown:
                _aimDown = isPressed;
                break;
            case MatchInputKey.AimLeft:
                _aimLeft = isPressed;
                break;
            case MatchInputKey.AimRight:
                _aimRight = isPressed;
                break;
            case MatchInputKey.Dash:
                _dash = isPressed;
                break;
            case MatchInputKey.PrimaryAttack:
                _primaryAttack = isPressed;
                break;
            case MatchInputKey.RoleAbility:
                _roleAbility = isPressed;
                break;
        }

        _ = SendCurrentInputAsync();
    }

    private async Task SendCurrentInputAsync()
    {
        if (!IsMatchScreen || _lobbySession is null)
        {
            return;
        }

        await _lobbySession.SendPlayerInputAsync(
            KeyboardInputMapper.Map(
                _moveUp,
                _moveDown,
                _moveLeft,
                _moveRight,
                _aimUp,
                _aimDown,
                _aimLeft,
                _aimRight,
                _dash,
                _primaryAttack,
                _roleAbility));
    }

    private void SetConnectionFeedback(string message)
    {
        ConnectionFeedback = message;
        HasConnectionFeedback = !string.IsNullOrWhiteSpace(message);
    }

    private void SetSelectionFeedback(string message)
    {
        SelectionFeedback = message;
        HasSelectionFeedback = !string.IsNullOrWhiteSpace(message);
    }

    private static string GetStartLockStatus(LobbyStartEligibility eligibility)
    {
        if (eligibility.CanStart)
        {
            return "Ready to start.";
        }

        if (eligibility.LockReasons.Contains(LobbyStartLockReason.FullLobbyRequirement) &&
            eligibility.LockReasons.Contains(LobbyStartLockReason.RoleConstraint))
        {
            return "Waiting for exactly four Clients and valid Team roles.";
        }

        if (eligibility.LockReasons.Contains(LobbyStartLockReason.FullLobbyRequirement))
        {
            return "Waiting for exactly four Clients.";
        }

        return "Waiting for each Team to choose one Melee and one Ranged Fighter.";
    }
}
