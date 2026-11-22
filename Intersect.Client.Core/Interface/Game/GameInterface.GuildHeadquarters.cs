using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network.Packets.GuildHeadquarters;
using Newtonsoft.Json;

namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private HeadquartersWindow? _headquarters;
    private Button? _headquartersHud;
    private Window? _headquartersInvitation;
    private long _headquartersInvitationUntil;
    private sealed class InvitationWindow(Canvas parent, string title) : Window(parent, title, false, "HeadquartersInvitation")
    { protected override void EnsureInitialized() { } }
    internal void ApplyHeadquarters(HeadquartersStatePacket packet)
    {
        var view = JsonConvert.DeserializeObject<HeadquartersView>(packet.Json);
        if (view == null || view.Assets is not { IsValid: true }) return;
        HeadquartersPresentation.Apply(view, packet.Open);
        _headquarters ??= new HeadquartersWindow(GameCanvas);
        _headquarters.Apply(view, packet.Open);
        _headquartersHud ??= NavalManagementWindow.Button(GameCanvas, "HeadquartersHud", HeadquartersWindow.T("Guild headquarters"), 8, 28, 240, 34,
            () => HeadquartersWindow.Request(HeadquartersAction.Open));
        UpdateHeadquarters();
    }
    private void UpdateHeadquarters()
    {
        HeadquartersPresentation.Update();
        if (_headquartersHud != null)
        {
            _headquartersHud.IsHidden = !HeadquartersPresentation.Inside || SuppressStatusHuds || Cinematics.CinematicControls.Active ||
                _headquarters?.IsVisibleInTree == true || GameCanvas.Children.OfType<Modal>().Any(modal => modal.IsVisibleInTree);
            _headquartersHud.SendToBack();
        }
        if (_headquartersInvitation != null && Maps.MapWeatherClock.UtcMilliseconds >= _headquartersInvitationUntil)
        { _headquartersInvitation.Dispose(); _headquartersInvitation = null; }
    }
    internal void InviteToHeadquarters(HeadquartersInvitationPacket packet)
    {
        if (packet.Id == Guid.Empty || packet.ExpiresUtc <= Maps.MapWeatherClock.UtcMilliseconds) return;
        _headquartersInvitation?.Dispose();
        var dialog = new InvitationWindow(GameCanvas, HeadquartersWindow.T("Guild invitation")) { DeleteOnClose = false };
        dialog.SetSize(Math.Min(560, GameCanvas.Width - 24), 350); dialog.Alignment = [Alignments.Center]; dialog.DisableResizing();
        _headquartersInvitation = dialog; _headquartersInvitationUntil = packet.ExpiresUtc;
        var icon = new ImagePanel(dialog, "InvitationArt") { MaintainAspectRatio = true, MouseInputEnabled = false, ShouldDrawBackground = false };
        icon.SetBounds((dialog.Width - 80) / 2, 34, 80, 80);
        if (HeadquartersAssets.File(packet.Icon, ".png") && packet.Icon.Length > 0) icon.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, packet.Icon);
        var text = NavalManagementWindow.Text(dialog, "InvitationText", string.Format(HeadquartersWindow.T("{0} invites you to {1}'s headquarters for up to {2} minutes. No bank access. Join?"),
            packet.HostName, packet.GuildName, packet.VisitMinutes), 24, 125, dialog.Width - 48, 120, 12);
        text.WrappingBehavior = WrappingBehavior.Wrapped;
        void Respond(HeadquartersAction action)
        { Networking.Network.SendPacket(new HeadquartersRequestPacket { Action = action, InvitationId = packet.Id }); dialog.Hide(); }
        var half = (dialog.Width - 60) / 2;
        NavalManagementWindow.Button(dialog, "AcceptVisit", HeadquartersWindow.T("Accept"), 24, 260, half, 34, () => Respond(HeadquartersAction.AcceptInvitation));
        NavalManagementWindow.Button(dialog, "DeclineVisit", HeadquartersWindow.T("Decline"), 36 + half, 260, half, 34, () => Respond(HeadquartersAction.DeclineInvitation));
        dialog.Closed += (_, _) => Respond(HeadquartersAction.DeclineInvitation);
        HeadquartersPresentation.Play(packet.Sound, packet.Volume);
        dialog.Show(); dialog.BringToFront();
    }
}
