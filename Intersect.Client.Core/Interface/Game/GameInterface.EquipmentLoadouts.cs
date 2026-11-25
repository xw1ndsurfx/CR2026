using Intersect.Client.Core.Controls;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Network.Packets.EquipmentLoadouts;
using Newtonsoft.Json;

namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private EquipmentLoadoutWindow? _equipmentLoadouts;
    internal static void UseEquipmentSpell() => Networking.Network.SendPacket(new UseEquipmentSpellPacket { TargetId = Globals.Me?.TargetId ?? Guid.Empty });
    internal bool TryHandleEquipmentSpellHotkey()
    {
        if (!CanShowWeaponSkills || !Controls.IsControlPressed(Control.WeaponSpell, out _, out _)) return false;
        if (Controls.IsControlJustPressed(Control.WeaponSpell)) UseEquipmentSpell();
        return true;
    }
    internal void HideEquipmentLoadouts() => _equipmentLoadouts?.Hide();
    internal void OpenEquipmentLoadouts() => Networking.Network.SendPacket(new EquipmentLoadoutRequestPacket { Action = EquipmentLoadoutAction.Open, RequestId = Guid.NewGuid() });
    internal void ApplyEquipmentLoadouts(EquipmentLoadoutStatePacket packet)
    {
        var view = JsonConvert.DeserializeObject<EquipmentLoadoutView>(packet.Json);
        if (view == null || view.CharacterId != Globals.Me?.Id) return;
        var cue = view.Message switch { "Equipment set equipped." => "equip", "Equipment set saved." => "save", "Equipment set deleted." => "delete", "" => packet.Open ? "open" : "", "This equipment set is already equipped." => "select", _ => "error" };
        if (cue.Length > 0) EquipmentPresentation.Play(cue);
        if (packet.Open) _equipmentLoadouts ??= new EquipmentLoadoutWindow(GameCanvas);
        _equipmentLoadouts?.Apply(view, packet.RequestId, packet.Open);
    }

    // Runs after the player's text-input guard. Consume held bindings to avoid also using a hotbar item.
    internal bool TryHandleEquipmentLoadoutHotkey()
    {
        var hit = Enumerable.Range(0, EquipmentLoadoutRules.Count)
            .Select(slot => Controls.IsControlPressed(Control.EquipmentSet1 + slot, out _, out var binding)
                ? (Slot: slot, Binding: binding) : (Slot: -1, Binding: (ControlBinding?)null))
            .Where(x => x.Slot >= 0).OrderByDescending(x => x.Binding!.Modifier).FirstOrDefault((Slot: -1, Binding: (ControlBinding?)null));
        if (hit.Slot < 0) return false;
        // A more specific existing skill/hotbar binding (e.g. Alt+1 versus 1) wins.
        if (Controls.ActiveControls.Mappings.Any(pair =>
            (pair.Key >= Control.WeaponSkill1 && pair.Key <= Control.WeaponSkill10 || pair.Key > Control.HotkeyOffset) &&
            Controls.IsControlPressed(pair.Key, out _, out var binding) && binding.Key == hit.Binding!.Key && binding.Modifier > hit.Binding.Modifier)) return false;
        if (Controls.IsControlJustPressed(Control.EquipmentSet1 + hit.Slot))
            Networking.Network.SendPacket(new EquipmentLoadoutRequestPacket { Action = EquipmentLoadoutAction.Apply, Slot = hit.Slot, RequestId = Guid.NewGuid() });
        return true;
    }
}
