using DarkUI.Forms;
using Intersect.Editor.General;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed partial class FrmCinematicDirector
{
    private static readonly Dictionary<Guid,WeakReference<FrmCinematicDirector>> VesselRequests=[];
    internal static IReadOnlyDictionary<string,CinematicVesselModel> VesselModels { get; private set; } = new Dictionary<string,CinematicVesselModel>();
    internal static NavalConfiguration VesselConfiguration { get; private set; } = new();
    private bool _vesselModelsReady,_addVesselAfterResponse;
    private Guid _vesselRequest;

    private void RequestVesselModels(bool add)
    {
        _addVesselAfterResponse|=add;
        if (_vesselRequest != Guid.Empty) return;
        var request=new NavalConfigurationRequestPacket();_vesselRequest=request.RequestId;
        VesselRequests[request.RequestId]=new(this);_vesselModelsReady=false;
        _status.Text=T("LoadingVesselModels");Intersect.Editor.Networking.Network.SendPacket(request);
    }

    internal static bool ReceiveVesselModels(NavalConfigurationPacket packet)
    {
        if (!VesselRequests.Remove(packet.RequestId,out var pending)) return false;
        if (!pending.TryGetTarget(out var form) || form.IsDisposed) return true;
        form._vesselRequest=Guid.Empty;
        try
        {
            if (packet.Error.Length > 0 || packet.NavigationJson.Length > 1_000_000 || packet.CatalogueJson.Length > 1_000_000)
                throw new InvalidOperationException();
            var settings=new JsonSerializerSettings {MaxDepth=12,TypeNameHandling=TypeNameHandling.None};
            var navigation=JsonConvert.DeserializeObject<NavalConfiguration>(packet.NavigationJson,settings) ?? throw new InvalidOperationException();
            var catalogue=JsonConvert.DeserializeObject<HarbourCatalog>(packet.CatalogueJson,settings) ?? throw new InvalidOperationException();
            navigation.Validate(Options.Instance.Map.MapWidth,Options.Instance.Map.MapHeight);catalogue.Validate();
            VesselConfiguration=navigation;
            VesselModels=CinematicVessels.Models(navigation,catalogue);form._vesselModelsReady=true;
            form._status.Text=T("VesselModelsReady");form.RenderMap();
            if (form._addVesselAfterResponse) { form._addVesselAfterResponse=false;form.AddVessel(); }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or InvalidDataException)
        { form._status.Text=T("VesselModelsUnavailable");form._addVesselAfterResponse=false; }
        return true;
    }

    private CinematicVesselModel? PickVesselModel()
    {
        var choices=VesselModels.Values.ToDictionary(_=>Guid.NewGuid(),model=>model);
        var selected=Pick(choices.Select(pair=>new Resource(pair.Key,
            (string.IsNullOrWhiteSpace(pair.Value.Name) ? T("TemporaryVessel") : pair.Value.Name)+" · "+pair.Value.Key)));
        return selected != null ? choices.GetValueOrDefault(selected.Id) : null;
    }

    private static bool VesselPositionClear(CinematicActor actor,double x,double y)
    {
        if (Globals.CurrentMap is not { } map || !VesselModels.TryGetValue(actor.ShipModelKey,out var model)) return false;
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight) return false;
        return ShipFootprint.Tiles(model.Definition.Width,model.Definition.Length,x,y).All(tile=>tile.X >= 0 && tile.Y >= 0 &&
            tile.X < Options.Instance.Map.MapWidth && tile.Y < Options.Instance.Map.MapHeight &&
            !VesselBlocking(map.Attributes[tile.X,tile.Y]));
    }
    internal static bool VesselBlocking(MapAttribute attribute) =>
        attribute is MapDockAttribute or MapBlockedAttribute or MapWarpAttribute or MapResourceAttribute or MapSlideAttribute or MapGrappleStoneAttribute ||
        attribute is MapAnimationAttribute { IsBlock: true } || attribute is MapZDimensionAttribute;

    private void AddVessel()
    {
        if (!IsVesselMap()) { _status.Text=T("vessel-ocean-map");return; }
        if (!_vesselModelsReady) { RequestVesselModels(true);return; }
        if (VesselModels.Count == 0) { _status.Text=T("NoVesselModels");return; }
        if (_scene.Actors.Count >= 64 || _scene.Actors.Count(a=>a.Kind == CinematicActorKind.TemporaryVessel) >= CinematicVessels.MaximumVessels)
        { _status.Text=T("vessel-count");return; }
        if (PickVesselModel() is not { } model) return;
        var actor=new CinematicActor {Name=string.IsNullOrWhiteSpace(model.Name)?T("TemporaryVessel"):model.Name,
            TargetId=Guid.NewGuid(),Kind=CinematicActorKind.TemporaryVessel,ShipModelKey=model.Key};
        var placement=Enumerable.Range(0,Options.Instance.Map.MapWidth).SelectMany(x=>Enumerable.Range(0,Options.Instance.Map.MapHeight).Select(y=>(X:x,Y:y)))
            .OrderBy(p=>Math.Abs(p.X-Options.Instance.Map.MapWidth/2)+Math.Abs(p.Y-Options.Instance.Map.MapHeight/2))
            .Where(p=>VesselPositionClear(actor,p.X+.5,p.Y+.5)).Select(p=>((int X,int Y)?)p).FirstOrDefault();
        if (placement is not { } tile) { _status.Text=T("vessel-position");return; }
        actor.StartX=tile.X;actor.StartY=tile.Y;_scene.Actors.Add(actor);
        _scene.Mode=CinematicSceneMode.NavalScene;SelectObject(actor);CommitEdit();RenderMap();
    }

    private void ChooseVesselModel(CinematicActor actor)
    {
        if (!_vesselModelsReady) { RequestVesselModels(false);return; }
        if (PickVesselModel() is not { } model) return;
        var previous=actor.ShipModelKey;actor.ShipModelKey=model.Key;
        if (!VesselPositionClear(actor,actor.StartX+.5,actor.StartY+.5)) { actor.ShipModelKey=previous;_status.Text=T("vessel-position");return; }
        SelectObject(actor);CommitEdit();RenderMap();
    }

    private void ValidateVesselPaths()
    {
        if (_scene.Actors.Any(a=>a.Kind == CinematicActorKind.TemporaryVessel) && !IsVesselMap()) throw new InvalidOperationException("vessel-ocean-map");
        foreach (var actor in _scene.Actors.Where(a=>a.Kind == CinematicActorKind.TemporaryVessel))
        {
            if (!_vesselModelsReady || !VesselModels.ContainsKey(actor.ShipModelKey)) throw new InvalidOperationException("vessel-model");
            if (!VesselPositionClear(actor,actor.StartX+.5,actor.StartY+.5)) throw new InvalidOperationException("vessel-position");
            foreach (var clip in _scene.Clips.Where(c=>c.Track == CinematicTrack.Movement && c.ActorId == actor.TargetId))
            {
                if (clip.Points == null || clip.Points.Count > 256 || clip.Points.Any(p=>p == null || p.X < 0 || p.Y < 0 ||
                        p.X >= Options.Instance.Map.MapWidth || p.Y >= Options.Instance.Map.MapHeight)) throw new InvalidOperationException("vessel-path");
                for (var i=1;i<clip.Points.Count;i++)
                {
                    var a=clip.Points[i-1];var b=clip.Points[i];
                    var distance=Math.Sqrt(Math.Pow((double)b.X-a.X,2)+Math.Pow((double)b.Y-a.Y,2));
                    if (!double.IsFinite(distance) || distance > 256) throw new InvalidOperationException("vessel-path");
                    var steps=Math.Max(1,(int)Math.Ceiling(distance*4));
                    for (var sample=0;sample<=steps;sample++)
                        if (!VesselPositionClear(actor,a.X+.5+(b.X-a.X)*sample/(double)steps,a.Y+.5+(b.Y-a.Y)*sample/(double)steps))
                            throw new InvalidOperationException("vessel-position");
                }
            }
        }
    }

    private static bool IsVesselMap()
    {
        var target=Globals.CurrentMap?.Id ?? Guid.Empty;
        bool Find(MapList list,bool ocean) => list.Items.ToArray().Any(item=>item is MapListMap map ? ocean && map.MapId == target :
            item is MapListFolder folder && Find(folder.Children,ocean || string.Equals(folder.Name,"Ocean",StringComparison.OrdinalIgnoreCase)));
        return target != Guid.Empty && Find(MapList.List,false);
    }

    private void AddFleetFormation()
    {
        if (_selection is not CinematicActor {Kind:CinematicActorKind.TemporaryVessel} leader) { _status.Text=T("SelectVesselFirst");return; }
        using var dialog=new DarkForm {Text=T("FleetFormation"),Width=430,Height=260,StartPosition=FormStartPosition.CenterParent};
        var formation=new ComboBox {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};
        foreach (var kind in Enum.GetValues<CinematicFormationKind>()) formation.Items.Add(T(kind.ToString()));formation.SelectedIndex=0;
        var count=new NumericUpDown {Dock=DockStyle.Top,Minimum=2,Maximum=16,Value=4};
        var spacing=new NumericUpDown {Dock=DockStyle.Top,Minimum=1,Maximum=8,Value=6};
        var accept=new Button {Text=T("Accept"),Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};
        dialog.Controls.AddRange([new Label {Text=T("FleetFormationHint"),Dock=DockStyle.Top,Height=55},spacing,count,formation,accept]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var offsets=CinematicFormations.Offsets((CinematicFormationKind)formation.SelectedIndex,(int)count.Value,(int)spacing.Value);
        if (_scene.Actors.Count+offsets.Count-1 > 64 || _scene.Actors.Count(a=>a.Kind == CinematicActorKind.TemporaryVessel)+offsets.Count-1 > 16)
        { _status.Text=T("vessel-count");return; }
        var group=Guid.NewGuid();var copies=offsets.Select(p=>new CinematicActor {Name=leader.Name,Kind=CinematicActorKind.TemporaryVessel,
            TargetId=Guid.NewGuid(),ShipModelKey=leader.ShipModelKey,StartX=leader.StartX+p.X-offsets[0].X,StartY=leader.StartY+p.Y-offsets[0].Y,
            StartHeading=leader.StartHeading,VesselAppearance=leader.VesselAppearance,GroupId=group,GroupName=leader.Name,
            VisibleAtStart=leader.VisibleAtStart,HideName=leader.HideName}).ToArray();
        if (copies.Any(a=>!VesselPositionClear(a,a.StartX+.5,a.StartY+.5))) { _status.Text=T("vessel-position");return; }
        // Replace only the selected anchor; existing authored paths keep its stable actor id.
        var anchor=copies[0];anchor.TargetId=leader.TargetId;
        _scene.Actors[_scene.Actors.IndexOf(leader)]=anchor;_scene.Actors.AddRange(copies.Skip(1));
        SelectObject(anchor);CommitEdit();RenderMap();
    }
}
