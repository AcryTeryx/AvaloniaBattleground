using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace AvaloniaBattleground.App.ViewModels;

public sealed class AvaloniaApplicationShell : IApplicationShell
{
    public void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
