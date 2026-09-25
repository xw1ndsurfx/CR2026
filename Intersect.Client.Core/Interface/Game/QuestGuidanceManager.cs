using System.Numerics;
using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class QuestGuidanceManager
{
    private sealed record Marker(Guid TaskId, Guid AnimationId, Animation Animation);

    private sealed class QuestArrowOverlay(Base parent) : Base(parent, "QuestObjectiveArrow")
    {
        public bool HasTarget { get; set; }
        public Vector2 TargetWorld { get; set; }

        protected override void Render(SkinBase skin)
        {
            base.Render(skin);
            if (!HasTarget || Globals.Me == null) return;

            var zoom = Math.Max(0.01f, Globals.Database?.WorldZoom ?? 1f);
            var view = Graphics.CurrentView;
            var target = new Vector2(
                (TargetWorld.X - view.Left) * zoom,
                (TargetWorld.Y - view.Top) * zoom
            );

            var center = new Vector2(Width / 2f, Height / 2f);
            var delta = target - center;
            if (delta.LengthSquared() < 16f) return;

            var direction = Vector2.Normalize(delta);
            const float margin = 44f;
            var maxX = Math.Max(margin, Width - margin);
            var maxY = Math.Max(margin, Height - margin);

            var end = target;
            if (target.X < margin || target.Y < margin || target.X > maxX || target.Y > maxY)
            {
                var tx = Math.Abs(direction.X) < 0.001f
                    ? float.MaxValue
                    : (direction.X > 0 ? maxX - center.X : margin - center.X) / direction.X;
                var ty = Math.Abs(direction.Y) < 0.001f
                    ? float.MaxValue
                    : (direction.Y > 0 ? maxY - center.Y : margin - center.Y) / direction.Y;
                var distance = Math.Max(0, Math.Min(Math.Abs(tx), Math.Abs(ty)));
                end = center + direction * distance;
            }

            var start = center + direction * 28f;
            var shaftEnd = end - direction * 14f;
            var normal = new Vector2(-direction.Y, direction.X);

            var renderer = skin.Renderer;
            renderer.DrawColor = new Color(a: 235, r: 238, g: 203, b: 112);
            DrawLine(renderer, start, shaftEnd);
            DrawLine(renderer, end, shaftEnd + normal * 9f);
            DrawLine(renderer, end, shaftEnd - normal * 9f);
            renderer.DrawColor = new Color(a: 130, r: 40, g: 26, b: 20);
            DrawLine(renderer, start + normal * 2f, shaftEnd + normal * 2f);
        }

        private static void DrawLine(RendererBase renderer, Vector2 a, Vector2 b) =>
            renderer.DrawLine(
                (int)Math.Round(a.X),
                (int)Math.Round(a.Y),
                (int)Math.Round(b.X),
                (int)Math.Round(b.Y)
            );
    }

    private readonly Canvas _canvas;
    private readonly QuestArrowOverlay _arrow;
    private readonly Dictionary<Guid, Marker> _markers = [];

    public QuestGuidanceManager(Canvas canvas)
    {
        _canvas = canvas;
        _arrow = new QuestArrowOverlay(canvas)
        {
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
            IsHidden = true,
        };
        _arrow.SetBounds(0, 0, Math.Max(1, canvas.Width), Math.Max(1, canvas.Height));
        _arrow.BringToFront();
    }

    public void Update()
    {
        if (Globals.Me == null)
        {
            Clear();
            return;
        }

        if (_arrow.Width != _canvas.Width || _arrow.Height != _canvas.Height)
            _arrow.SetBounds(0, 0, Math.Max(1, _canvas.Width), Math.Max(1, _canvas.Height));

        var desiredMarkers = new Dictionary<Guid, (Guid TaskId, Guid AnimationId, Entity Entity)>();
        Entity? nearestArrowTarget = null;
        var nearestDistance = float.MaxValue;

        foreach (var (questId, progress) in Globals.Me.QuestProgress)
        {
            if (progress.Completed || progress.TaskId == Guid.Empty) continue;
            if (!QuestDescriptor.TryGet(questId, out var quest)) continue;

            var task = quest.FindTask(progress.TaskId);
            if (task == null) continue;

            foreach (var entity in MatchingEntities(task))
            {
                if (task.GuideAnimationId != Guid.Empty)
                    desiredMarkers[entity.Id] = (task.Id, task.GuideAnimationId, entity);

                if (!task.ShowNavigationArrow) continue;

                var delta = entity.Center - Globals.Me.Center;
                var distance = delta.LengthSquared();
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestArrowTarget = entity;
                }
            }
        }

        SynchronizeMarkers(desiredMarkers);

        if (nearestArrowTarget != null)
        {
            _arrow.HasTarget = true;
            _arrow.TargetWorld = nearestArrowTarget.Center;
            _arrow.IsHidden = false;
            _arrow.BringToFront();
        }
        else
        {
            _arrow.HasTarget = false;
            _arrow.IsHidden = true;
        }
    }

    private static IEnumerable<Entity> MatchingEntities(QuestTaskDescriptor task)
    {
        foreach (var entity in Globals.Entities.Values)
        {
            if (entity.MapInstance == null) continue;

            if (task.Objective == QuestObjective.KillNpcs &&
                entity.Type == EntityType.GlobalEntity &&
                entity.NpcDescriptorId == task.TargetId)
            {
                yield return entity;
                continue;
            }

            if (task.Objective == QuestObjective.GatherItems &&
                task.GuideResourceId != Guid.Empty &&
                entity is Resource resource &&
                !resource.IsDead &&
                resource.Descriptor?.Id == task.GuideResourceId)
            {
                yield return resource;
            }
        }
    }

    private void SynchronizeMarkers(
        IReadOnlyDictionary<Guid, (Guid TaskId, Guid AnimationId, Entity Entity)> desired)
    {
        foreach (var (entityId, marker) in _markers.ToArray())
        {
            if (!desired.TryGetValue(entityId, out var target) ||
                target.TaskId != marker.TaskId ||
                target.AnimationId != marker.AnimationId)
            {
                marker.Animation.Dispose();
                _markers.Remove(entityId);
            }
        }

        foreach (var (entityId, target) in desired)
        {
            if (_markers.ContainsKey(entityId)) continue;
            if (!AnimationDescriptor.TryGet(target.AnimationId, out var descriptor)) continue;

            var animation = new Animation(descriptor, true, false, -1, target.Entity);
            _markers[entityId] = new Marker(target.TaskId, target.AnimationId, animation);
        }
    }

    public void Clear()
    {
        foreach (var marker in _markers.Values)
            marker.Animation.Dispose();
        _markers.Clear();

        _arrow.HasTarget = false;
        _arrow.IsHidden = true;
    }
}
