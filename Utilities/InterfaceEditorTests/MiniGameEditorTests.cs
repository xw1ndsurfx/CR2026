using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;

internal static class MiniGameEditorTests
{
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Fields)!.GetValue(instance)!;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Render(Form form, string name)
    {
        var folder = Environment.GetEnvironmentVariable("CR2026_MINIGAME_EDITOR_PREVIEW");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        form.ShowInTaskbar = false; form.Opacity = 0; form.Show();
        Application.DoEvents(); form.PerformLayout();
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(Path.Combine(folder, name + ".png"));
        form.Hide();
    }
    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "CR2026-editor-sounds-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "effect.wav"), []);
            File.WriteAllBytes(Path.Combine(folder, "ignore.mp3"), []);
            using var picker = new EditorSoundPicker(folder);
            Check(picker.Items.Count == 2 && picker.FileName == "", "Sound picker lists WAV files plus None");
            picker.FileName = "missing.wav";
            picker.ReloadSounds();
            Check(picker.FileName == "missing.wav", "Missing sounds survive refresh and editing");
            File.WriteAllBytes(Path.Combine(folder, "missing.wav"), []);
            picker.ReloadSounds();
            Check(picker.FileName == "missing.wav" && picker.SelectedItem!.ToString() == "missing.wav", "Imports resolve missing sounds without losing selection");
            picker.FileName = "";
            Check(picker.FileName == "" && picker.SelectedIndex == 0, "None saves an empty filename");
        }
        finally { Directory.Delete(folder, true); }

        using (var rewards = new FrmRewardConfiguration())
        {
            var tabs = Field<TabControl>(rewards, "_tabs");
            Check(rewards.MaximizeBox && rewards.MinimumSize.Width >= 960 && tabs.TabPages.Count == 1, "Daily rewards opens a readable resizable workspace");
            rewards.NavigateGame(MiniGameType.Potions);
            Check(tabs.TabPages.Count == 3, "Potions groups recipes, progression rewards and event settings");
            Render(rewards, "minigame-potions");
            var recipes = tabs.TabPages[0];
            var rewardsPage = tabs.TabPages[1];
            var xp = Field<List<PokerLevelReward>>(rewards, "_potionLevels");
            var original = xp.Count;
            rewards.NavigateGame(MiniGameType.Roulette);
            Check(tabs.TabPages.Count == 1 && tabs.TabPages[0].Text == "Events & settings", "Roulette settings are accessible by game");
            rewards.NavigateGame(MiniGameType.Potions);
            Check(ReferenceEquals(recipes, tabs.TabPages[0]) && ReferenceEquals(rewardsPage, tabs.TabPages[1]) && xp.Count == original,
                "Switching games preserves page instances and unsaved drafts");
            foreach (var game in Enum.GetValues<MiniGameType>()) rewards.NavigateGame(game);
        }
        foreach (var name in new[] { "CookingRecipeDialog", "PotionRecipeDialog" })
        {
            var type = typeof(FrmRewardConfiguration).Assembly.GetType("Intersect.Editor.Forms.Editors." + name)!;
            using var dialog = (Form)Activator.CreateInstance(type, new object?[] { null })!;
            Render(dialog, name);
            Check(dialog.MaximizeBox && dialog.BackColor.R < 80, "Recipe editors use a dark, maximizable layout");
            if (name == "CookingRecipeDialog")
            {
                var sounds = type.GetFields(Fields).Where(f => f.FieldType == typeof(EditorSoundPicker)).ToArray();
                Check(sounds.Length == 10, "All recipe and stage sound fields are dropdowns");
                var sections = dialog.Controls.OfType<TabControl>().Single();
                Check(sections.TabPages.Count == 4, "Cooking separates recipe, ingredients, stages and results");
                foreach (var sound in sounds)
                    Check(!((Control)sound.GetValue(dialog)!).IsDisposed, "Moving recipe fields into sections preserves all sound controls");
            }
        }
        var item = new ItemDescriptor(Guid.NewGuid()) { Price = 13 };
        var currency = new ItemDescriptor(Guid.NewGuid()) { Price = 0 };
        Check(ShopPriceSuggestion.Suggest(item, currency, false) == 13, "Default currency can suggest the base item price");
        ItemDescriptor.Lookup[item.Id] = item;
        ItemDescriptor.Lookup[currency.Id] = currency;
        using (var shop = new FrmShop())
        {
            var items = Field<ComboBox>(shop, "cmbAddSoldItem");
            var currencies = Field<ComboBox>(shop, "cmbSellFor");
            foreach (var descriptor in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>())
            { items.Items.Add(descriptor.Name); currencies.Items.Add(descriptor.Name); }
            items.SelectedIndex = ItemDescriptor.ListIndex(item.Id);
            currencies.SelectedIndex = ItemDescriptor.ListIndex(currency.Id);
            var sold = Field<Control>(shop, "grpItemsSold");
            var button = sold.Controls["SuggestSellPrice"]!;
            var cost = Field<NumericUpDown>(shop, "nudSellCost");
            Check(button.Enabled, "Suggested price is enabled for default currency without a conversion value");
            typeof(Control).GetMethod("OnClick", Fields)!.Invoke(button, [EventArgs.Empty]);
            Check(cost.Value == 13, "Suggested price applies the item base value");
            Field<Control>(shop, "pnlContainer").Visible = true;

            Check(button.Width > TextRenderer.MeasureText(button.Text, button.Font).Width && button.Top > cost.Bottom,
                "Suggested price has a full separate row without truncation");
            foreach (Control control in sold.Controls)
                if (control != button) Check(!button.Bounds.IntersectsWith(control.Bounds), "Suggested price does not overlap another shop control");
        }
        System.Console.WriteLine("PASS mini-game navigation, draft preservation, recipe layout, WAV selectors and readable shop price controls");
    }
}
