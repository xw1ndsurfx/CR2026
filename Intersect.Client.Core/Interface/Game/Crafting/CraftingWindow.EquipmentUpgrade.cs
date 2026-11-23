using Intersect.Client.General;
using Intersect.Client.Networking;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Crafting;

namespace Intersect.Client.Interface.Game.Crafting;
public partial class CraftingWindow
{
    private EquipmentUpgradeWindow? _equipmentUpgrade;
    private void OpenEquipmentUpgrade(CraftingRecipeDescriptor recipe)
    {
        if (_equipmentUpgrade is { IsDisposed: false }) Interface.GameUi.GameCanvas.RemoveChild(_equipmentUpgrade, true);
        var tableId = Globals.ActiveCraftingTable.Id;
        _equipmentUpgrade = new EquipmentUpgradeWindow(Interface.GameUi.GameCanvas, recipe, (slot, expected) =>
        {
            if (IsHidden || IsCrafting || Globals.ActiveCraftingTable?.Id != tableId || _craftRecipeDescriptorId != recipe.Id) return;
            mRemainingCrafts = 1; mBarTimer = Timing.Global.Milliseconds; IsClosable = false; mCraftAll.IsDisabled = true;
            PacketSender.SendCraftItem(recipe.Id, 1, slot, expected); EquipmentPresentation.Play("forge");
        });
        _equipmentUpgrade.Show(); _equipmentUpgrade.BringToFront(); EquipmentPresentation.Play("open");
    }
}
