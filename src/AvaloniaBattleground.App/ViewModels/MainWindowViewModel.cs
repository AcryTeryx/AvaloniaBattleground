using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
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
    private bool _hasConnectionFeedback;
    private string _hostAddressesDisplay = string.Empty;
    private string _hostPortDisplay = string.Empty;
    private bool _isBusy;
    private bool _isHostLobby;
    private bool _isJoinScreen;
    private bool _isLobbyScreen;
    private bool _isMainMenu = true;
    private string _joinAddressInput = "127.0.0.1";
    private string _joinPortInput = string.Empty;
    private ILobbySession? _lobbySession;
    private EventHandler<LobbySnapshot>? _snapshotChangedHandler;

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

    public ObservableCollection<LobbyClientItemViewModel> LobbyClients { get; } = [];

    public ICommand SaveDisplayNameCommand { get; }

    public IAsyncRelayCommand HostMatchCommand { get; }

    public ICommand JoinMatchCommand { get; }

    public IAsyncRelayCommand JoinHostCommand { get; }

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
        LobbyClients.Clear();

        CurrentScreenTitle = "Join Match";
        IsMainMenu = false;
        IsLobbyScreen = false;
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
    }

    private void AttachLobbySession(ILobbySession lobbySession)
    {
        _lobbySession = lobbySession;
        _snapshotChangedHandler = (_, snapshot) =>
            _viewDispatcher.Post(() => UpdateLobbyClients(snapshot));
        _lobbySession.SnapshotChanged += _snapshotChangedHandler;
        UpdateLobbyClients(_lobbySession.Snapshot);
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

        await _lobbySession.DisposeAsync();
        _lobbySession = null;
        _snapshotChangedHandler = null;
        LobbyClients.Clear();
    }

    private void ShowLobbyScreen()
    {
        CurrentScreenTitle = "Lobby";
        IsMainMenu = false;
        IsJoinScreen = false;
        IsLobbyScreen = true;
    }

    private void ShowJoinFailureScreen()
    {
        CurrentScreenTitle = "Join Match";
        IsMainMenu = false;
        IsLobbyScreen = false;
        IsJoinScreen = true;
    }

    private void ShowMainMenu()
    {
        CurrentScreenTitle = "Main Menu";
        IsJoinScreen = false;
        IsLobbyScreen = false;
        IsHostLobby = false;
        IsMainMenu = true;
        HostAddressesDisplay = string.Empty;
        HostPortDisplay = string.Empty;
    }

    private void UpdateLobbyClients(LobbySnapshot snapshot)
    {
        LobbyClients.Clear();

        foreach (var client in snapshot.Clients)
        {
            LobbyClients.Add(new LobbyClientItemViewModel(client.DisplayName, client.IsHost));
        }
    }

    private void SetConnectionFeedback(string message)
    {
        ConnectionFeedback = message;
        HasConnectionFeedback = !string.IsNullOrWhiteSpace(message);
    }
}
