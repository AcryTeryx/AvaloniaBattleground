using System.Text.Json.Serialization;

namespace AvaloniaBattleground.Core;

public readonly record struct GameVector(double X, double Y)
{
    [JsonIgnore]
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public static GameVector Zero { get; } = new(0, 0);

    public double Dot(GameVector other)
    {
        return (X * other.X) + (Y * other.Y);
    }

    public double DistanceTo(GameVector other)
    {
        return (this - other).Length;
    }

    public GameVector NormalizeOrZero()
    {
        var length = Length;
        return length == 0
            ? Zero
            : new GameVector(X / length, Y / length);
    }

    public static GameVector operator +(GameVector left, GameVector right)
    {
        return new GameVector(left.X + right.X, left.Y + right.Y);
    }

    public static GameVector operator -(GameVector left, GameVector right)
    {
        return new GameVector(left.X - right.X, left.Y - right.Y);
    }

    public static GameVector operator *(GameVector vector, double scale)
    {
        return new GameVector(vector.X * scale, vector.Y * scale);
    }
}
