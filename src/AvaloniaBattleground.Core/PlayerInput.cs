namespace AvaloniaBattleground.Core;

public sealed record PlayerInput(
    GameVector MoveDirection,
    GameVector AimDirection,
    bool Dash,
    bool PrimaryAttack = false,
    bool RoleAbility = false);

public static class KeyboardInputMapper
{
    public static PlayerInput Map(
        bool moveUp,
        bool moveDown,
        bool moveLeft,
        bool moveRight,
        bool aimUp,
        bool aimDown,
        bool aimLeft,
        bool aimRight,
        bool dash,
        bool primaryAttack = false,
        bool roleAbility = false)
    {
        var move = new GameVector(
            BoolToAxis(moveRight, moveLeft),
            BoolToAxis(moveDown, moveUp)).NormalizeOrZero();
        var aim = new GameVector(
            BoolToAxis(aimRight, aimLeft),
            BoolToAxis(aimDown, aimUp)).NormalizeOrZero();

        return new PlayerInput(move, aim, dash, primaryAttack, roleAbility);
    }

    private static int BoolToAxis(bool positive, bool negative)
    {
        return positive == negative
            ? 0
            : positive ? 1 : -1;
    }
}
