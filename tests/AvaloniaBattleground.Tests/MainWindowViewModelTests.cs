using AvaloniaBattleground.App.ViewModels;
using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Shell_initializes_from_stored_local_display_name()
    {
        var profilePath = CreateProfilePath();
        new LocalProfileStore(profilePath).Save(new LocalProfile("Acryteryx"));

        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(profilePath),
            new RecordingApplicationShell());

        Assert.True(viewModel.IsMainMenu);
        Assert.False(viewModel.IsPlaceholderScreen);
        Assert.Equal("Acryteryx", viewModel.CurrentDisplayName);
        Assert.Equal("Acryteryx", viewModel.DisplayNameInput);
    }

    [Theory]
    [InlineData("Host Match")]
    [InlineData("Join Match")]
    public void Shell_saves_edited_display_name_before_placeholder_navigation(string targetScreen)
    {
        var profilePath = CreateProfilePath();
        var store = new LocalProfileStore(profilePath);
        var viewModel = new MainWindowViewModel(store, new RecordingApplicationShell())
        {
            DisplayNameInput = "  Acryteryx  ",
        };

        if (targetScreen == "Host Match")
        {
            viewModel.HostMatchCommand.Execute(null);
        }
        else
        {
            viewModel.JoinMatchCommand.Execute(null);
        }

        Assert.False(viewModel.IsMainMenu);
        Assert.True(viewModel.IsPlaceholderScreen);
        Assert.Equal(targetScreen, viewModel.CurrentScreenTitle);
        Assert.Equal("Acryteryx", viewModel.CurrentDisplayName);
        Assert.Equal("Acryteryx", store.Load().DisplayName);
    }

    [Fact]
    public void Exit_command_requests_application_exit()
    {
        var shell = new RecordingApplicationShell();
        var viewModel = new MainWindowViewModel(
            new LocalProfileStore(CreateProfilePath()),
            shell);

        viewModel.ExitCommand.Execute(null);

        Assert.True(shell.ExitRequested);
    }

    private static string CreateProfilePath()
    {
        return Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "profile.json");
    }

    private sealed class RecordingApplicationShell : IApplicationShell
    {
        public bool ExitRequested { get; private set; }

        public void Exit()
        {
            ExitRequested = true;
        }
    }
}
