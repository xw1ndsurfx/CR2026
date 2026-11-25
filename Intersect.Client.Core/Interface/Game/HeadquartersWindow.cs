using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network.Packets.GuildHeadquarters;
using Newtonsoft.Json;
using static Intersect.Client.Interface.Game.NavalManagementWindow;

namespace Intersect.Client.Interface.Game;

internal sealed class HeadquartersWindow : Window
{
    private readonly ScrollControl _body;
    private readonly Label _message;
    private readonly ImagePanel _art;
    private readonly Button _enter, _leave, _bank, _upgrade;
    private readonly TextBox _guest;
    private readonly Button _invite, _endVisits;
    private readonly Action<HeadquartersRequestPacket> _send;
    private HeadquartersView _view = new();
    private string _signature = "";
    private bool _confirm;
    internal static new string T(string text) => HeadquartersText.Get(text, ClientLanguages.Current);
    private static string Content(string text) => ClientLanguages.ContentText(text);
    internal static void Request(HeadquartersAction action) => Networking.Network.SendPacket(new HeadquartersRequestPacket { Action = action });
    internal HeadquartersWindow(Canvas parent, Action<HeadquartersRequestPacket>? send = null) : base(parent, T("Guild headquarters"), false, nameof(HeadquartersWindow))
    {
        _send = send ?? (packet => Networking.Network.SendPacket(packet));
        SetSize(Math.Min(840, parent.Width - 24), Math.Min(700, parent.Height - 36));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        var frame = new MarketplaceWindow.MarketChrome(this, "HeadquartersFrame", MarketplaceWindow.FrameStyle.Window) { DrawHeaderDivider = false };
        frame.SetBounds(12, 8, Width - 24, Height - 35);
        _art = new ImagePanel(this, "HallArtwork") { MaintainAspectRatio = false, MouseInputEnabled = false, ShouldDrawBackground = false };
        var artHeight = Height >= 600 ? 120 : 40;
        _art.SetBounds(22, 34, Width - 44, artHeight);
        _message = Text(this, "HeadquartersMessage", "", 26, 44 + artHeight, Width - 52, 44, 12);
        _message.WrappingBehavior = WrappingBehavior.Wrapped;
        _body = new ScrollControl(this, "HeadquartersDetails") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = false };
        _body.SetBounds(24, 94 + artHeight, Width - 48, Math.Max(80, Height - 296 - artHeight));
        Shop.ShopWindow.ConfigureNativeScrollbar(_body);
        var half = (Width - 58) / 2;
        _guest = new TextBox(this, "VisitorName") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 12 };
        _guest.SetBounds(24, Height - 190, half, 30);
        _guest.SetToolTipText(T("Visitor character name"));
        _invite = ActionButton("InviteVisitor", "Invite visitor", 34 + half, Height - 190, half, () => Send(HeadquartersAction.Invite));
        _endVisits = ActionButton("EndVisits", "End all visits", 24, Height - 152, half, () =>
            _ = new Shared.InputBox(T("End all visits"), T("Revoke invitations and return every visitor?"), Shared.InputType.YesNo,
                onSubmit: (_, _) => Send(HeadquartersAction.EndVisits)));
        _bank = ActionButton("OpenHeadquartersBank", "Open guild bank", 34 + half, Height - 152, half, () => { Send(HeadquartersAction.OpenBank); });
        _enter = ActionButton("EnterHeadquarters", "Enter headquarters", 24, Height - 114, half, () => Send(HeadquartersAction.Enter));
        _leave = ActionButton("LeaveHeadquarters", "Leave headquarters", 24, Height - 114, half, () => Send(HeadquartersAction.Leave));
        _upgrade = ActionButton("UpgradeHeadquarters", "Upgrade headquarters", 34 + half, Height - 114, half, () =>
        {
            if (_confirm) { Send(HeadquartersAction.Upgrade); _confirm = false; }
            else _confirm = true;
            RefreshActions();
        });
        ActionButton("CloseHeadquarters", "Close", Width - 174, Height - 70, 150, () => { _confirm = false; Hide(); });
        Closed += (_, _) => _confirm = false;
        Hide();
    }
    protected override void EnsureInitialized() { }
    private Button ActionButton(string id, string text, int x, int y, int w, Action action) => Button(this, id, T(text), x, y, w, 34, action);
    private void Send(HeadquartersAction action) => _send(new() { Action = action, TierId = _view.Next?.Id ?? Guid.Empty,
        Revision = _view.Revision, ConfigurationRevision = _view.ConfigurationRevision, Name = (_guest.Text ?? "").Trim() });
    internal void Apply(HeadquartersView view, bool open)
    {
        if (_view.Revision != view.Revision || _view.ConfigurationRevision != view.ConfigurationRevision || _view.GuildId != view.GuildId) _confirm = false;
        _view = view;
        _art.Texture = string.IsNullOrEmpty(view.Assets.Background) ? null : GameContentManager.Current.GetTexture(TextureType.Gui, view.Assets.Background);
        if (_art.Texture is {} texture)
        {
            var sourceHeight = Math.Min(texture.Height, texture.Width * _art.Height / Math.Max(1, _art.Width));
            _art.SetTextureRect(0, (texture.Height - sourceHeight) / 2, texture.Width, sourceHeight);
        }
        if (open || !string.IsNullOrEmpty(view.Message)) _message.Text = T(view.Message);
        if (string.IsNullOrEmpty(_message.Text)) _message.Text = T(view.Visitor ? "Guest access: no bank or upgrades. Your host can end the visit." :
            !view.Enabled ? "Headquarters are not available yet." : "Build your guild home together. Deposit upgrade resources in the shared bank.");
        var signature = ClientLanguages.Current + JsonConvert.SerializeObject(new { view.GuildId, view.GuildName, view.TierNumber, view.Current, view.Next, view.Visitor,
            view.MemberCount, view.BankSlots, view.AvailableQuantity, view.CostItemName, view.Visitors, view.Assets.BankIcon, view.VisitMinutes });
        if (_signature != signature) { _signature = signature; Rebuild(); }
        RefreshActions();
        if (open) { Show(); BringToFront(); }
    }
    private void RefreshActions()
    {
        _enter.IsHidden = _view.Inside; _leave.IsHidden = !_view.Inside;
        _enter.IsDisabled = !_view.Enabled || _view.Current == null || _view.GuildId == Guid.Empty;
        _bank.IsDisabled = !_view.NearBank;
        _upgrade.IsDisabled = !_view.Enabled || !_view.Inside || !_view.CanManage || _view.Next == null ||
            _view.MemberCount < _view.Next.MinimumMembers || _view.AvailableQuantity < _view.Next.CostQuantity;
        _upgrade.Text = T(_confirm ? "Confirm spending guild resources" : "Upgrade headquarters");
        _guest.IsDisabled = _invite.IsDisabled = _endVisits.IsDisabled = !_view.Inside || !_view.CanInvite;
    }
    private void Rebuild()
    {
        var scroll = _body.VerticalScrollBar.ScrollAmount; _body.DeleteAllChildren();
        var width = _body.Width - 32; var y = 4; var indent = 0;
        void Line(string id, string text, int size = 12)
        {
            var available = width - indent;
            var label = Text(_body, id, text, 6 + indent, y, available, 30, size);
            var lines = text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(Skin.Renderer.MeasureText(label.Font, size, line).X / (double)Math.Max(1, available - 16))));
            var height = lines * (size + 6) + 8; label.SetSize(available, height); label.WrappingBehavior = WrappingBehavior.Wrapped; y += height + 6;
        }
        void Card(string id, string file, Action content)
        {
            var start = y; var iconSize = Height < 600 ? 56 : 88;
            var image = new ImagePanel(_body, id) { MaintainAspectRatio = true, MouseInputEnabled = false, ShouldDrawBackground = false };
            image.SetBounds(6, y, iconSize, iconSize);
            if (file.Length > 0) image.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, file);
            indent = iconSize + 14; content(); indent = 0; y = Math.Max(y, start + iconSize) + 12;
        }
        Line("HeadquartersGuild", _view.GuildName, 18);
        if (_view.Current is {} current)
        {
            Card("CurrentTierIcon", current.Icon, () => {
                Line("CurrentTier", string.Format(T("Tier {0}: {1}"), _view.TierNumber, Content(current.Name)), 15);
                if (current.Description.Length > 0) Line("CurrentDescription", Content(current.Description));
                Line("GuildCapacity", string.Format(T("{0} members · {1} bank slots · {2} visitors"), _view.MemberCount, _view.BankSlots, _view.Visitors));
            });
            if (!_view.Visitor)
            {
                Card("GuildBankIcon", _view.Assets.BankIcon, () =>
                    Line("BankLocation", string.Format(T("Guild bank location: ({0}, {1}). Stand nearby to open it."), current.BankX, current.BankY)));
            }
        }
        if (_view.Visitor) Line("VisitorRules", T("Guest access: no bank or upgrades. Your host can end the visit."));
        else if (_view.Next is {} next)
        {
            Card("NextTierIcon", next.Icon, () => {
            Line("NextTier", T("Next upgrade") + " · " + Content(next.Name), 15);
            Line("NextDescription", Content(next.Description));
            Line("UpgradeCost", next.CostQuantity == 0 ? T("Free upgrade") : string.Format(T("Cost from guild bank: {0} × {1} · available: {2}"), next.CostQuantity, Content(_view.CostItemName), _view.AvailableQuantity));
            Line("UpgradeRequirements", string.Format(T("Required members: {0} · bank capacity after upgrade: at least {1}"), next.MinimumMembers, Math.Max(_view.BankSlots, next.BankSlots)));
            Line("UpgradeHelp", T("Upgrading moves everyone to the next hall. Guild bank permissions stay unchanged."));
            });
        }
        else if (_view.Current != null) Line("MaximumTier", T("All headquarters upgrades are unlocked."));
        Line("VisitHelp", string.Format(T("Invitations last 2 minutes. Visits last up to {0} minutes and end when the host disconnects."), _view.VisitMinutes));
        _body.SetInnerSize(width, y + 8); _body.UpdateScrollBars(); _body.VerticalScrollBar.ScrollAmount = scroll;
    }
}
