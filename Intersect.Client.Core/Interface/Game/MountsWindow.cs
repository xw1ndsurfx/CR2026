using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game;
internal sealed class MountsWindow : Window
{
    private readonly ScrollControl _scroll;
    private readonly Label _status;
    private MountStatePacket? _state;
    private bool _ownedOnly;
    private string _search = "";
    private static string T(string text) => ClientLanguages.Text(text);
    internal MountsWindow(Canvas canvas) : base(canvas, T("Mounts"), false, nameof(MountsWindow))
    {
        SetSize(720, 580); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        var title = new Label(this) { Text = T("Stable"), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 17, TextColorOverride = Color.White }; title.SetBounds(24, 42, 420, 28);
        var search = new TextBox(this, "MountSearch"); search.SetBounds(24, 82, 445, 30);
        search.TextChanged += (_, _) => { _search = search.Text ?? ""; Rebuild(); };
        var ownedFilter = NavalManagementWindow.Button(this, "MountOwnedFilter", T("Unlocked only"), 484, 82, 208, 30, () => { _ownedOnly = !_ownedOnly; Rebuild(); });
        ownedFilter.IsToggle = true; ownedFilter.TextColorOverride = Color.White;
        _scroll = new ScrollControl(this, "MountCards") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _scroll.SetBounds(24, 125, 668, 355);
        _status = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 10,
            AutoSizeToContents = false, TextColorOverride = Color.White }; _status.SetBounds(24, 487, 668, 25);
        NavalManagementWindow.Button(this, "MountFavoriteRide", T("Ride favorite / Dismount"), 24, 522, 668, 32,
            () => Send(MountAction.Toggle, Guid.Empty)).TextColorOverride = Color.White;
        Hide();
    }
    protected override void EnsureInitialized() { }
    internal static void Open() => Send(MountAction.List, Guid.Empty);
    private static void Send(MountAction action, Guid id) => Intersect.Client.Networking.Network.SendPacket(new MountRequestPacket { Action = action, MountId = id });
    internal void Apply(MountStatePacket state) { _state = state; Rebuild(); }
    private void Rebuild()
    {
        if (_state == null) return;
        MountConfiguration config;
        try { config = MountConfiguration.FromJson(_state.ConfigurationJson); } catch { return; }
        _scroll.DeleteAll(); var y = 6;
        foreach (var mount in config.Mounts.Where(m => (!_ownedOnly || _state.Owned.Contains(m.Id)) &&
            System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(T(ClientLanguages.ContentText(m.Name)), _search,
                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0))
        {
            var owned = config.Enabled && _state.Owned.Contains(mount.Id);
            var card = new Base(_scroll); card.SetBounds(2, y, 638, 135);
            var portrait = new MountPortrait(card, mount); portrait.SetBounds(4, 0, 128, 128);
            var name = new Label(card) { Text = T(ClientLanguages.ContentText(mount.Name)), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 12, AutoSizeToContents = false, TextColorOverride = Color.White }; name.SetBounds(145, 8, 470, 24);
            var info = new Label(card) { Text = T(owned ? "Unlocked" : "Locked") + " · " + T("Speed") + " +" + ((mount.SpeedMultiplier - 1) * 100).ToString("0") + "%",
                Font = name.Font, FontSize = 10, AutoSizeToContents = false, TextColorOverride = Color.White }; info.SetBounds(145, 36, 470, 22);
            var description = new Label(card) { Text = ClientLanguages.ContentText(mount.Description), Font = name.Font, FontSize = 9,
                AutoSizeToContents = false, TextColorOverride = Color.White }; description.SetBounds(145, 60, 470, 22);
            var ride = NavalManagementWindow.Button(card, "MountRide" + mount.Id, T(_state.Active == mount.Id ? "Dismount" : "Ride"), 145, 93, 220, 30,
                () => { if (owned) Send(MountAction.Toggle, mount.Id); });
            ride.IsDisabled = !owned; ride.TextColorOverride = Color.White;
            var favorite = NavalManagementWindow.Button(card, "MountFavorite" + mount.Id, T(_state.Favorite == mount.Id ? "Favorite mount" : "Set favorite"), 380, 93, 235, 30,
                () => { if (owned) Send(MountAction.Favorite, mount.Id); });
            favorite.IsDisabled = !owned; favorite.TextColorOverride = Color.White;
            y += 143;
        }
        _status.Text = !config.Enabled ? T("Mounts are disabled.") : !string.IsNullOrEmpty(_state.Message) ? T(_state.Message)
            : y == 6 ? T("No mounts match your search.") : T("Mounts are unavailable during combat, in instances and aboard ships.");
    }
    private sealed class MountPortrait(Base parent, MountDefinition definition) : Base(parent)
    {
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var texture = Globals.ContentManager?.GetTexture(TextureType.Image, definition.Idle);
            if (texture == null || texture.Width < definition.FrameWidth * definition.IdleFrames || texture.Height < definition.FrameHeight * 4) return;
            var frame = (int)(Timing.Global.Milliseconds / definition.FrameMilliseconds % definition.IdleFrames);
            skin.Renderer.DrawTexturedRect(texture, new Rect(0, 0, Width, Height), Color.White,
                frame * definition.FrameWidth / (float)texture.Width, definition.FrameHeight / (float)texture.Height,
                (frame + 1) * definition.FrameWidth / (float)texture.Width, 2 * definition.FrameHeight / (float)texture.Height);
        }
    }
}
