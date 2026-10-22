using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Maps;
using Intersect.Client.Networking;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private sealed class FireView
    {
        internal required CampfireState State;
        internal Animation? Animation;
        internal Button? Button;
        internal Guid DisplayedAnimation;
        internal void Dispose()
        {
            Animation?.Dispose();var button=Button;Button=null;
            if(button!=null){button.Parent?.RemoveChild(button,false);button.Dispose();}
        }
    }
    private readonly Dictionary<Guid, FireView> _campfires = [];

    public void ApplyCampfires(CampfiresPacket packet)
    {
        var keep = packet.Fires.Select(fire => fire.Id).ToHashSet();
        foreach (var id in _campfires.Keys.Where(id => !keep.Contains(id)).ToArray())
        { _campfires[id].Dispose(); _campfires.Remove(id); }
        foreach (var state in packet.Fires.Take(512))
        {
            if (!_campfires.TryGetValue(state.Id, out var view))
                _campfires[state.Id] = view = new FireView { State = state };
            view.State = state;
        }
    }

    private void UpdateCampfires()
    {
        foreach (var view in _campfires.Values)
        {
            var state = view.State;
            if (Globals.Me?.MapId != state.MapId || MapInstance.Get(state.MapId) is not {} map)
            { view.Animation?.Dispose(); view.Animation = null; view.Button?.Hide(); continue; }
            if (view.Animation == null || view.Animation.IsDisposed || view.DisplayedAnimation != state.AnimationId)
            {
                view.Animation?.Dispose();
                view.Animation = map.AddTileAnimation(state.AnimationId, state.X, state.Y) as Animation;
                view.DisplayedAnimation = state.AnimationId;
                if (view.Animation != null) view.Animation.InfiniteLoop = true;
            }
            if (view.Button == null)
            {
                var id = state.Id;
                view.Button = NavalManagementWindow.Button(GameCanvas, "Campfire_" + id, "Campfire", 0, 0, 100, 24,
                    () => Intersect.Client.Networking.Network.SendPacket(new CampfireRequestPacket { FireId = id }));
                view.Button.Disposed+=(_,_)=>view.Button=null;
                view.Button.SendToBack();
            }
            var nearby = Math.Abs(Globals.Me.X - state.X) <= 2 && Math.Abs(Globals.Me.Y - state.Y) <= 2;
            view.Button.IsHidden = !state.Burning || !nearby || SuppressStatusHuds ||
                Interface.InputBlockingComponents.Any(control => control.IsVisibleInTree && control.IsBlockingInput);
            view.Button.Text = QualityOfLifeText.Get("Cook", ClientLanguages.Current);
            var zoom = Globals.Database.WorldZoom;
            view.Button.SetPosition(
                (int)((map.X + (state.X + .5f) * Options.Instance.Map.TileWidth - Graphics.CurrentView.Left) * zoom) - 50,
                (int)((map.Y + (state.Y + 1) * Options.Instance.Map.TileHeight - Graphics.CurrentView.Top) * zoom) + 4);
        }
    }
}
