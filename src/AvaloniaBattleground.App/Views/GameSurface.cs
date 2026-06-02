using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaBattleground.Core;
using System;

namespace AvaloniaBattleground.App.Views;

public sealed class GameSurface : Control
{
    public static readonly StyledProperty<MatchSnapshot?> MatchSnapshotProperty =
        AvaloniaProperty.Register<GameSurface, MatchSnapshot?>(nameof(MatchSnapshot));

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#101418"));
    private static readonly IBrush ArenaBrush = new SolidColorBrush(Color.Parse("#182026"));
    private static readonly Pen ArenaPen = new(new SolidColorBrush(Color.Parse("#AEB7C2")), 2);
    private static readonly Pen AimPen = new(new SolidColorBrush(Color.Parse("#F4F7FA")), 2);
    private static readonly Pen FighterPen = new(new SolidColorBrush(Color.Parse("#101418")), 2);
    private static readonly IBrush RedTeamBrush = new SolidColorBrush(Color.Parse("#D95858"));
    private static readonly IBrush BlueTeamBrush = new SolidColorBrush(Color.Parse("#4E8FE8"));

    public MatchSnapshot? MatchSnapshot
    {
        get => GetValue(MatchSnapshotProperty);
        set => SetValue(MatchSnapshotProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(BackgroundBrush, null, bounds);

        var center = bounds.Center;
        var arenaScale = Math.Max(
            0.1,
            Math.Min(bounds.Width, bounds.Height) / ((MatchRules.ArenaRadius * 2) + 40));
        var arenaRadius = MatchRules.ArenaRadius * arenaScale;
        var arenaRect = new Rect(
            center.X - arenaRadius,
            center.Y - arenaRadius,
            arenaRadius * 2,
            arenaRadius * 2);

        context.DrawEllipse(ArenaBrush, ArenaPen, arenaRect);

        if (MatchSnapshot is null)
        {
            return;
        }

        foreach (var fighter in MatchSnapshot.Fighters)
        {
            DrawFighter(context, center, arenaScale, fighter);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MatchSnapshotProperty)
        {
            InvalidateVisual();
        }
    }

    private static void DrawFighter(
        DrawingContext context,
        Point center,
        double arenaScale,
        FighterState fighter)
    {
        var fighterCenter = ToScreenPoint(center, arenaScale, fighter.Position);
        var fighterRadius = fighter.Role == FighterRole.Melee ? 12 : 10;
        var fighterBrush = fighter.Team == Team.Red ? RedTeamBrush : BlueTeamBrush;
        var fighterRect = new Rect(
            fighterCenter.X - fighterRadius,
            fighterCenter.Y - fighterRadius,
            fighterRadius * 2,
            fighterRadius * 2);

        context.DrawEllipse(fighterBrush, FighterPen, fighterRect);

        var aimEnd = new Point(
            fighterCenter.X + (fighter.AimDirection.X * 24),
            fighterCenter.Y + (fighter.AimDirection.Y * 24));
        context.DrawLine(AimPen, fighterCenter, aimEnd);
    }

    private static Point ToScreenPoint(Point center, double arenaScale, GameVector position)
    {
        return new Point(
            center.X + (position.X * arenaScale),
            center.Y + (position.Y * arenaScale));
    }
}
