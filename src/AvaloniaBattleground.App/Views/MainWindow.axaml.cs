using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaBattleground.App.ViewModels;

namespace AvaloniaBattleground.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        HandleKey(e, true);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        HandleKey(e, false);
    }

    private void HandleKey(KeyEventArgs e, bool isPressed)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !TryMapKey(e.Key, out var inputKey))
        {
            return;
        }

        viewModel.SetMatchKeyState(inputKey, isPressed);
        e.Handled = true;
    }

    private static bool TryMapKey(Key key, out MatchInputKey inputKey)
    {
        inputKey = key switch
        {
            Key.W => MatchInputKey.MoveUp,
            Key.S => MatchInputKey.MoveDown,
            Key.A => MatchInputKey.MoveLeft,
            Key.D => MatchInputKey.MoveRight,
            Key.Up => MatchInputKey.AimUp,
            Key.Down => MatchInputKey.AimDown,
            Key.Left => MatchInputKey.AimLeft,
            Key.Right => MatchInputKey.AimRight,
            Key.LeftShift => MatchInputKey.Dash,
            Key.Space => MatchInputKey.PrimaryAttack,
            Key.LeftCtrl => MatchInputKey.RoleAbility,
            _ => default,
        };

        return key is Key.W or Key.S or Key.A or Key.D or
            Key.Up or Key.Down or Key.Left or Key.Right or
            Key.LeftShift or Key.Space or Key.LeftCtrl;
    }
}
