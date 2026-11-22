using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private NavalExpeditionWindow? _navalExpedition;
    private Button? _navalExpeditionHud;
    private NavalExpeditionView _expeditionView = new();
    internal void ApplyNavalExpedition(NavalExpeditionPacket packet)
    {
        var view = JsonConvert.DeserializeObject<NavalExpeditionView>(packet.Json);
        if (view == null) return;
        _expeditionView = view;
        _navalExpedition ??= new NavalExpeditionWindow(GameCanvas);
        _navalExpedition.Apply(view, packet.Open);
        _navalExpeditionHud ??= NavalManagementWindow.Button(GameCanvas, "NavalExpeditionHud", "Naval expeditions", 0, 0, 350, 52,
            () => NavalExpeditionWindow.Send(NavalExpeditionAction.Open));
        var stage = view.Definition?.Stages.ElementAtOrDefault(view.StageIndex);
        _navalExpeditionHud.Text = NavalExpeditionWindow.Content(view.Definition?.Name ?? "") + "\n" +
            (view.Status == NavalExpeditionStatus.Active && stage != null ? NavalManagementWindow.T(stage.Objective.ToString()) + $" · {view.Progress}/{view.Required}" :
                NavalManagementWindow.T(view.CanClaim ? "Claim my reward" : view.Status.ToString()));
        UpdateNavalExpeditionInterface();
    }
    private void UpdateNavalExpeditionInterface()
    {
        if (_navalExpeditionHud == null) return;
        _navalExpeditionHud.IsHidden = _expeditionView.RunId == Guid.Empty || SuppressStatusHuds || Cinematics.CinematicControls.Active ||
            GameCanvas.Children.OfType<Modal>().Any(modal => modal.IsVisibleInTree) || _navalExpedition?.IsVisibleInTree == true;
        _navalExpeditionHud.SetBounds((GameCanvas.Width - 350) / 2, 140, 350, 52);
        _navalExpeditionHud.SendToBack();
    }
}
