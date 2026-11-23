using Intersect.Client.Core.Controls;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Input;
using Intersect.Client.Localization;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Network.Packets.EquipmentLoadouts;
using static Intersect.Client.Interface.Game.NavalManagementWindow;

namespace Intersect.Client.Interface.Game;

internal sealed class EquipmentLoadoutWindow : Window
{
    private readonly Action<EquipmentLoadoutRequestPacket> _send;
    private readonly Label _status, _help;
    private readonly TextBox _name;
    private readonly ScrollControl _items;
    private readonly Button[] _slots = new Button[EquipmentLoadoutRules.Count];
    private readonly Button _save, _equip, _delete, _refresh;
    private EquipmentLoadoutView _view = new();
    private int _selected;
    private Guid _pending;
    private long _pendingUntil;
    private bool _confirmSave, _confirmDelete, _uncertain;
    internal static string T(string source) => EquipmentLoadoutText.Get(source, ClientLanguages.Current);

    internal EquipmentLoadoutWindow(Canvas canvas, Action<EquipmentLoadoutRequestPacket>? send = null)
        : base(canvas, T("Equipment sets"), false, nameof(EquipmentLoadoutWindow))
    {
        _send = send ?? (p => Networking.Network.SendPacket(p));
        SetSize(Math.Min(720, canvas.Width - 24), Math.Min(620, canvas.Height - 36));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        EquipmentPresentation.Background(this, false, Width, Height - 25);
        var inset = Math.Max(40, Width / 10); var contentWidth = Width - inset * 2;
        EquipmentPresentation.Icon(this, 0, inset, 34, 32);
        _status = Text(this, "EquipmentSetStatus", "", inset + 40, 54, contentWidth - 40, 48, 12); _status.WrappingBehavior = WrappingBehavior.Wrapped;
        var sw = (contentWidth + 4) / EquipmentLoadoutRules.Count;
        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = index;
            _slots[index] = Button(this, "EquipmentSet" + index, (index + 1).ToString(), inset + index * sw, 106, sw - 4, 32,
                () => { EquipmentPresentation.Play("select"); _selected = slot; _confirmSave = _confirmDelete = false; RenderState(true); });
        }
        _name = new TextBox(this, "EquipmentSetName")
        {
            MaximumLength = EquipmentLoadoutRules.MaxNameLength,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 12,
        };
        _name.SetBounds(inset, 146, contentWidth, 30);
        _help = Text(this, "EquipmentSetHelp", "", inset, 183, contentWidth, 48, 12); _help.WrappingBehavior = WrappingBehavior.Wrapped;
        _items = new ScrollControl(this, "EquipmentSetItems") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = false };
        _items.SetBounds(inset, 235, contentWidth, Math.Max(48, Height - 373)); Shop.ShopWindow.ConfigureNativeScrollbar(_items);
        var bw = (contentWidth - 16) / 3;
        _save = Button(this, "SaveEquipmentSet", T("Save"), inset, Height - 130, bw, 32, () =>
        {
            if (_view.Sets.ContainsKey(_selected) && !_confirmSave) { _confirmSave = true; _confirmDelete = false; RenderState(); return; }
            Send(EquipmentLoadoutAction.Save);
        });
        _equip = Button(this, "ApplyEquipmentSet", T("Equip set"), inset + 8 + bw, Height - 130, bw, 32, () => Send(EquipmentLoadoutAction.Apply));
        _delete = Button(this, "DeleteEquipmentSet", T("Delete set"), inset + 16 + bw * 2, Height - 130, bw, 32, () =>
        {
            if (!_confirmDelete) { _confirmDelete = true; _confirmSave = false; RenderState(); return; }
            Send(EquipmentLoadoutAction.Delete);
        });
        _refresh = Button(this, "RefreshEquipmentSets", T("Refresh"), inset, Height - 90, bw, 30, () => Send(EquipmentLoadoutAction.Refresh));
        Button(this, "CloseEquipmentSets", T("Close"), Width - inset - bw, Height - 90, bw, 30, Hide);
        _save.TooltipText = T("Save current equipment");
        Interface.InputBlockingComponents.Add(this);
        Disposed += (_, _) => Interface.InputBlockingComponents.Remove(this);
        Hide();
    }

    protected override void EnsureInitialized() { }
    internal void Apply(EquipmentLoadoutView view, Guid requestId, bool open)
    {
        if (_pending != Guid.Empty && requestId != _pending && !open) return;
        if (_view.CharacterId != view.CharacterId) _selected = 0;
        _pending = Guid.Empty; _pendingUntil = 0; _uncertain = false;
        _view = view; _confirmSave = _confirmDelete = false;
        RenderState(true); if (open) { Show(); BringToFront(); }
    }

    internal void UpdateState()
    {
        if (_pending != Guid.Empty && Environment.TickCount64 >= _pendingUntil)
        {
            _pending = Guid.Empty; _uncertain = true;
            _view.Message = "No response. Refresh before trying again."; RenderState();
        }
    }

    private void Send(EquipmentLoadoutAction action)
    {
        if (_pending != Guid.Empty || _uncertain && action != EquipmentLoadoutAction.Refresh) return;
        _pending = Guid.NewGuid(); _pendingUntil = Environment.TickCount64 + 15000;
        _send(new() { Action = action, Slot = _selected, Name = _name.Text.Trim(), Revision = _view.Revision, RequestId = _pending });
        _confirmSave = _confirmDelete = false; RenderState();
    }

    internal static string KeyCaption(int slot)
    {
        try
        {
            if (Controls.ActiveControls.TryGetMappingFor(Control.EquipmentSet1 + slot, out var mapping))
            {
                var binding = mapping.Bindings.FirstOrDefault(b => b.Key != Keys.None);
                if (binding != null && binding.Key != Keys.None) return Hotbar.HotBarWindow.NavalKeyCaption(binding);
            }
        }
        catch (InvalidOperationException) { }
        return T("Unassigned");
    }

    private void RenderState(bool updateName = false)
    {
        var set = _view.Sets.GetValueOrDefault(_selected); var pending = _pending != Guid.Empty;
        if (updateName) _name.Text = set?.Name ?? string.Format(T("Set {0}"), _selected + 1);
        _status.Text = pending ? T("Waiting for the server...") : _confirmSave ? T("Click again to replace this saved set with your current equipment.") :
            _confirmDelete ? T("Click again to delete this saved set. Your items are kept.") : _view.Message.Length > 0 ? T(_view.Message) : T("Save your equipped items, then switch sets outside combat.");
        _help.Text = string.Format(T("Shortcut: {0}. Change it in Settings > Controls."), KeyCaption(_selected));
        var blocked = _view.BlockReason.Length > 0 ? _view.BlockReason : _view.Problems.GetValueOrDefault(_selected) ?? "";
        if (blocked.Length > 0) _help.Text = T(blocked);
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i].IsDisabled = pending;
            _slots[i].Text = (i == _selected ? "[" + (i + 1) + "]" : (i + 1).ToString()) + (_view.Sets.ContainsKey(i) ? " *" : "");
            _slots[i].TooltipText = (_view.Sets.GetValueOrDefault(i)?.Name ?? T("Empty set")) + " · " + KeyCaption(i);
        }
        _save.Text = T(_confirmSave ? "Confirm" : "Save");
        _delete.Text = T(_confirmDelete ? "Confirm" : "Delete set");
        _save.IsDisabled = pending || _uncertain || _view.BlockReason.Length > 0;
        _equip.IsDisabled = pending || _uncertain || set == null || blocked.Length > 0;
        _delete.IsDisabled = pending || _uncertain || set == null || _view.BlockReason.Length > 0;
        _refresh.IsDisabled = pending; _name.IsDisabled = pending;
        _items.DeleteAllChildren();
        if (set == null) Text(_items, "EmptyEquipmentSet", T("Equip the items you want, then save this set."), 4, 4, _items.Width - 28, 48, 12).WrappingBehavior = WrappingBehavior.Wrapped;
        else for (var i = 0; i < (set.Items?.Length ?? 0); i++)
        {
            var item = set.Items[i]; var rowY = i * 58; var name = item == null ? T("Empty slot") : ClientLanguages.ContentText(_view.ItemNames.GetValueOrDefault(item.ItemId) ?? T("Missing item"));
            var descriptor = item == null ? null : Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(item.ItemId);
            if (descriptor != null)
            {
                var icon = new ImagePanel(_items, "EquipmentSetIcon" + i) { MaintainAspectRatio = true, MouseInputEnabled = false }; icon.SetBounds(4, rowY + 6, 36, 36);
                icon.Texture = ItemIconTexture.Get(descriptor); icon.RenderColor = descriptor.Color;
            }
            var label = Text(_items, "EquipmentSetItem" + i, ClientLanguages.ContentText(set.SlotNames?.ElementAtOrDefault(i) ?? "") + "\n" + name, 48, rowY, _items.Width - 80, 54, 12);
            label.WrappingBehavior = WrappingBehavior.Wrapped; label.TooltipText = name + (descriptor == null ? "" : "\n" + string.Join("\n", EquipmentPresentation.Details(descriptor, new Intersect.Framework.Core.GameObjects.Items.ItemProperties { Enhancement = item!.Enhancement }).Select(p => p.Key + ": " + p.Value)));
        }
        _items.UpdateScrollBars();
    }
}
