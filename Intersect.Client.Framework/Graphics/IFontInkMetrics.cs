namespace Intersect.Client.Framework.Graphics;

/// <summary>Visible capital-letter ink inside a fixed-cell bitmap font, in cell-relative units.</summary>
public readonly record struct FontInkMetrics(float TopFraction, float HeightFraction);

public interface IFontInkMetrics
{
    FontInkMetrics InkMetrics { get; }
}
