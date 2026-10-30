using Intersect.Enums;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

internal enum ProjectileAutoPattern { Straight, ThreeWay, FiveWay, Radial }
internal enum ProjectileAutoBehavior { Directional, Direct, Homing }

internal sealed record ProjectileAutoProfile
{
    internal ProjectileAutoPattern Pattern { get; init; }
    internal ProjectileAutoBehavior Behavior { get; init; }
    internal int Range { get; init; } = 8;
    internal int TravelTime { get; init; } = 600;
    internal int Bursts { get; init; } = 1;
    internal int Delay { get; init; } = 120;
    internal int Knockback { get; init; }
    internal Guid AnimationId { get; init; }
    internal bool AutoRotate { get; init; } = true;
    internal bool Pierce { get; init; }
    internal bool IgnoreMapBlocks { get; init; }
    internal int Rays => Pattern switch { ProjectileAutoPattern.ThreeWay => 3, ProjectileAutoPattern.FiveWay => 5, ProjectileAutoPattern.Radial => 8, _ => 1 };

    internal void Apply(ProjectileDescriptor projectile)
    {
        if (!Enum.IsDefined(Pattern) || !Enum.IsDefined(Behavior) || Range is < 1 or > 100 ||
            TravelTime is < 1 or > 60000 || Bursts is < 1 or > 20 || Delay is < 1 or > 60000 || Knockback is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(projectile));
        projectile.Range = Range; projectile.Speed = TravelTime; projectile.Quantity = Bursts;
        projectile.Delay = Delay; projectile.Knockback = Knockback;
        projectile.PierceTarget = Pierce; projectile.IgnoreMapBlocks = IgnoreMapBlocks;
        projectile.DirectShotBehavior = Behavior == ProjectileAutoBehavior.Direct;
        projectile.HomingBehavior = Behavior == ProjectileAutoBehavior.Homing;
        // Replace the spawn pattern completely, avoiding stale authored directions.
        for (var x = 0; x < ProjectileDescriptor.SPAWN_LOCATIONS_WIDTH; x++)
        for (var y = 0; y < ProjectileDescriptor.SPAWN_LOCATIONS_HEIGHT; y++)
            projectile.SpawnLocations[x, y] = new Intersect.GameObjects.Location();
        var directions = Pattern switch
        {
            ProjectileAutoPattern.ThreeWay => new[] { Direction.Up, Direction.UpLeft, Direction.UpRight },
            ProjectileAutoPattern.FiveWay => [Direction.Up, Direction.UpLeft, Direction.UpRight, Direction.Left, Direction.Right],
            ProjectileAutoPattern.Radial => Enum.GetValues<Direction>().Where(d => d != Direction.None).ToArray(),
            _ => [Direction.Up],
        };
        foreach (var direction in directions) projectile.SpawnLocations[2, 2].Directions[(int)direction] = true;
        projectile.Animations = [new ProjectileAnimation(AnimationId, Bursts, AutoRotate)];
        // Ammo, collision spell, resource/Z collisions, grappling and folder stay authored.
    }
}
