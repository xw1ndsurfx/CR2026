using System.Numerics;

namespace Intersect.Client.Core.Controls;

internal readonly record struct JoystickSample(Vector2 Offset, int Horizontal, int Vertical);

internal sealed class VirtualJoystick
{
    internal int? FingerId { get; private set; }
    internal JoystickSample Current { get; private set; }

    internal bool TryCapture(int fingerId)
    {
        if (FingerId != null) return false;
        FingerId = fingerId;
        return true;
    }

    internal void Update(int fingerId, Vector2 displacement, float travelRadius)
    {
        if (FingerId != fingerId) return;
        Current = Sample(displacement, travelRadius);
    }

    internal void Release(int fingerId)
    {
        if (FingerId == fingerId) Cancel();
    }

    internal void Cancel() { FingerId = null; Current = default; }

    internal static JoystickSample Sample(Vector2 displacement, float travelRadius)
    {
        if (!float.IsFinite(displacement.X) || !float.IsFinite(displacement.Y) ||
            !float.IsFinite(travelRadius) || travelRadius <= 0) return default;
        var offset = displacement / travelRadius;
        var length = offset.Length();
        if (length > 1) offset /= length;
        if (length <= .18f) return new(offset, 0, 0);
        // Eight sectors, with a neutral center to avoid drifting after release.
        const float diagonalBoundary = .41421356f; // tan(22.5 degrees)
        var horizontal = Math.Abs(offset.X) >= Math.Abs(offset.Y) * diagonalBoundary ? Math.Sign(offset.X) : 0;
        var vertical = Math.Abs(offset.Y) >= Math.Abs(offset.X) * diagonalBoundary ? Math.Sign(offset.Y) : 0;
        return new(offset, horizontal, vertical);
    }
}
