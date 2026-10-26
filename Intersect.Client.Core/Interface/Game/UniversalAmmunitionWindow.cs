using System.Text.Json;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;
internal sealed class UniversalAmmunitionWindow : Window
{
    private readonly ScrollControl _list;
    private readonly Label _status;
    private readonly List<Base> _rows = [];
    private long _lastRefresh;
    private string _lastJson = "";
    private static string T(string text) => ClientLanguages.Text(text);
    internal UniversalAmmunitionWindow(Canvas canvas) : base(canvas, T("Ammunition"), false, nameof(UniversalAmmunitionWindow))
    {
        SetSize(560, 500); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _status = NavalManagementWindow.Text(this, "AmmunitionStatus", "", 16, 38, 528, 56, 11);
        _list = new(this) { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _list.SetBounds(16, 102, 528, 300);
        NavalManagementWindow.Button(this, "AmmunitionRefresh", T("Refresh"), 16, 414, 188, 30, () => Request(false));
        NavalManagementWindow.Button(this, "CannonAmmunition", T("Cannon ammunition"), 216, 414, 328, 30, () => {
            Hide(); NavalManagementWindow.Request(Intersect.Framework.Core.Naval.NavalManageAction.Ammo);
        });
        Hide();
    }
    protected override void EnsureInitialized() { }
    internal static void Request(bool open = true) => Networking.Network.SendPacket(new ArcheryRequestPacket {
        Action = ArcheryAction.Ammunition, RequestId = open ? Guid.NewGuid() : Guid.Empty });
    internal void Apply(ArcheryStatePacket packet)
    {
        var view = JsonSerializer.Deserialize<AmmunitionView>(packet.AmmunitionJson);
        if (view == null) return;
        _status.Text = view.WeaponId == Guid.Empty ? T("Equip a ranged weapon to select compatible ammunition.") :
            ClientLanguages.Format("Weapon: {0}", ClientLanguages.ContentText(ItemDescriptor.GetName(view.WeaponId)));
        if (packet.Message.Length > 0) _status.Text += "\n" + T(packet.Message);
        if (_lastJson != packet.AmmunitionJson)
        {
            _lastJson = packet.AmmunitionJson;
            foreach (var row in _rows) { row.Parent = null; row.Dispose(); } _rows.Clear();
            foreach (var entry in view.Entries.OrderByDescending(e => e.Compatible).ThenBy(e => ItemDescriptor.GetName(e.ItemId)))
            {
                var item = ItemDescriptor.Get(entry.ItemId); if (item == null) continue;
                var row = new Button(_list, "AmmunitionItem" + entry.ItemId) { Text = "", IsDisabled = !entry.Compatible || entry.Quantity < entry.Required };
                row.SetBounds(0, _rows.Count * 64, 502, 60); _rows.Add(row);
                var icon = new ImagePanel(row) { MaintainAspectRatio = true, ShouldDrawBackground = false, MouseInputEnabled = false };
                icon.SetBounds(8, 10, 36, 36);
                icon.Texture = GameContentManager.Current.GetTexture(item.UsesSpellIcon ? TextureType.Spell : TextureType.Item, item.DisplayIcon);
                var selected = entry.ItemId == view.SelectedItemId;
                NavalManagementWindow.Text(row, "AmmoItemName", ClientLanguages.ContentText(item.Name), 54, 3, 430, 24, 11);
                NavalManagementWindow.Text(row, "AmmoItemCount", ClientLanguages.Format("Quantity {0} · Per shot {1} · {2}", entry.Quantity,
                    entry.Required, T(selected ? "In use" : entry.Compatible ? "Select ammunition" : "Incompatible")), 54, 28, 430, 24, 10);
                row.TooltipText = ClientLanguages.ContentText(item.Description);
                row.Clicked += (_, _) => Networking.Network.SendPacket(new ArcheryRequestPacket {
                    Action = ArcheryAction.SelectAmmunition, WeaponId = view.WeaponId, ItemId = entry.ItemId });
            }
        }
        if (packet.Open) { Show(); BringToFront(); }
    }
    public override void Think()
    {
        base.Think();
        if (!IsVisibleInTree || Environment.TickCount64 - _lastRefresh < 1500) return;
        _lastRefresh = Environment.TickCount64; Request(false);
    }
}
