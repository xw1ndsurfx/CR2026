using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.Networking;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Enums;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    private sealed record View(ShipSnapshot Previous, ShipSnapshot Current, long Received, ShipAnimationClock Animation);
    private static Dictionary<Guid, View> _ships = [];
    private static Guid _viewer;
    internal static NavalRewardFeed RewardFeed {get;}=new();
    private static long _received, _inputTime;
    internal static void Clear() {RewardFeed.Clear(); _ships.Clear(); _viewer = Guid.Empty; _received = _inputTime = 0; NavalSpriteCatalog.Clear(); ClearActions(); ClearPresentation(); }
    internal static bool IsCaptain(Guid playerId) => playerId != Guid.Empty && Fresh && _ships.Values.Any(v => v.Current.CaptainId == playerId);
    private static bool Fresh => Globals.Me != null && _viewer == Globals.Me.Id && Environment.TickCount64 - _received < 1500;

    internal static void Apply(NavalSnapshotPacket packet)
    {
        var now = Environment.TickCount64;
        var next = new Dictionary<Guid, View>();
        foreach (var s in packet.Ships.Take(64))
        {
            var continuing = _ships.TryGetValue(s.Id, out var v) && Fresh;
            var previous = continuing ? v!.Current : s;
            var animation = continuing ? v!.Animation : new ShipAnimationClock();
            animation.Observe(s.Graphics, now);
            next[s.Id] = new(previous, s, now, animation);
        }
        RewardFeed.Observe(packet.Rewards??[],_viewer==Guid.Empty || _viewer!=Globals.Me?.Id,now);
        ApplyPresentation(packet,now);
        ApplyCannonballs(packet,now);
        _ships = next; _viewer = Globals.Me?.Id ?? Guid.Empty; _received = now;
        // The engine's integer position remains an anchor for map loading and ordinary entity lookup.
        // Rendering and camera use the independent interpolated ship center below.
        foreach (var view in next.Values)
            if (view.Current.CaptainId != Guid.Empty && Globals.Entities.TryGetValue(view.Current.CaptainId, out var captain) &&
                captain.MapId == view.Current.MapId)
            { captain.X = (byte)Math.Floor(view.Current.X); captain.Y = (byte)Math.Floor(view.Current.Y); captain.OffsetX = captain.OffsetY = 0; }
    }

    internal static void Input(bool busy)
    {
        if (!Fresh || Globals.Me == null) return;
        ReadActions(busy);
        var own = _ships.Values.FirstOrDefault(v => v.Current.CaptainId == Globals.Me.Id)?.Current;
        if (own == null || Environment.TickCount64 - _inputTime < 50) return;
        _inputTime = Environment.TickCount64;
        bool Down(Keys key) => !busy && Globals.InputManager.IsKeyDown(key);
        Intersect.Client.Networking.Network.SendPacket(new ShipInputPacket { ShipId = own.Id,
            Thrust = (sbyte)((Down(Keys.W) ? 1 : 0) - (Down(Keys.S) ? 1 : 0)),
            Turn = (sbyte)((Down(Keys.D) ? 1 : 0) - (Down(Keys.A) ? 1 : 0)), Anchor = busy || Down(Keys.Space) });
    }

    private static bool Position(View view, out float x, out float y, out double heading)
    {
        x = y = 0; heading = view.Current.Heading;
        if (!MapInstance.TryGet(view.Current.MapId, out var map) || !Globals.GridMaps.ContainsKey(view.Current.MapId)) return false;
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        var targetX = map.X + (float)view.Current.X * tw; var targetY = map.Y + (float)view.Current.Y * th;
        var fromX = targetX; var fromY = targetY;
        if (MapInstance.TryGet(view.Previous.MapId, out var oldMap) && Globals.GridMaps.ContainsKey(view.Previous.MapId))
        { fromX = oldMap.X + (float)view.Previous.X * tw; fromY = oldMap.Y + (float)view.Previous.Y * th; }
        var t = Math.Clamp((Environment.TickCount64 - view.Received) / 100f, 0, 1);
        x = fromX + (targetX - fromX) * t; y = fromY + (targetY - fromY) * t;
        var angle = (view.Current.Heading - view.Previous.Heading + 540) % 360 - 180;
        heading = ShipMovement.Normalize(view.Previous.Heading + angle * t);
        return true;
    }

    internal static bool TryCamera(out float x, out float y)
    {
        x = y = 0;
        if (!Fresh) return false;
        var view = _ships.Values.FirstOrDefault(v => v.Current.CaptainId == _viewer);
        return view != null && Position(view, out x, out y, out _);
    }

    internal static bool TryCaptainCenter(Guid captainId, out float x, out float y)
    {
        x = y = 0;
        if (!Fresh) return false;
        var view = _ships.Values.FirstOrDefault(v => v.Current.CaptainId == captainId && captainId != Guid.Empty);
        return view != null && Position(view,out x,out y,out _);
    }

    internal static bool TryHullBounds(Guid captainId, out FloatRect bounds)
    {
        bounds = default;
        if (!Fresh) return false;
        var view = _ships.Values.FirstOrDefault(v => v.Current.CaptainId == captainId && captainId != Guid.Empty);
        if (view == null || !Position(view,out var x,out var y,out _)) return false;
        var width = (float)view.Current.Width * Options.Instance.Map.TileWidth;
        var length = (float)view.Current.Length * Options.Instance.Map.TileHeight;
        bounds = new(x-width/2,y-length/2,width,length);
        return true;
    }

    internal static void DrawRow(int row)
    {
        if (!Fresh || Graphics.Renderer == null) return;
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        DrawCannonballs(row);
        foreach (var view in _ships.Values)
        {
            if (!Position(view, out var x, out var y, out var heading)) continue;
            // Same world-row ordering as ordinary entities, before the fringe layers.
            var currentMap = Globals.Me?.MapInstance;
            if (currentMap == null || row != (int)Math.Floor((y - currentMap.Y) / th) + Options.Instance.Map.MapHeight * 2) continue;
            var s = view.Current;

            var texture = Globals.ContentManager?.GetTexture(TextureType.Entity, s.Graphics);
            var oceanName = s.Graphics;
            var oceanTexture = Globals.ContentManager?.GetTexture(TextureType.Ocean, oceanName);
            if (oceanTexture == null && texture == null)
            {
                oceanName = NavalSpriteCatalog.LegacyAlias(s.Graphics);
                oceanTexture = Globals.ContentManager?.GetTexture(TextureType.Ocean, oceanName);
            }
            if (oceanTexture != null)
            {
                var atlas = NavalSpriteCatalog.Get(oceanName, oceanTexture.Width, oceanTexture.Height, s.Frames);
                if (atlas != null)
                {
                    var pose = atlas.Frame(heading, view.Animation.Elapsed(Environment.TickCount64), s.FrameMilliseconds);
                    var scale = (float)(tw / atlas.PixelsPerTile);
                    DrawShipName(ShipNameplate(s),x,y-(float)(pose.AnchorY*scale)-22);
                    Graphics.DrawGameTexture(oceanTexture, new FloatRect(pose.X, pose.Y, pose.Width, pose.Height),
                        NavalSpriteGeometry.Destination(pose,x,y,scale), Color.White);
                    continue;
                }
                DrawShipName(ShipNameplate(s),x,y-(float)s.Length*th/2-22);
                // An ocean atlas is never silently interpreted as the legacy regular grid.
                DrawPrototype(x, y, heading, (float)s.Width * tw, (float)s.Length * th, Math.Abs(s.Speed) > .05);
                continue;
            }
            if (texture != null && s.Frames > 0 && texture.Width % s.Frames == 0 && texture.Height % 8 == 0)
            {
                var w = texture.Width / s.Frames; var h = texture.Height / 8;
                var frame = (int)(Environment.TickCount64 / Math.Max(50, s.FrameMilliseconds) % s.Frames);
                DrawShipName(ShipNameplate(s),x,y-h/2f-22);
                Graphics.DrawGameTexture(texture, new FloatRect(frame * w, ShipMovement.DirectionRow(heading) * h, w, h),
                    new FloatRect(x - w / 2f, y - h / 2f, w, h), Color.White);
                continue;
            }
            DrawShipName(ShipNameplate(s),x,y-(float)s.Length*th/2-22);
            DrawPrototype(x, y, heading, (float)s.Width * tw, (float)s.Length * th, Math.Abs(s.Speed) > .05);
        }
        DrawNavalEffects(row);
    }

    // Explicit code-drawn test hull, never presented as the unavailable royal artwork.
    private static void DrawPrototype(float x, float y, double heading, float width, float length, bool moving)
    {
        var angle = ShipMovement.DirectionRow(heading) * Math.PI / 4;
        void Pixel(float side, float forward, float size, Color color)
        {
            var px = x + (float)(Math.Cos(angle) * side + Math.Sin(angle) * forward);
            var py = y + (float)(Math.Sin(angle) * side - Math.Cos(angle) * forward);
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel, new FloatRect(0, 0, 1, 1),
                new FloatRect((int)px - size / 2, (int)py - size / 2, size, size), color);
        }
        for (var fore = -length / 2; fore <= length / 2; fore += 4)
        {
            var half = width / 2 * (1 - .65f * Math.Abs(fore) / (length / 2));
            for (var side = -half; side <= half; side += 4) Pixel(side, fore, 4, new Color(255, 35, 65, 105));
            Pixel(-half, fore, 4, new Color(255, 195, 155, 55)); Pixel(half, fore, 4, new Color(255, 195, 155, 55));
        }
        for (var side = -width / 2; side <= width / 2; side += 4) Pixel(side, length / 6, 4, new Color(255, 225, 225, 205));
        for (var fore = -length / 4; fore < length / 3; fore += 4) Pixel(0, fore, 4, new Color(255, 195, 155, 55));
        if (moving)
            for (var i = 0; i < 3; i++) Pixel((i - 1) * 8, -length / 2 - 8 - Environment.TickCount64 / 150 % 3 * 3,
                4, new Color(160, 150, 210, 235));
    }
}
