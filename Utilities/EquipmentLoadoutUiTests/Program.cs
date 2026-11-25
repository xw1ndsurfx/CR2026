using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Network.Packets.EquipmentLoadouts;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using TextBox = Intersect.Client.Framework.Gwen.Control.TextBox;
using Point = Intersect.Point;
using Console = System.Console;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static IEnumerable<Base> All(Base root) { foreach (var c in root.Children) { yield return c; foreach (var n in All(c)) yield return n; } }
    private static void Click(Button button)
    {
        Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl = button;
        var method = typeof(Base).GetMethod("InputMouseButtonState", Hidden)!;
        method.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), true, false]);
        method.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), false, false]);
    }
    [STAThread] private static void Main()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogProxy>();
        var content = new TestContentManager();
        foreach (var name in new[] { "equipment_loadouts_v1.png", "equipment_forge_v1.png", "equipment_icons_v1.png" })
            content.AddGuiTexture(name, new BitmapTexture(Path.Combine(Directory.GetCurrentDirectory(), "Intersect.Client", "resources", "gui", name)));
        var output = Path.Combine(Directory.GetCurrentDirectory(), "validation-local", "equipment-loadouts-ui"); Directory.CreateDirectory(output);
        CheckBindings(output);
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.EquipmentLoadoutWindow", true)!;
        foreach (var language in new[] { "en", "fr", "es" }) foreach (var size in new[] { new Point(640, 480), new Point(1280, 720), new Point(1920, 1080) })
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, language);
            using var renderer = new DrawingRenderer(size.X, size.Y); using var skin = new EquipmentSkin(renderer); using var canvas = new Canvas(skin) { Size = size };
            var sent = new List<EquipmentLoadoutRequestPacket>();
            var window = (Base)Activator.CreateInstance(type, Hidden, null, [canvas, new Action<EquipmentLoadoutRequestPacket>(sent.Add)], null)!;
            Button Find(string name) => All(window).OfType<Button>().Single(b => b.Name == name);
            var view = new EquipmentLoadoutView { CharacterId = Guid.NewGuid(), Revision = 3 };
            void Apply(Guid request = default) => type.GetMethod("Apply", Hidden)!.Invoke(window, [view, request, true]);
            void Render() { canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero); canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero); }
            Apply(); Render();
            Check(window.Width <= size.X && window.Height <= size.Y, "Window exceeds viewport");
            Check(Find("ApplyEquipmentSet").IsDisabled && !Find("SaveEquipmentSet").IsDisabled, "Empty set state");
            Check(Find("SaveEquipmentSet").Text == EquipmentLoadoutText.Get("Save", language), "Untranslated save button");
            var name = All(window).OfType<TextBox>().Single(); name.Text = "Donjon"; Render();
            Check(name.Font != null && name.TextWidth >= 25 && name.TextHeight >= 10 && name.TextHeight <= name.Height, "Equipment name is clipped or has no font");
            Click(Find("SaveEquipmentSet")); Render();
            Check(sent.Count == 1 && sent[0].Action == EquipmentLoadoutAction.Save && sent[0].Name == "Donjon" && sent[0].Revision == 3, "Wrong save request");
            Check(Find("SaveEquipmentSet").IsDisabled && Find("ApplyEquipmentSet").IsDisabled, "Duplicate pending mutation");
            var set = new EquipmentLoadout { Name = "Donjon", SlotNames = Enumerable.Range(1, 16).Select(i => "Slot " + i).ToArray(), Items = new EquipmentLoadoutItem?[16] };
            view.Sets[0] = set; view.Revision++; Apply(sent.Last().RequestId); Render();
            Click(Find("SaveEquipmentSet")); Render(); Check(sent.Count == 1, "Overwrite requires confirmation");
            Check(Find("SaveEquipmentSet").TextWidth <= Find("SaveEquipmentSet").Width - 8, "Replacement confirmation is clipped");
            Click(Find("SaveEquipmentSet")); Check(sent.Count == 2, "Overwrite confirmation missing");
            Apply(sent.Last().RequestId); Render();
            Click(Find("DeleteEquipmentSet")); Check(sent.Count == 2, "Delete requires confirmation");
            Click(Find("EquipmentSet1")); Render(); Check(Find("ApplyEquipmentSet").IsDisabled, "Second slot must be empty");
            Click(Find("EquipmentSet0")); Render(); Click(Find("ApplyEquipmentSet"));
            Check(sent.Last().Action == EquipmentLoadoutAction.Apply, "Equip sends wrong action");
            type.GetField("_pendingUntil", Hidden)!.SetValue(window, Environment.TickCount64 - 1); type.GetMethod("UpdateState", Hidden)!.Invoke(window, []); Render();
            Check(Find("SaveEquipmentSet").IsDisabled && !Find("RefreshEquipmentSets").IsDisabled, "Uncertain mutation requires refresh");
            Click(Find("RefreshEquipmentSets")); Check(sent.Last().Action == EquipmentLoadoutAction.Refresh, "Refresh unavailable after timeout");
            view.Message = ""; Apply(sent.Last().RequestId); Render(); var count = All(window).Count(); for (var n = 0; n < 4; n++) { Apply(); Render(); }
            Check(All(window).Count() == count, "Refresh leaks controls");
            var items = All(window).OfType<ScrollControl>().Single(); items.VerticalScrollBar.ScrollAmount = 1; Render();
            Check(items.VerticalScrollBar.ScrollAmount > .9, "Cannot reach last equipment slot");
            Check(items.Bottom < Find("SaveEquipmentSet").Y, "Slots overlap action buttons");
            renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"sets-{language}-{size.X}x{size.Y}.png"));
            view.BlockReason = "Equipment sets cannot be changed during combat."; Apply(); Render(); Check(Find("ApplyEquipmentSet").IsDisabled, "Combat permits apply");
            view.BlockReason = ""; view.Problems[0] = "A saved item is missing from your inventory or its bonuses have changed."; Apply(); Render(); Check(Find("ApplyEquipmentSet").IsDisabled, "Missing piece permits apply");
            Click(Find("CloseEquipmentSets")); Check(window.IsHidden, "Close broken");
            var upgradeType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.EquipmentUpgradeWindow", true)!;
            var recipe = new Intersect.Framework.Core.GameObjects.Crafting.CraftingRecipeDescriptor(Guid.NewGuid()) { Name = "Royal forge", EquipmentUpgrade = new() { TargetRarity = 3 }, FailureChance = 15, ItemLossChance = 50 };
            var forge = (Base)Activator.CreateInstance(upgradeType, Hidden, null, [canvas, recipe, new Action<int, Intersect.Framework.Core.GameObjects.Items.ItemProperties>((_, _) => throw new Exception("No copy must not craft"))], null)!;
            forge.Show(); Render();
            Check(All(forge).OfType<Button>().Single(b => b.Name == "ConfirmEquipmentUpgrade").IsDisabled, "Craft without selected item");
            foreach (var label in All(forge).OfType<Label>().Where(l => l.Name is "UpgradeSummary" or "UpgradeRisk")) Check(label.TextHeight <= label.Height, "Forge text clipped: " + language + " " + label.Name);
            canvas.RemoveChild(forge, true);
            // Feed two real inventory copies to the production selector without starting a game/network session.
            var player = (Intersect.Client.Entities.Player)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player));
            GC.SuppressFinalize(player);
            var descriptor = new Intersect.Framework.Core.GameObjects.Items.ItemDescriptor(Guid.NewGuid()) { Name = "Royal blade", ItemType = Intersect.Framework.Core.GameObjects.Items.ItemType.Equipment, EquipmentSlot = Options.Instance.Equipment.WeaponSlot, Rarity = 1 };
            Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.Set(descriptor.Id, descriptor);
            var copies = Enumerable.Range(0, 2).Select(_ => new Intersect.Client.Items.Item { ItemId = descriptor.Id, Quantity = 1, ItemProperties = new() }).ToArray();
            copies[1].ItemProperties.StatModifiers[0] = 12;
            typeof(Intersect.Client.Entities.Entity).GetField("<Inventory>k__BackingField", Hidden)!.SetValue(player, copies);
            Intersect.Client.General.Globals.Me = player; recipe.ItemId = descriptor.Id;
            var selected = -1; Intersect.Framework.Core.GameObjects.Items.ItemProperties? expected = null;
            var choose = new Action<int, Intersect.Framework.Core.GameObjects.Items.ItemProperties>((slot, props) => { selected = slot; expected = props; });
            forge = (Base)Activator.CreateInstance(upgradeType, Hidden, null, [canvas, recipe, choose], null)!; forge.Show(); Render();
            Click(All(forge).OfType<Button>().Single(b => b.Name == "UpgradeCopy1")); Render();
            renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"forge-{language}-{size.X}x{size.Y}.png"));
            copies[1].ItemProperties.StatModifiers[0] = 99;
            Click(All(forge).OfType<Button>().Single(b => b.Name == "ConfirmEquipmentUpgrade"));
            Check(selected == 1 && expected?.StatModifiers[0] == 12 && forge.IsHidden, "Selector lost the selected copy snapshot");
            canvas.RemoveChild(forge, true); Intersect.Client.General.Globals.Me = null; Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.Delete(descriptor);
            Console.WriteLine($"PASS equipment sets: save, confirmation, apply, timeout, combat, missing items, scroll, localization / {language} / {size}");
        }
    }

    private static void CheckBindings(string output)
    {
        var file = Path.Combine(output, "controls-" + Guid.NewGuid().ToString("N") + ".json");
        Intersect.Client.General.Globals.Database = new Intersect.Client.Framework.Database.JsonDatabase(file);
        var input = new TestInput();
        var controls = new Intersect.Client.Core.Controls.Controls(); input.ControlSet = controls;
        for (var slot = 0; slot < EquipmentLoadoutRules.Count; slot++)
        {
            var key = Intersect.Client.Framework.Input.Control.EquipmentSet1 + slot;
            Check(input.AllControls.Contains(key), "Equipment key absent from Settings controls");
            Check(controls.TryGetMappingFor(key, out var mapping) && mapping.Bindings.All(b => b.Key == Intersect.Client.Framework.GenericClasses.Keys.None), "New key overwrites old controls");
            controls[key] = new(new Intersect.Client.Framework.Input.ControlBinding(Intersect.Client.Framework.GenericClasses.Keys.Control, Intersect.Client.Framework.GenericClasses.Keys.F3 + slot), Intersect.Client.Framework.Input.ControlBinding.Default);
        }
        Check(input.AllControls.Contains(Intersect.Client.Framework.Input.Control.WeaponSpell), "Weapon spell missing from Settings");
        Check(controls.TryGetMappingFor(Intersect.Client.Framework.Input.Control.WeaponSpell, out var power) && power.Bindings.All(b => b.Key == Intersect.Client.Framework.GenericClasses.Keys.None), "Weapon spell overrides existing key");
        controls[Intersect.Client.Framework.Input.Control.WeaponSpell] = new(new(Intersect.Client.Framework.GenericClasses.Keys.Alt, Intersect.Client.Framework.GenericClasses.Keys.F2), Intersect.Client.Framework.Input.ControlBinding.Default);
        Check(controls.TrySave(), "Cannot save control preferences");
        Intersect.Client.General.Globals.Database = new Intersect.Client.Framework.Database.JsonDatabase(file);
        var restored = new Intersect.Client.Core.Controls.Controls(); input.ControlSet = restored;
        Check(restored.TryGetMappingFor(Intersect.Client.Framework.Input.Control.WeaponSpell, out var restoredPower) && restoredPower.Bindings[0].Key == Intersect.Client.Framework.GenericClasses.Keys.F2, "Weapon spell key not persisted");
        for (var slot = 0; slot < EquipmentLoadoutRules.Count; slot++)
        {
            var control = Intersect.Client.Framework.Input.Control.EquipmentSet1 + slot;
            Check(restored.TryGetMappingFor(control, out var mapping) && mapping.Bindings[0].Modifier == Intersect.Client.Framework.GenericClasses.Keys.Control && mapping.Bindings[0].Key == Intersect.Client.Framework.GenericClasses.Keys.F3 + slot, "Keybind lost on restart");
        }
        input.KeysDown = [Intersect.Client.Framework.GenericClasses.Keys.Control, Intersect.Client.Framework.GenericClasses.Keys.F3];
        Check(Intersect.Client.Core.Controls.Controls.IsControlJustPressed(Intersect.Client.Framework.Input.Control.EquipmentSet1), "Configured key does not fire");
        input.KeysBefore = input.KeysDown.ToArray();
        Check(!Intersect.Client.Core.Controls.Controls.IsControlJustPressed(Intersect.Client.Framework.Input.Control.EquipmentSet1), "Held key repeats");
        input.KeysDown = []; input.KeysBefore = [];
        Console.WriteLine("PASS five equipment shortcuts: discovery, defaults, custom keys persisted and held-key suppression");
    }
}
public class LogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}

internal sealed class TestInput : Intersect.Client.Framework.Input.GameInput
{
    public TestInput() : base(true) { }
    public Intersect.Client.Framework.GenericClasses.Keys[] KeysDown = [], KeysBefore = [];
    public override Intersect.Client.Framework.Input.IControlSet ControlSet { get; set; } = null!;
    public override bool MouseHitInterface => false;
    public override bool IsMouseInBounds => true;
    public override bool IsMouseButtonDown(Intersect.Client.Framework.Input.MouseButton button) => false;
    public override bool WasMouseButtonDown(Intersect.Client.Framework.Input.MouseButton button) => false;
    public override bool IsKeyDown(Intersect.Client.Framework.GenericClasses.Keys key) => KeysDown.Contains(key);
    public override bool WasKeyDown(Intersect.Client.Framework.GenericClasses.Keys key) => KeysBefore.Contains(key);
    public override Intersect.Client.Framework.Input.InputDeviceType CursorMovementDevice { get; set; }
    public override System.Numerics.Vector2 GetMousePosition() => default;
    public override void Update(TimeSpan elapsed) { }
    public override void OpenKeyboard(Intersect.Client.Framework.Input.KeyboardType type, string text, bool autoCorrection, bool multiLine, bool secure) { }
    public override void OpenKeyboard(Intersect.Client.Framework.Input.KeyboardType type, Action<string?> handler, string description, string text, bool multiline = false, uint maxLength = 1024, Intersect.Client.Framework.GenericClasses.Rectangle? inputBounds = default) { }
}
