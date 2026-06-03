using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using AvaloniaBattleground.App.Audio;
using AvaloniaBattleground.App.ViewModels;
using AvaloniaBattleground.App.Views;
using AvaloniaBattleground.Core;
using AvaloniaBattleground.Networking;

namespace AvaloniaBattleground.App;

public partial class App : Application
{
    private IGameAudio? _gameAudio;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _gameAudio = new ProceduralGameAudio();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    new LocalProfileStore(LocalProfileStore.GetDefaultProfilePath()),
                    new AvaloniaApplicationShell(),
                    new TcpLobbyNetworkService(),
                    new AvaloniaViewDispatcher(),
                    _gameAudio),
            };
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _gameAudio?.Dispose();
        _gameAudio = null;
    }
}