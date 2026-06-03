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
    private static readonly IBrush HealthBackBrush = new SolidColorBrush(Color.Parse("#2B3036"));
    private static readonly IBrush HealthBrush = new SolidColorBrush(Color.Parse("#62D26F"));
    private static readonly IBrush CooldownReadyBrush = new SolidColorBrush(Color.Parse("#F1C75B"));
    private static readonly IBrush CooldownBackBrush = new SolidColorBrush(Color.Parse("#4B5560"));
    private static readonly IBrush ProjectileBrush = new SolidColorBrush(Color.Parse("#F4D47B"));
    private static readonly IBrush HitBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
    private static readonly IBrush DeathBrush = new SolidColorBrush(Color.FromArgb(140, 255, 80, 80));
    private static readonly Pen MeleeEffectPen = new(new SolidColorBrush(Color.FromArgb(180, 255, 220, 130)), 3);
    private static readonly Pen RangedEffectPen = new(new SolidColorBrush(Color.FromArgb(170, 140, 210, 255)), 2);

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

        foreach (var projectile in MatchSnapshot.Projectiles)
        {
            DrawProjectile(context, center, arenaScale, projectile);
        }

        foreach (var effect in MatchSnapshot.Effects)
        {
            DrawEffect(context, center, arenaScale, effect);
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
        var fighterRadius = MatchRules.GetFighterRadius(fighter.Role);
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

        DrawHealthBar(context, fighterCenter, fighter, fighterRadius);
        DrawCooldownIndicators(context, fighterCenter, fighter, fighterRadius);
    }

    private static void DrawProjectile(
        DrawingContext context,
        Point center,
        double arenaScale,
        ProjectileState projectile)
    {
        var projectileCenter = ToScreenPoint(center, arenaScale, projectile.Position);
        var radius = Math.Max(3, projectile.Radius * arenaScale);
        var projectileRect = new Rect(
            projectileCenter.X - radius,
            projectileCenter.Y - radius,
            radius * 2,
            radius * 2);

        context.DrawEllipse(ProjectileBrush, null, projectileRect);
    }

    private static void DrawEffect(
        DrawingContext context,
        Point center,
        double arenaScale,
        CombatEffect effect)
    {
        var effectCenter = ToScreenPoint(center, arenaScale, effect.Position);
        var radius = Math.Max(4, effect.Radius * arenaScale);
        var rect = new Rect(
            effectCenter.X - radius,
            effectCenter.Y - radius,
            radius * 2,
            radius * 2);

        switch (effect.Kind)
        {
            case CombatEffectKind.UniversalDash:
                context.DrawEllipse(null, RangedEffectPen, rect);
                break;
            case CombatEffectKind.MeleeFrontalStrike:
                context.DrawLine(
                    MeleeEffectPen,
                    effectCenter,
                    new Point(
                        effectCenter.X + (effect.Direction.X * radius),
                        effectCenter.Y + (effect.Direction.Y * radius)));
                break;
            case CombatEffectKind.MeleeAreaSlash:
                context.DrawEllipse(null, MeleeEffectPen, rect);
                break;
            case CombatEffectKind.RangedSingleArrowShot:
            case CombatEffectKind.RangedConeVolley:
                context.DrawLine(
                    RangedEffectPen,
                    effectCenter,
                    new Point(
                        effectCenter.X + (effect.Direction.X * radius),
                        effectCenter.Y + (effect.Direction.Y * radius)));
                break;
            case CombatEffectKind.Hit:
                context.DrawEllipse(HitBrush, null, rect);
                break;
            case CombatEffectKind.Death:
                context.DrawEllipse(DeathBrush, null, rect);
                break;
        }
    }

    private static void DrawHealthBar(
        DrawingContext context,
        Point fighterCenter,
        FighterState fighter,
        double fighterRadius)
    {
        var maxHealth = MatchRules.GetStartingHealth(fighter.Role);
        var healthRatio = maxHealth == 0
            ? 0
            : Math.Clamp((double)fighter.Health / maxHealth, 0, 1);
        var barWidth = 34;
        var barHeight = 4;
        var barLeft = fighterCenter.X - (barWidth / 2);
        var barTop = fighterCenter.Y - fighterRadius - 10;
        var backRect = new Rect(barLeft, barTop, barWidth, barHeight);
        var healthRect = new Rect(barLeft, barTop, barWidth * healthRatio, barHeight);

        context.DrawRectangle(HealthBackBrush, null, backRect);
        context.DrawRectangle(HealthBrush, null, healthRect);
    }

    private static void DrawCooldownIndicators(
        DrawingContext context,
        Point fighterCenter,
        FighterState fighter,
        double fighterRadius)
    {
        var primaryMax = fighter.Role == FighterRole.Melee
            ? MatchRules.MeleeFrontalStrikeCooldownSeconds
            : MatchRules.RangedSingleArrowShotCooldownSeconds;
        var abilityMax = fighter.Role == FighterRole.Melee
            ? MatchRules.MeleeAreaSlashCooldownSeconds
            : MatchRules.RangedConeVolleyCooldownSeconds;

        DrawCooldownBar(
            context,
            new Point(fighterCenter.X - 13, fighterCenter.Y + fighterRadius + 7),
            fighter.PrimaryAttackCooldownSeconds,
            primaryMax);
        DrawCooldownBar(
            context,
            new Point(fighterCenter.X + 3, fighterCenter.Y + fighterRadius + 7),
            fighter.RoleAbilityCooldownSeconds,
            abilityMax);
    }

    private static void DrawCooldownBar(
        DrawingContext context,
        Point topLeft,
        double remaining,
        double max)
    {
        const double width = 10;
        const double height = 3;
        var readyRatio = max <= 0
            ? 1
            : 1 - Math.Clamp(remaining / max, 0, 1);
        var backRect = new Rect(topLeft.X, topLeft.Y, width, height);
        var readyRect = new Rect(topLeft.X, topLeft.Y, width * readyRatio, height);

        context.DrawRectangle(CooldownBackBrush, null, backRect);
        context.DrawRectangle(CooldownReadyBrush, null, readyRect);
    }

    private static Point ToScreenPoint(Point center, double arenaScale, GameVector position)
    {
        return new Point(
            center.X + (position.X * arenaScale),
            center.Y + (position.Y * arenaScale));
    }
}
