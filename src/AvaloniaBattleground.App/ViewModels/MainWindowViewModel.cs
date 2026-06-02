using AvaloniaBattleground.Core;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace AvaloniaBattleground.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IApplicationShell _applicationShell;
    private readonly LocalProfileStore _profileStore;
    private string _currentDisplayName;
    private string _currentScreenTitle = "Main Menu";
    private string _displayNameInput;
    private bool _isMainMenu = true;
    private bool _isPlaceholderScreen;

    public MainWindowViewModel()
        : this(
            new LocalProfileStore(LocalProfileStore.GetDefaultProfilePath()),
            new AvaloniaApplicationShell())
    {
    }

    public MainWindowViewModel(LocalProfileStore profileStore, IApplicationShell applicationShell)
    {
        _profileStore = profileStore;
        _applicationShell = applicationShell;

        var profile = _profileStore.Load();
        _currentDisplayName = profile.DisplayName;
        _displayNameInput = profile.DisplayName;

        SaveDisplayNameCommand = new RelayCommand(SaveDisplayName);
        HostMatchCommand = new RelayCommand(() => NavigateToPlaceholder("Host Match"));
        JoinMatchCommand = new RelayCommand(() => NavigateToPlaceholder("Join Match"));
        BackToMainMenuCommand = new RelayCommand(ShowMainMenu);
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

    public bool IsPlaceholderScreen
    {
        get => _isPlaceholderScreen;
        private set => SetProperty(ref _isPlaceholderScreen, value);
    }

    public ICommand SaveDisplayNameCommand { get; }

    public ICommand HostMatchCommand { get; }

    public ICommand JoinMatchCommand { get; }

    public ICommand BackToMainMenuCommand { get; }

    public ICommand ExitCommand { get; }

    private void SaveDisplayName()
    {
        var normalizedDisplayName = LocalProfileStore.NormalizeDisplayName(DisplayNameInput);
        var profile = new LocalProfile(normalizedDisplayName);

        _profileStore.Save(profile);
        CurrentDisplayName = normalizedDisplayName;
        DisplayNameInput = normalizedDisplayName;
    }

    private void NavigateToPlaceholder(string screenTitle)
    {
        SaveDisplayName();
        CurrentScreenTitle = screenTitle;
        IsMainMenu = false;
        IsPlaceholderScreen = true;
    }

    private void ShowMainMenu()
    {
        CurrentScreenTitle = "Main Menu";
        IsPlaceholderScreen = false;
        IsMainMenu = true;
    }
}
