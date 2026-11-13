using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Core.Controls;

public static class MobileHudLayout
{
    public static Rectangle AvoidControls(Rectangle desired, Rectangle safe, IReadOnlyList<Rectangle> controls)
    {
        var width = Math.Min(desired.Width, safe.Width);
        var height = Math.Min(desired.Height, safe.Height);
        Rectangle Fit(int x, int y) => new(Math.Clamp(x, safe.X, safe.Right - width),
            Math.Clamp(y, safe.Y, safe.Bottom - height), width, height);
        var start = Fit(desired.X, desired.Y);
        var candidates = new List<Rectangle> { start, Fit(safe.X, start.Y), Fit(start.X, safe.Y) };
        foreach (var obstacle in controls)
        {
            candidates.Add(Fit(obstacle.X - width - 8, start.Y));
            candidates.Add(Fit(obstacle.Right + 8, start.Y));
            candidates.Add(Fit(start.X, obstacle.Y - height - 8));
            candidates.Add(Fit(start.X, obstacle.Bottom + 8));
        }
        bool Overlaps(Rectangle a, Rectangle b) => a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
        return candidates.Where(candidate => controls.All(control => !Overlaps(candidate, control)))
            .OrderBy(candidate => Math.Abs(candidate.X - start.X) + Math.Abs(candidate.Y - start.Y) * 2)
            .FirstOrDefault(start);
    }
}
