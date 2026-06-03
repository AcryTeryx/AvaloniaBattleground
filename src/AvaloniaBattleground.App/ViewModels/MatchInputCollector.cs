using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.App.ViewModels;

// Accumulates the pressed/released state of every keyboard-only control and
// projects it into a normalized PlayerInput. Keeping this out of the shell view
// model isolates the raw key bookkeeping from screen and session concerns.
internal sealed class MatchInputCollector
{
    private bool _moveUp;
    private bool _moveDown;
    private bool _moveLeft;
    private bool _moveRight;
    private bool _aimUp;
    private bool _aimDown;
    private bool _aimLeft;
    private bool _aimRight;
    private bool _dash;
    private bool _primaryAttack;
    private bool _roleAbility;

    public void SetKey(MatchInputKey key, bool isPressed)
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
    }

    public PlayerInput ToPlayerInput()
    {
        return KeyboardInputMapper.Map(
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
            _roleAbility);
    }
}
