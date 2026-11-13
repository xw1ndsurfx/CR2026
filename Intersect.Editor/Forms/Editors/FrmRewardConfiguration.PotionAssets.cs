using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmRewardConfiguration
{
    private static string PotionAssetText(string text) => QualityOfLifeText.Get(text,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private TabPage BuildPotionAssetsTab()
    {
        var page = new TabPage(PotionAssetText("Artwork and sounds"));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        var row = 0;
        void Add(string title, Control control)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { Text = PotionAssetText(title), AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            control.Dock = DockStyle.Top;
            root.Controls.Add(control, 1, row++);
        }
        void ImagePicker(string title, string selected, Action<string> set)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 400 };
            var preview = new PictureBox { Height = 150, SizeMode = PictureBoxSizeMode.Zoom, BackColor = System.Drawing.Color.FromArgb(25, 25, 25) };
            void RefreshFiles()
            {
                var previous = box.SelectedItem as string ?? selected;
                box.BeginUpdate();
                box.Items.Clear(); box.Items.Add("");
                var directory = Path.Combine("resources", "gui");
                if (Directory.Exists(directory))
                    foreach (var file in Directory.EnumerateFiles(directory).Where(f => Path.GetExtension(f).Equals(".png", StringComparison.OrdinalIgnoreCase))
                        .Select(Path.GetFileName).OfType<string>().OrderBy(f => f)) box.Items.Add(file);
                if (!box.Items.Contains(previous)) box.Items.Add(previous);
                box.SelectedItem = previous;
                box.EndUpdate();
            }
            void UpdatePreview()
            {
                var file = box.SelectedItem as string ?? "";
                set(file);
                var old = preview.Image; preview.Image = null; old?.Dispose();
                if (file.Length == 0) return;
                try
                {
                    using var stream = File.OpenRead(Path.Combine("resources", "gui", file));
                    using var image = Image.FromStream(stream);
                    preview.Image = new Bitmap(image);
                }
                catch (Exception exception) when (exception is IOException or ArgumentException or OutOfMemoryException) { }
            }
            RefreshFiles();
            box.SelectedIndexChanged += (_, _) => UpdatePreview();
            box.DropDown += (_, _) => RefreshFiles();
            preview.Disposed += (_, _) => preview.Image?.Dispose();
            UpdatePreview();
            Add(title, box);
            Add("Preview", preview);
        }
        void SoundPicker(string title, string file, Action<string> set)
        {
            var picker = new EditorSoundPicker { FileName = file };
            picker.SelectedIndexChanged += (_, _) => set(picker.FileName);
            Add(title, picker);
        }
        ImagePicker("Workshop background", _working.PotionAssets.Background, file => _working.PotionAssets = _working.PotionAssets with { Background = file });
        ImagePicker("Ingredient atlas (4 x 4)", _working.PotionAssets.PieceAtlas, file => _working.PotionAssets = _working.PotionAssets with { PieceAtlas = file });
        SoundPicker("Rotate ingredients", _working.PotionAssets.RotateSound, file => _working.PotionAssets = _working.PotionAssets with { RotateSound = file });
        SoundPicker("Drop ingredients", _working.PotionAssets.DropSound, file => _working.PotionAssets = _working.PotionAssets with { DropSound = file });
        SoundPicker("Ingredient fusion", _working.PotionAssets.MergeSound, file => _working.PotionAssets = _working.PotionAssets with { MergeSound = file });
        SoundPicker("Recipe complete", _working.PotionAssets.CompleteSound, file => _working.PotionAssets = _working.PotionAssets with { CompleteSound = file });
        SoundPicker("Board full", _working.PotionAssets.GameOverSound, file => _working.PotionAssets = _working.PotionAssets with { GameOverSound = file });
        var hint = new Label { AutoSize = true, MaximumSize = new Size(740, 0), Padding = new Padding(3, 10, 3, 10),
            Text = PotionAssetText("Empty selection disables the asset. Atlas: rows = Verdant, Ember, Arcane, Radiant; columns = tiers 1 to 4. Install the files on every client. Missing images use the original drawing.") };
        root.Controls.Add(hint, 0, row); root.SetColumnSpan(hint, 2);
        page.Controls.Add(root);
        return page;
    }
}
