using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using static Intersect.Client.Interface.Game.NavalManagementWindow;

namespace Intersect.Client.Interface.Game;

internal sealed class EquipmentUpgradeWindow : Window
{
    private readonly Label _selection;
    private readonly Button _confirm;
    private int _selected = -1;
    private ItemProperties? _expected;
    private static string T(string s) => EquipmentPresentation.T(s);
    internal EquipmentUpgradeWindow(Canvas canvas, CraftingRecipeDescriptor recipe, Action<int, ItemProperties> apply)
        : base(canvas, T("Equipment forge"), false, nameof(EquipmentUpgradeWindow))
    {
        SetSize(Math.Min(760, canvas.Width - 24), Math.Min(670, canvas.Height - 36));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        EquipmentPresentation.Background(this, true, Width, Height - 25);
        var inset = Math.Max(38, Width / 10); var width = Width - inset * 2;
        EquipmentPresentation.Icon(this, 1, inset, 32, 40);
        Text(this, "UpgradeRecipe", ClientLanguages.ContentText(recipe.Name), inset + 48, 34, width - 48, 32, 14);
        var effects = recipe.EquipmentUpgrade!;
        if (effects.EventId != Guid.Empty) EquipmentPresentation.Icon(this, 3, Width - inset - 34, 32, 34);
        var preview = new ItemProperties { Enhancement = new EquipmentEnhancement { Rarity = effects.TargetRarity, Spell = effects.Spell, EventId = effects.EventId, EventTrigger = effects.EventTrigger } };
        var descriptor = ItemDescriptor.Get(recipe.ItemId);
        var summary = T("Choose one inventory copy. Its existing bonuses are preserved.");
        if (effects.TargetRarity >= 0 && Options.Instance.Items.TryGetRarityName(effects.TargetRarity, out var rarity)) summary += "\n" + T("Rarity") + ": " + T(rarity);
        if (descriptor != null) summary += "\n" + string.Join(" · ", EquipmentPresentation.Details(descriptor, preview).Select(p => p.Key + ": " + p.Value));
        Text(this, "UpgradeSummary", summary, inset, 79, width, 80, 12).WrappingBehavior = WrappingBehavior.Wrapped;
        var list = new ScrollControl(this, "UpgradeCopies") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = false };
        list.SetBounds(inset, 166, width, Math.Max(64, Height - 356)); Shop.ShopWindow.ConfigureNativeScrollbar(list);
        var y = 0;
        if (Globals.Me is {} player) for (var i = 0; i < player.Inventory.Length; i++)
        {
            var item = player.Inventory[i]; if (item.ItemId != recipe.ItemId || item.Quantity != 1) continue;
            var slot = i; var expected = new ItemProperties(item.ItemProperties);
            var title = string.Format(T("Inventory slot {0}"), slot + 1);
            if (descriptor != null && Options.Instance.Items.TryGetRarityName(descriptor.EffectiveRarity(item.ItemProperties), out var tier)) title += " · " + T(tier);
            var stats = string.Join(", ", item.ItemProperties.StatModifiers.Select((value, index) => value == 0 ? "" : T(((Intersect.Enums.Stat)index).ToString()) + " " + value.ToString("+0;-0")).Where(s => s.Length > 0));
            var caption = title + (stats.Length > 0 ? " · " + stats : "");
            var button = Button(list, "UpgradeCopy" + slot, caption, 0, y, width - 24, 44, () => { _selected = slot; _expected = expected; _selection.Text = caption; _confirm.IsDisabled = false; EquipmentPresentation.Play("select"); });
            button.TooltipText = caption + "\n" + (descriptor == null ? "" : string.Join("\n", EquipmentPresentation.Details(descriptor, item.ItemProperties).Select(p => p.Key + ": " + p.Value)));
            y += 50;
        }
        if (y == 0) Text(list, "NoUpgradeCopy", T("No matching equipment in your inventory."), 4, 4, width - 30, 60, 12).WrappingBehavior = WrappingBehavior.Wrapped;
        _selection = Text(this, "UpgradeSelection", T("Select an item above."), inset, Height - 181, width, 35, 12); _selection.WrappingBehavior = WrappingBehavior.Wrapped;
        _confirm = Button(this, "ConfirmEquipmentUpgrade", T("Improve selected item"), inset, Height - 128, (width - 8) / 2, 32, () => { if (_selected < 0 || _expected == null) return; _confirm.IsDisabled = true; apply(_selected, _expected); Hide(); });
        _confirm.IsDisabled = true;
        Button(this, "CancelEquipmentUpgrade", T("Close"), inset + (width + 8) / 2, Height - 128, (width - 8) / 2, 32, Hide);
        Text(this, "UpgradeRisk", string.Format(T("Failure: {0}%. Material loss on failure: {1}%. The selected item is kept."), recipe.FailureChance, recipe.ItemLossChance), inset, Height - 91, width, 38, 10).WrappingBehavior = WrappingBehavior.Wrapped;
        list.UpdateScrollBars(); Interface.InputBlockingComponents.Add(this);
        Disposed += (_, _) => Interface.InputBlockingComponents.Remove(this);
    }
    protected override void EnsureInitialized() { }
}
