using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Editor.Networking;
using Intersect.Editor.Forms.Editors.Events;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed partial class FrmCinematicDirector
{
    private void GroupAnimation()
    {
        if (_selection is not CinematicActor {GroupId:var group} actor || group == Guid.Empty)
        { _status.Text=T("SelectBattleGroup");return; }
        var clip=new CinematicClip {Track=CinematicTrack.Animation,Name=T("GroupAnimation"),ActorId=actor.TargetId,
            AttachToActor=true,SynchronizedGroup=true,DurationMs=Math.Min(1000,Math.Max(1,_scene.DurationMs)),
            StartMs=Math.Min(_timeline.CursorMs,Math.Max(0,_scene.DurationMs-1000))};
        if (_scene.Clips.Count >= 512) { _status.Text=T("scene-size");return; }
        _scene.Clips.Add(clip);_timeline.Selected=_viewport.Selected=clip;SelectObject(clip);CommitEdit();ChooseResource();
    }

    private void Reinforcements()
    {
        if (_selection is not CinematicActor {Kind:CinematicActorKind.TemporaryNpc,GroupId:var group} || group == Guid.Empty)
        { _status.Text=T("SelectBattleGroup");return; }
        var actors=_scene.Actors.Where(a=>a.Kind == CinematicActorKind.TemporaryNpc && a.GroupId == group).ToArray();
        if (_scene.Clips.Count+actors.Length > 512) { _status.Text=T("scene-size");return; }
        foreach (var actor in actors)
        {
            actor.VisibleAtStart=false;
            _scene.Clips.Add(new() {Track=CinematicTrack.Actors,Name=T("Reinforcements"),ActorId=actor.TargetId,ActorVisible=true,
                DurationMs=Math.Min(1000,Math.Max(1,_scene.DurationMs)),StartMs=Math.Min(_timeline.CursorMs,Math.Max(0,_scene.DurationMs-1000))});
        }
        _scene.Mode=CinematicSceneMode.BattlefieldScene;CommitEdit();RenderMap();
    }

    private void GroupCharge()
    {
        if (_timeline.Selected is not {Track:CinematicTrack.Movement} clip ||
            !_scene.Actors.Any(a=>a.TargetId == clip.ActorId && a.Kind == CinematicActorKind.TemporaryNpc && a.GroupId != Guid.Empty))
        { _status.Text=T("SelectGroupRoute");return; }
        clip.Speed=4;CommitEdit();CopyGroupPath();
    }

    private void ChooseProjectileResource(CinematicClip clip)
    {
        var primary=new Guid(1,0,0,new byte[8]);
        if (Pick([new Resource(primary,T("ProjectileAnimation")),new Resource(Guid.Empty,T("ImpactAnimationId"))]) is not { } field) return;
        var choices=AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().Select(a=>new Resource(a.Id,a.Name));
        if (field.Id == Guid.Empty) choices=choices.Prepend(new(Guid.Empty,T("NoImpactAnimation")));
        if (Pick(choices) is not { } selected) return;
        if (field.Id == primary) clip.ResourceId=selected.Id;else clip.ImpactAnimationId=selected.Id;
        SelectObject(clip);CommitEdit();RenderMap();
    }

    private void CreateGameplayTrigger()
    {
        if (!SaveScene()) return;
        if (Pick(EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e=>e.CommonEvent && !e.Global)
                .Select(e=>new Resource(e.Id,e.Name))) is not { } callback) return;
        var page=new EventPage();var root=page.CommandLists.Values.First();
        root.Add(new PlayCinematicCommand {MapId=_scene.MapId,SceneEventId=_eventId});
        root.Add(new StartCommmonEventCommand {EventId=callback.Id});
        var trigger=new EventDescriptor(Guid.NewGuid(),true) {Name=_scene.Name+" · "+T("GameplayTransition"),Global=false,Pages=[page]};
        EventDescriptor.Lookup.Set(trigger.Id,trigger);PacketSender.SendSaveObject(trigger);
        _status.Text=T("GameplayTriggerCreated")+" "+trigger.Name;
        // The caller is a common Event, so the cinematic has fully restored its
        // state before the native playable callback runs or changes map/instance.
    }
}
