using Intersect.Editor.Forms.Editors;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.GameObjects;
using Intersect.Localization;

internal static class AutoCreatorsTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        var previousDirectory = Environment.CurrentDirectory;
        var folder = Path.Combine(Path.GetTempPath(), "AutoCreators-" + Guid.NewGuid());
        var animation = new AnimationDescriptor(Guid.NewGuid()) { Name = "Projectile preview fixture" };
        try
        {
            foreach (var category in new[] { "entities", "items", "spells", "animations" })
            {
                Directory.CreateDirectory(Path.Combine(folder, "resources", category));
                using var image = new Bitmap(40, 40); using var graphics = Graphics.FromImage(image);
                graphics.Clear(System.Drawing.Color.Gold); graphics.FillRectangle(Brushes.Red, 0, 0, 10, 10);
                image.Save(Path.Combine(folder, "resources", category, "fixture.png"));
            }
            Environment.CurrentDirectory = folder;
            animation.Lower.Sprite = "fixture.png"; animation.Lower.XFrames = 4; animation.Lower.YFrames = 4;
            animation.Lower.FrameCount = 4; AnimationDescriptor.Lookup.Set(animation.Id, animation);
            foreach (var category in new[] { "entities", "items", "spells" }) TestPicker(category);
            TestProfiles(animation.Id);
            TestDialog(animation.Id);
            using var editor = new FrmProjectile();
            Check(editor.Controls.Find("ProjectileAutoCreator", true).Length == 1, "Projectile editor exposes the creator button");
            var save = editor.Controls.Find("btnSave", true).Single();
            Intersect.Editor.Forms.Styling.EditorWorkspaceLayout.Apply(editor,
                editor.Controls.Find("grpProjectiles", true).Single(), editor.Controls.Find("pnlContainer", true).Single(),
                save, editor.Controls.Find("btnCancel", true).Single());
            var creator = editor.Controls.Find("ProjectileAutoCreator", true).Single();
            Check(creator.Parent == save.Parent && creator.Parent is FlowLayoutPanel,
                "The creator follows Save into the responsive footer instead of being covered by it");
            foreach (var text in new[] { "NPC sprite", "Item icon", "Spell icon", "Auto Projectile Creator", "Travel time (ms)",
                "{0} projectile(s) per burst × {1} burst(s) = {2} total\nRange: {3} tiles | Travel: {4} ms\nDelay: {5} ms | Total sequence: {6} ms\nReview the preview, then Apply and Save in the editor." })
            foreach (var language in GameTextCatalog.Languages)
                Check(QualityOfLifeText.Catalog.Entries.TryGetValue(text, out var entry) && entry.ContainsKey(language) &&
                    GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)), "Translations and placeholders: " + text);
            Console.WriteLine("PASS auto creators: sprite/item/spell selectors and previews, detached drafts, spawn patterns, timing, targeting, references, serialization, bounds and three locales");
        }
        finally { Environment.CurrentDirectory = previousDirectory; AnimationDescriptor.Lookup.Delete(animation); Directory.Delete(folder, true); }
    }

    private static void TestPicker(string category)
    {
        using var dialog = new Form { Size = new(510, 715), MinimumSize = new(420, 370), AutoScroll = true };
        var apply = new Button { Text = "Apply", Location = new(260, 690), Size = new(150, 32) }; dialog.Controls.Add(apply);
        using var target = new ComboBox(); target.Items.AddRange(["None", "fixture.png"]); target.SelectedIndex = 0;
        var picker = AutoCreatorAssetPicker.Insert(dialog, "TestPicker", "Item icon", category, ["fixture.png"], "");
        var choices = picker.Controls.Find("AssetChoices", true).OfType<ComboBox>().Single();
        var picture = picker.Controls.Find("AssetPreview", true).OfType<PictureBox>().Single();
        choices.SelectedIndex = 1;
        Check(target.SelectedIndex == 0 && picker.SelectedAsset == "fixture.png", "A draft selection does not change the main editor");
        Check(picture.Image != null && picture.Image.Width == (category == "entities" ? 10 : 40), "Entity frames and icons use the correct preview shape");
        picker.ApplyTo(target); Check(target.SelectedIndex == 1, "Apply transfers the exact filename");
        choices.SelectedIndex = 0; picker.ApplyTo(target);
        Check(target.SelectedIndex == 0 && picture.Image == null, "None clears the selected resource and preview");
        using var missing = new AutoCreatorAssetPicker("Spell icon", category, [], "missing.png");
        Check(missing.SelectedAsset == "missing.png", "An existing missing resource is preserved, not silently removed");
        missing.ApplyTo(target);
        Check(target.SelectedIndex == 0 && target.Items.Count == 2, "A missing reference never forces the legacy editor to load a missing file");
        using var blank = new AutoCreatorAssetPicker("Item icon", category, [], "  ");
        Check(blank.SelectedAsset == "", "Whitespace means no selected resource");
        foreach (var size in new[] { new Size(510, 715), new Size(420, 370) })
        {
            dialog.Size = size; dialog.PerformLayout();
            Check(dialog.AutoScrollMinSize.Height >= apply.Bottom && picker.Bottom <= apply.Top, "The asset row and Apply remain reachable via scrolling");
        }
    }

    private static void TestProfiles(Guid animationId)
    {
        var source = new ProjectileDescriptor(Guid.NewGuid()) { AmmoItemId = Guid.NewGuid(), AmmoRequired = 3,
            SpellId = Guid.NewGuid(), IgnoreActiveResources = true, IgnoreZDimension = true, Folder = "Authored" };
        source.GrappleHookOptions.Add(GrappleOption.NPC);
        foreach (var pattern in Enum.GetValues<ProjectileAutoPattern>())
        foreach (var behavior in Enum.GetValues<ProjectileAutoBehavior>())
        {
            var profile = new ProjectileAutoProfile { Pattern = pattern, Behavior = behavior, AnimationId = animationId, Bursts = 4, Range = 9, TravelTime = 750, Delay = 130 };
            profile.Apply(source);
            Check(source.SpawnLocations.Cast<Intersect.GameObjects.Location>().Sum(location => location.Directions.Count(value => value)) == profile.Rays,
                "Each generated pattern replaces previous spawn directions");
            Check(source.SpawnLocations[2, 2].Directions[(int)Direction.Up], "Forward is relative to the shooter's direction");
            Check(source.Speed == 750 && source.Delay == 130 && source.Range == 9 && source.Quantity == 4, "Travel duration, burst delay and range keep engine units");
            Check(source.HomingBehavior == (behavior == ProjectileAutoBehavior.Homing) && source.DirectShotBehavior == (behavior == ProjectileAutoBehavior.Direct), "Targeting flags are exclusive");
            Check(source.Animations.Single().AnimationId == animationId && source.Animations[0].SpawnRange == 4, "Selected animation covers every burst");
            Check(source.AmmoItemId != Guid.Empty && source.AmmoRequired == 3 && source.SpellId != Guid.Empty && source.IgnoreActiveResources &&
                source.IgnoreZDimension && source.GrappleHookOptions.Contains(GrappleOption.NPC) && source.Folder == "Authored", "Authored references and special collision settings survive generation");
        }
        var restored = new ProjectileDescriptor(Guid.NewGuid()); restored.Load(source.JsonData, true);
        Check(restored.Quantity == source.Quantity && restored.Speed == source.Speed && restored.Animations[0].AnimationId == animationId, "Generated projectile persists through native serialization");
        var before = source.JsonData;
        try { new ProjectileAutoProfile { Range = 0 }.Apply(source); throw new Exception("Invalid range accepted"); }
        catch (ArgumentOutOfRangeException) { Check(source.JsonData == before, "Invalid input cannot partially change the projectile"); }
    }

    private static void TestDialog(Guid animationId)
    {
        var source = new ProjectileDescriptor(Guid.NewGuid()) { Name = "Original projectile", Speed = 500, Range = 6 };
        source.Animations.Add(new ProjectileAnimation(animationId, 1, true));
        var before = source.JsonData;
        using (var cancelled = new FrmProjectileAutoCreator(source))
        {
            ((ComboBox)cancelled.Controls.Find("ProjectilePattern", true).Single()).SelectedIndex = 3;
            Check(cancelled.Result == null && source.JsonData == before, "Cancelling a draft leaves the source unchanged");
        }
        using var dialog = new FrmProjectileAutoCreator(source) { ShowInTaskbar = false };
        ((ComboBox)dialog.Controls.Find("ProjectilePattern", true).Single()).SelectedIndex = 3;
        ((NumericUpDown)dialog.Controls.Find("ProjectileBursts", true).Single()).Value = 4;
        Check(((Label)dialog.Controls.Find("ProjectileSummary", true).Single()).Text.Contains("32"), "Preview counts all rays in all bursts");
        dialog.Show(); Application.DoEvents();
        if (Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is { Length: > 0 } previewFolder)
        {
            Directory.CreateDirectory(previewFolder);
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(previewFolder, "auto-projectile-creator.png"));
        }
        ((Button)dialog.Controls.Find("ApplyProjectileProfile", true).Single()).PerformClick();
        Check(dialog.Result is { Pattern: ProjectileAutoPattern.Radial, Bursts: 4 } && source.JsonData == before,
            "Apply returns a validated draft; the editor remains responsible for changing and saving the source");
    }
}
