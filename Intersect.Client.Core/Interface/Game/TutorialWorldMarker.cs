using System.Numerics;
using Intersect.Client.Core;
using Intersect.Client.Entities;
using ClientEvent = Intersect.Client.Entities.Events.Event;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Configuration;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core.Tutorials;
using Intersect.GameObjects;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Screen-space world guide. Uses the same view/zoom projection as quest guidance;
/// never takes mouse/keyboard input or moves player/NPC entities.
/// </summary>
internal sealed class TutorialWorldMarker : Base
{
    private readonly Canvas _canvas;
    private TutorialStep? _step;
    private Vector2 _headAnchor;
    internal bool HasGuide => _step != null;

    internal TutorialWorldMarker(Canvas canvas) : base(canvas, nameof(TutorialWorldMarker))
    {
        _canvas = canvas;
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;
        ClipContents = false;
        SetBounds(0, 0, canvas.Width, canvas.Height);
        Hide();
        SendToBack();
    }

    internal void SetStep(TutorialStep? step)
    {
        _step = step is { HighlightTarget: TutorialHighlightTarget.Player or
            TutorialHighlightTarget.WorldTile or TutorialHighlightTarget.WorldNpc or
            TutorialHighlightTarget.WorldEvent } ? step : null;
        RefreshVisibility();
    }

    internal void RefreshVisibility()
    {
        if (_canvas.Width != Width || _canvas.Height != Height)
            SetBounds(0, 0, _canvas.Width, _canvas.Height);
        var show = _step != null && TryGetWorldAnchor(out _);
        IsHidden = !show;
    }

    internal Rectangle? GetTargetBounds()
    {
        if (IsHidden || !TryGetWorldAnchor(out var world))
            return null;

        var screen = Project(world);
        if (!InView(screen, 12))
            return null;
        var head = Project(_headAnchor);
        return new Rectangle((int)screen.X - 36, (int)head.Y - 56, 72, Math.Max(72, (int)(screen.Y-head.Y)+80));
    }

    private bool TryGetWorldAnchor(out Vector2 anchor)
    {
        anchor = default;
        var player = Globals.Me;
        if (player == null || _step == null)
            return false;

        if (_step.HighlightTarget == TutorialHighlightTarget.Player)
        {
            anchor = player.Origin;
            _headAnchor = player.AnimationAnchor(true);
            return true;
        }

        // Avoid showing markers on a different island or before the target
        // map has loaded. Never substitute a similar-looking NPC name.
        var map = MapInstance.Get(_step.MapId);
        if (map is not { IsLoaded: true })
            return false;

        if (_step.HighlightTarget == TutorialHighlightTarget.WorldTile)
        {
            if (_step.TargetX < 0 || _step.TargetY < 0 ||
                _step.TargetX >= Options.Instance.Map.MapWidth || _step.TargetY >= Options.Instance.Map.MapHeight)
                return false;
            anchor = new Vector2(
                map.X + (_step.TargetX + 0.5f) * Options.Instance.Map.TileWidth,
                map.Y + (_step.TargetY + 0.5f) * Options.Instance.Map.TileHeight
            );
            _headAnchor = anchor;
            return true;
        }

        if (_step.HighlightTarget == TutorialHighlightTarget.WorldEvent)
        {
            // Map events can look exactly like NPCs, but their persistent
            // descriptor ID is the trusted identity of an interaction lesson.
            ClientEvent? selectedEvent = null;
            var closest = float.MaxValue;
            foreach (var local in map.LocalEntities.Values)
            {
                if (local is not ClientEvent eventCandidate || eventCandidate.IsDisposed || eventCandidate.IsHidden || eventCandidate.IsStealthed ||
                    eventCandidate.Id != _step.TargetEventId || eventCandidate.MapId != _step.MapId)
                    continue;
                var squareDistance = Vector2.DistanceSquared(eventCandidate.Center, player.Center);
                if (squareDistance >= closest)
                    continue;
                selectedEvent = eventCandidate;
                closest = squareDistance;
            }
            if (selectedEvent == null)
                return false;
            anchor = selectedEvent.Origin;
            _headAnchor = selectedEvent.AnimationAnchor(true);
            return true;
        }

        if (_step.HighlightTarget != TutorialHighlightTarget.WorldNpc)
            return false;

        Entity? candidate = null;
        var distance = float.MaxValue;
        foreach (var entity in Globals.Entities.Values)
        {
            if (entity.IsDisposed || entity.IsHidden || entity.IsStealthed || entity.Type != EntityType.GlobalEntity ||
                entity.MapId != _step.MapId || entity.NpcDescriptorId != _step.TargetNpcId)
                continue;

            var distanceSquared = Vector2.DistanceSquared(entity.Center, player.Center);
            if (distanceSquared >= distance)
                continue;

            candidate = entity;
            distance = distanceSquared;
        }

        if (candidate == null)
            return false;

        anchor = candidate.Origin;
        _headAnchor = candidate.AnimationAnchor(true);
        return true;
    }

    private Vector2 Project(Vector2 world)
    {
        var zoom = Math.Max(0.01f, Graphics.WorldScale);
        var view = Graphics.CurrentView;
        return ProjectPoint(world, new Vector2(view.Left, view.Top), zoom, _canvas.Scale);
    }

    internal static Vector2 ProjectPoint(Vector2 world, Vector2 origin, float zoom, float uiScale) =>
        (world - origin) * Math.Max(.01f, zoom) / Math.Max(.01f, uiScale);

    private bool InView(Vector2 point, int margin) =>
        point.X >= margin && point.Y >= margin &&
        point.X < Width - margin && point.Y < Height - margin;

    protected override void Render(SkinBase skin)
    {
        if (!TryGetWorldAnchor(out var world))
            return;

        var point = Project(world);
        var pulse = (float)((Math.Sin(Environment.TickCount64 / 190d) + 1d) / 2d);
        var renderer = skin.Renderer;
        var gold = new Color(a: 255, r: 251, g: 203, b: 110);
        var halo = new Color(a: (byte)(115 + pulse * 125f), r: 255, g: 196, b: 88);

        if (!InView(point, 40))
        {
            // Pointer on the viewport edge; no fake line through other maps.
            var center = new Vector2(Width / 2f, Height / 2f);
            var delta = point - center;
            if (delta.LengthSquared() < 0.1f)
                return;
            var direction = Vector2.Normalize(delta);
            const float margin = 42f;
            var factorX = Math.Abs(direction.X) < 0.001f
                ? float.MaxValue : (Width / 2f - margin) / Math.Abs(direction.X);
            var factorY = Math.Abs(direction.Y) < 0.001f
                ? float.MaxValue : (Height / 2f - margin) / Math.Abs(direction.Y);
            var tip = center + direction * Math.Min(factorX, factorY);
            DrawArrow(renderer, tip, direction, gold, 3);
            return;
        }

        var x = (int)Math.Round(point.X);
        var y = (int)Math.Round(point.Y);
        var radius = 20 + (int)Math.Round(pulse * 5);
        if (!DrawAsset(renderer, "royal-guide/guide_ground_v1.png",
                new Rectangle(x-radius-14, y-radius-14, (radius+14)*2, (radius+14)*2), halo))
        {
            Ring(renderer, x, y, radius + 3, halo);
            Ring(renderer, x, y, radius, gold);
        }
        if (_step?.HighlightTarget == TutorialHighlightTarget.Player)
        {
            // Four quick directional cues around the player's feet.
            DrawArrow(renderer, new Vector2(x - radius - 13, y), Vector2.UnitX, gold, 2);
            DrawArrow(renderer, new Vector2(x + radius + 13, y), -Vector2.UnitX, gold, 2);
            DrawArrow(renderer, new Vector2(x, y - radius - 13), Vector2.UnitY, gold, 2);
            DrawArrow(renderer, new Vector2(x, y + radius + 13), -Vector2.UnitY, gold, 2);
        }
        else
        {
            var head = Project(_headAnchor);
            var arrowY = (int)head.Y - 10 - (int)(pulse * 5);
            if (_step?.HighlightTarget is not (TutorialHighlightTarget.WorldNpc or TutorialHighlightTarget.WorldEvent) ||
                !DrawAsset(renderer, "royal-guide/guide_npc_v1.png", new Rectangle(x-24, arrowY-48, 48, 48), Color.White))
                DrawArrow(renderer, new Vector2(x, arrowY), Vector2.UnitY, gold, 3);
        }
    }

    private static bool DrawAsset(RendererBase renderer, string file, Rectangle bounds, Color tint)
    {
        var texture = Globals.ContentManager?.GetTexture(TextureType.Gui, file);
        if (texture is not { IsMissingOrCorrupt: false, IsLoaded: true }) return false;
        renderer.DrawColor = tint;
        renderer.DrawTexturedRect(texture, bounds, tint);
        return true;
    }

    private static void DrawArrow(RendererBase renderer, Vector2 tip, Vector2 direction, Color color, int thickness)
    {
        var normal = new Vector2(-direction.Y, direction.X);
        var basePoint = tip - direction * 15f;
        Segment(renderer, basePoint, tip, color, thickness);
        Segment(renderer, tip, basePoint + normal * 9f, color, thickness + 1);
        Segment(renderer, tip, basePoint - normal * 9f, color, thickness + 1);
    }

    private static void Segment(RendererBase renderer, Vector2 from, Vector2 to, Color color, int width)
    {
        var delta = to - from;
        var count = Math.Max(1, (int)Math.Ceiling(delta.Length()));
        for (var i = 0; i <= count; i += 2)
        {
            var pos = from + delta * (i / (float)count);
            Fill(renderer, color, (int)pos.X - width / 2, (int)pos.Y - width / 2, width, width);
        }
    }

    private static void Ring(RendererBase renderer, int x, int y, int radius, Color color)
    {
        for (var i = 0; i < 96; i++)
        {
            var angle = 2d * Math.PI * i / 96d;
            Fill(renderer, color,
                x + (int)Math.Round(radius * Math.Cos(angle)) - 1,
                y + (int)Math.Round(radius * Math.Sin(angle)) - 1, 3, 3);
        }
    }

    private static void Fill(RendererBase renderer, Color color, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;
        renderer.DrawColor = color;
        renderer.DrawFilledRect(new Rectangle(x, y, width, height));
    }
}
