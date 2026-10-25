using Microsoft.Xna.Framework;

namespace Intersect.Rendering;

// Source-linked into WindowsDX editor and DesktopGL client so both use the
// native cannon projection without loading either application's runtime.
internal static class MapPerspectiveGeometry
{
    internal static int SpriteRow(int row, float cameraHeading)
    {
        var heading = row switch { 1 => 270f, 2 => 90f, 3 => 0f, _ => 180f };
        var quadrant = (int)System.Math.Floor(((heading-cameraHeading+360)%360+45)/90)%4;
        return quadrant switch { 0=>3,1=>2,2=>0,_=>1 };
    }

    internal static Matrix View(Vector2 camera, Vector2 forward, float tw, float th, float height, float pitch)
    {
        var eye = new Vector3(camera.X / tw, height, camera.Y / th);
        return Matrix.CreateLookAt(eye, eye + new Vector3(forward.X,
            (float)System.Math.Tan(MathHelper.ToRadians(pitch)), forward.Y), Vector3.Up);
    }

    internal static Matrix Projection(float fieldOfView, float aspect) =>
        Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fieldOfView), aspect, .15f, 96);

    internal static bool GroundPoint(Vector2 screen, int width, int height, Matrix view, Matrix projection, out Vector2 point)
    {
        var viewport = new Microsoft.Xna.Framework.Graphics.Viewport(0,0,width,height);
        var near = viewport.Unproject(new Vector3(screen,0),projection,view,Matrix.Identity);
        var far = viewport.Unproject(new Vector3(screen,1),projection,view,Matrix.Identity);
        var direction = far-near; point = default;
        if (!float.IsFinite(direction.Y) || direction.Y >= -.00001f) return false;
        var distance = -near.Y/direction.Y;
        if (distance < 0 || distance > 1) return false;
        var hit = near+direction*distance;
        point = new(hit.X,hit.Z);
        return float.IsFinite(point.X) && float.IsFinite(point.Y);
    }

    internal static Matrix TerrainPlane(float tw, float th) => new(
        1/tw, 0, 0, 0, 0, 0, 1/th, 0, 0, 1, 0, 0, 0, 0, 0, 1);

    internal static Matrix UprightPlane(Vector2 anchor, Vector2 forward, float tw, float th)
    {
        var right = new Vector3(-forward.Y, 0, forward.X);
        return new Matrix(right.X/tw,0,right.Z/tw,0, 0,-1/th,0,0, 0,0,0,0,
            anchor.X/tw-right.X*anchor.X/tw,anchor.Y/th,anchor.Y/th-right.Z*anchor.X/tw,1);
    }
}
