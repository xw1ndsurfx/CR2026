using System.Text.Json;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal sealed class JoustDuelWindow : Window
{
    private readonly ComboBox _players;
    private readonly Label _arena, _positions, _invitation, _message;
    private readonly Button _challenge, _accept, _decline;
    private Guid _invitationId;
    private long _expires;
    private long _countdown;
    internal JoustDuelWindow(Canvas canvas) : base(canvas, ClientLanguages.Text("Challenge a knight"), false, nameof(JoustDuelWindow))
    {
        SetSize(640, 420); Alignment = [Alignments.Center]; DisableResizing(); DeleteOnClose = false;
        Label Text(int x, int y, int width, int height)
        {
            var label = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11, TextColorOverride = Color.White, AutoSizeToContents = false };
            label.SetBounds(x, y, width, height); return label;
        }
        _arena = Text(20, 42, 600, 26); _positions = Text(20, 72, 600, 48);
        _players = new ComboBox(this, "JoustOpponentChoice") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        _players.SetBounds(20, 132, 420, 30);
        NavalManagementWindow.Button(this, "JoustRefreshPlayers", ClientLanguages.Text("Refresh players"), 456, 132, 164, 30, () => Request(JoustAction.BrowseDuels));
        _challenge = NavalManagementWindow.Button(this, "JoustChallenge", ClientLanguages.Text("Challenge this player"), 20, 176, 600, 32,
            () => Request(JoustAction.InviteDuel, _players.SelectedItem?.UserData as Guid? ?? Guid.Empty));
        _invitation = Text(20, 222, 600, 48);
        _accept = NavalManagementWindow.Button(this, "JoustAccept", ClientLanguages.Text("Accept joust"), 20, 278, 292, 32, () => Request(JoustAction.AcceptDuel));
        _decline = NavalManagementWindow.Button(this, "JoustDecline", ClientLanguages.Text("Decline joust"), 328, 278, 292, 32, () => Request(JoustAction.DeclineDuel));
        _message = Text(20, 324, 600, 66); _accept.Hide(); _decline.Hide(); Hide();
    }
    protected override void EnsureInitialized() { }
    private void Request(JoustAction action, Guid target = default) => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = action, TargetId = target, InvitationId = _invitationId });
    internal void Apply(JoustStatePacket packet)
    {
        if (packet.Message.Length > 0 && !IsHidden) _message.Text = ClientLanguages.Text(packet.Message);
        if (packet.DuelJson.Length == 0) return;
        JoustDuelView? view;
        try { view = JsonSerializer.Deserialize<JoustDuelView>(packet.DuelJson); } catch (JsonException) { return; }
        if (view == null) return;
        if (view.Started) { _invitationId = Guid.Empty; Hide(); return; }
        _arena.Text = view.ArenaName;
        _positions.Text = view.ArenaId == Guid.Empty ? ClientLanguages.Text("Player duels are not enabled on this map.") :
            ClientLanguages.Format("Gold start: ({0}, {1}) · Blue start: ({2}, {3})", view.StartX, view.StartY, view.OpponentX, view.OpponentY);
        var selected = _players.SelectedItem?.UserData as Guid?;
        _players.ClearItems();
        foreach (var player in view.Candidates) _players.AddItem(player.Name, userData: player.Id);
        if (selected.HasValue) _players.SelectByUserData(selected.Value);
        if (_players.SelectedItem == null && view.Candidates.Length > 0) _players.SelectByUserData(view.Candidates[0].Id);
        _challenge.IsDisabled = view.ArenaId == Guid.Empty || view.Candidates.Length == 0;
        _invitationId = view.InvitationId; _expires = Environment.TickCount64 + view.ExpiresInMs;
        _invitation.Text = _invitationId == Guid.Empty ? ClientLanguages.Text("Challenge from the gold start. Accept from the blue start.") : ClientLanguages.Format("{0} challenges you to a joust.", view.Challenger);
        _accept.IsHidden = _decline.IsHidden = _invitationId == Guid.Empty;
        _accept.IsDisabled = view.ExpiresInMs <= 0;
        _message.Text = view.Candidates.Length == 0 ? ClientLanguages.Text("No other knight is present on this map.") : "";
        if (packet.OpenDuelWindow) { Show(); BringToFront(); }
    }
    public override void Think()
    {
        base.Think();
        if (_invitationId == Guid.Empty || Environment.TickCount64 < _countdown) return;
        _countdown = Environment.TickCount64 + 250;
        var seconds = (int)Math.Max(0, (_expires - Environment.TickCount64 + 999) / 1000);
        _accept.IsDisabled = seconds == 0;
        _accept.Text = ClientLanguages.Format("Accept joust ({0}s)", seconds);
    }
}
