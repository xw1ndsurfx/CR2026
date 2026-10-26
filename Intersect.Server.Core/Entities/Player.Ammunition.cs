using System.Text.Json;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;

namespace Intersect.Server.Entities;
public partial class Player
{
    private Dictionary<Guid, Guid> _selectedWeaponAmmo;
    private long _ammoRequestAt;
    private Dictionary<Guid, Guid> SelectedWeaponAmmo => _selectedWeaponAmmo ??= RoyalCombatRuntime.LoadAmmo(Id);
    internal bool CanFireWorldWeapon => !IsArcheryActive && InShop == default && !InBank &&
        OpenCraftingTableId == default && Trading.Counterparty == default;
    internal ProjectileDescriptor[] CompatibleAmmoProjectiles(ItemDescriptor weapon)
    {
        if (weapon?.Projectile == null) return [];
        var rule = RoyalCombatRuntime.Current.Ammunition.FirstOrDefault(r => r.WeaponId == weapon.Id);
        return (rule?.ProjectileIds ?? []).Append(weapon.ProjectileId).Distinct().Select(ProjectileDescriptor.Get)
            .Where(p => p != null && p.AmmoItemId != Guid.Empty && p.AmmoRequired > 0).ToArray();
    }
    internal ProjectileDescriptor SelectedWeaponProjectile(ItemDescriptor weapon)
    {
        if (weapon == null) return null;
        // Legacy weapons require no database access unless an alternate ammo type was configured.
        if (!RoyalCombatRuntime.Current.Ammunition.Any(r => r.WeaponId == weapon.Id)) return weapon.Projectile;
        return CompatibleAmmoProjectiles(weapon).FirstOrDefault(p => p.AmmoItemId == SelectedWeaponAmmo.GetValueOrDefault(weapon.Id)) ?? weapon.Projectile;
    }
    internal bool TryConsumeProjectileAmmunition(ProjectileDescriptor projectile)
    {
        lock (EntityLock)
        {
            if (projectile == null) return false;
            if (projectile.AmmoItemId == Guid.Empty) return true;
            if (projectile.AmmoRequired <= 0 || ItemDescriptor.Get(projectile.AmmoItemId) == null) return false;
            return TryTakeItem(projectile.AmmoItemId, projectile.AmmoRequired);
        }
    }
    internal bool SelectWeaponAmmo(Guid weaponId, Guid itemId)
    {
        lock (EntityLock)
        {
            if (!TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var equipped) || equipped.ItemId != weaponId ||
                !CompatibleAmmoProjectiles(equipped.Descriptor).Any(p => p.AmmoItemId == itemId) || FindInventoryItemQuantity(itemId) <= 0) return false;
            RoyalCombatRuntime.SelectAmmo(Id, weaponId, itemId); // Persist before changing the live selection.
            SelectedWeaponAmmo[weaponId] = itemId; return true;
        }
    }
    private void HandleAmmunitionRequest(ArcheryRequestPacket packet)
    {
        lock (EntityLock)
        {
            var now = Environment.TickCount64;
            if (now - _ammoRequestAt < 250) return;
            _ammoRequestAt = now;
            var message = "";
            if (packet.Action == ArcheryAction.SelectAmmunition && !SelectWeaponAmmo(packet.WeaponId, packet.ItemId))
                message = "This ammunition is unavailable or incompatible with your equipped weapon.";
            var weapon = TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var equipped) ? equipped.Descriptor : null;
            var compatible = CompatibleAmmoProjectiles(weapon);
            var selected = SelectedWeaponProjectile(weapon)?.AmmoItemId ?? Guid.Empty;
            var knownAmmo = ProjectileDescriptor.Lookup.Values.OfType<ProjectileDescriptor>().Where(p => p.AmmoItemId != Guid.Empty).Select(p => p.AmmoItemId).ToHashSet();
            var owned = Items.Where(i => i.ItemId != Guid.Empty && i.Quantity > 0 &&
                    (i.Descriptor?.ItemType == ItemType.Ammo || knownAmmo.Contains(i.ItemId))).Select(i => i.ItemId);
            var entries = owned.Concat(compatible.Select(p => p.AmmoItemId)).Distinct().Where(id => ItemDescriptor.Get(id) != null)
                .Select(id => new AmmunitionEntry { ItemId = id, Quantity = FindInventoryItemQuantity(id),
                    Compatible = compatible.Any(p => p.AmmoItemId == id), Required = compatible.FirstOrDefault(p => p.AmmoItemId == id)?.AmmoRequired ?? 0 }).ToArray();
            SendPacket(new ArcheryStatePacket { Open = packet.Action == ArcheryAction.Ammunition && packet.RequestId != Guid.Empty, Message = message, AmmunitionJson = JsonSerializer.Serialize(new AmmunitionView {
                WeaponId = weapon?.Id ?? Guid.Empty, SelectedItemId = selected, Entries = entries }) });
        }
    }
}
