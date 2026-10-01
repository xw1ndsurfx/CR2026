using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.RegularExpressions;
using DarkUI.Forms;
using Intersect.Editor.Content;

namespace Intersect.Editor.Forms;

public sealed class FrmAnimationImport : DarkForm
{
    public sealed class GeneratedAnimationRequest
    {
        public required string Name { get; init; }

        public required string SpriteFile { get; init; }

        public required string Folder { get; init; }

        public int XFrames { get; init; }

        public int YFrames { get; init; }

        public int FrameCount { get; init; }

        public int FrameDuration { get; init; }
    }

    private sealed class AnimationAsset
    {
        public required string FilePath { get; init; }

        public required string FileName { get; init; }

        public required string SuggestedName { get; init; }

        public required string Category { get; init; }

        public int XFrames { get; init; }

        public int YFrames { get; init; }

        public int FrameCount => Math.Max(1, XFrames * YFrames);
    }

    private sealed class AnimationPreview : Control
    {
        private Bitmap? _image;
        private int _xFrames = 1;
        private int _yFrames = 1;
        private int _frame;

        public AnimationPreview()
        {
            DoubleBuffered = true;
            BackColor = System.Drawing.Color.FromArgb(17, 17, 17);
        }

        public void SetAnimation(Bitmap? image, int xFrames, int yFrames)
        {
            _image?.Dispose();
            _image = image;
            _xFrames = Math.Max(1, xFrames);
            _yFrames = Math.Max(1, yFrames);
            _frame = 0;
            Invalidate();
        }

        public void SetGrid(int xFrames, int yFrames)
        {
            _xFrames = Math.Max(1, xFrames);
            _yFrames = Math.Max(1, yFrames);
            _frame = Math.Min(_frame, Math.Max(0, _xFrames * _yFrames - 1));
            Invalidate();
        }

        public void Advance(int frameCount)
        {
            var count = Math.Max(1, Math.Min(frameCount, _xFrames * _yFrames));
            _frame = (_frame + 1) % count;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _image?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            const int checker = 16;
            using var dark = new SolidBrush(System.Drawing.Color.FromArgb(35, 35, 35));
            using var light = new SolidBrush(System.Drawing.Color.FromArgb(50, 50, 50));

            for (var y = 0; y < Height; y += checker)
            {
                for (var x = 0; x < Width; x += checker)
                {
                    e.Graphics.FillRectangle(
                        ((x / checker + y / checker) & 1) == 0 ? dark : light,
                        x,
                        y,
                        checker,
                        checker
                    );
                }
            }

            if (_image == null || _image.Width <= 0 || _image.Height <= 0)
            {
                return;
            }

            var frameWidth = Math.Max(1, _image.Width / _xFrames);
            var frameHeight = Math.Max(1, _image.Height / _yFrames);
            var column = _frame % _xFrames;
            var row = _frame / _xFrames;
            row = Math.Min(row, _yFrames - 1);

            var source = new Rectangle(
                column * frameWidth,
                row * frameHeight,
                Math.Min(frameWidth, _image.Width - column * frameWidth),
                Math.Min(frameHeight, _image.Height - row * frameHeight)
            );

            var scale = Math.Min(
                (double)Math.Max(1, Width - 24) / source.Width,
                (double)Math.Max(1, Height - 24) / source.Height
            );
            scale = Math.Max(1d, Math.Floor(scale));
            if (source.Width * scale > Width - 24 || source.Height * scale > Height - 24)
            {
                scale = Math.Min(
                    (double)Math.Max(1, Width - 24) / source.Width,
                    (double)Math.Max(1, Height - 24) / source.Height
                );
            }

            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            var destination = new Rectangle(
                (Width - width) / 2,
                (Height - height) / 2,
                width,
                height
            );

            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.DrawImage(_image, destination, source, GraphicsUnit.Pixel);
        }
    }

    private static readonly string[] Categories =
    {
        "Fire",
        "Ice",
        "Lightning",
        "Water",
        "Nature",
        "Poison",
        "Holy",
        "Dark",
        "Blood",
        "Smoke",
        "Magic",
        "Physical",
        "Misc",
    };

    private static readonly Dictionary<string, string[]> CategoryKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fire"] = new[] { "fire", "flame", "burn", "inferno", "ember", "lava", "meteor", "phoenix" },
            ["Ice"] = new[] { "ice", "frost", "freeze", "snow", "blizzard", "crystal" },
            ["Lightning"] = new[] { "lightning", "thunder", "electric", "shock", "volt", "spark" },
            ["Water"] = new[] { "water", "aqua", "wave", "splash", "bubble", "rain" },
            ["Nature"] = new[] { "nature", "leaf", "vine", "plant", "earth", "rock", "root", "grass" },
            ["Poison"] = new[] { "poison", "toxic", "acid", "venom", "slime" },
            ["Holy"] = new[] { "holy", "light", "heal", "angel", "divine", "bless" },
            ["Dark"] = new[] { "dark", "shadow", "curse", "void", "demon", "death", "necrom" },
            ["Blood"] = new[] { "blood", "bleed", "gore" },
            ["Smoke"] = new[] { "smoke", "dust", "cloud", "fog", "steam" },
            ["Magic"] = new[] { "magic", "mana", "arcane", "spell", "portal", "teleport", "aura" },
            ["Physical"] = new[] { "slash", "hit", "impact", "punch", "sword", "arrow", "bullet" },
        };

    private readonly string _gameRoot;
    private readonly string _importRoot;
    private readonly string _animationsRoot;
    private readonly Action<GeneratedAnimationRequest>? _afterImport;

    private readonly ListBox _categoryList = new();
    private readonly ListView _assetList = new();
    private readonly ImageList _assetImages = new();
    private readonly AnimationPreview _preview = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _category = new();
    private readonly NumericUpDown _xFrames = new();
    private readonly NumericUpDown _yFrames = new();
    private readonly NumericUpDown _frameCount = new();
    private readonly NumericUpDown _frameDuration = new();
    private readonly Label _assetInfo = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 80 };

    private readonly List<AnimationAsset> _assets = new();
    private AnimationAsset? _selectedAsset;

    public FrmAnimationImport(Action<GeneratedAnimationRequest>? afterImport = null)
    {
        _afterImport = afterImport;
        _gameRoot = ResolveGameRoot();
        _importRoot = Path.Combine(_gameRoot, "animationimport");
        _animationsRoot = Path.Combine(_gameRoot, "resources", "animations");

        Text = "Animations Import";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1380, 860);
        BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        ForeColor = System.Drawing.Color.Gainsboro;

        Directory.CreateDirectory(_importRoot);
        Directory.CreateDirectory(_animationsRoot);
        foreach (var categoryName in Categories)
        {
            Directory.CreateDirectory(Path.Combine(_importRoot, categoryName));
        }

        BuildInterface();

        _previewTimer.Tick += (_, _) =>
        {
            _preview.Advance((int)_frameCount.Value);
        };

        Shown += (_, _) =>
        {
            ReloadAssets();
            _previewTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            _previewTimer.Stop();
            _assetImages.Dispose();
        };
    }

    private static string ResolveGameRoot()
    {
        var candidates = new List<string>
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
        };

        var parent = Directory.GetParent(AppContext.BaseDirectory);
        for (var i = 0; i < 4 && parent != null; i++)
        {
            candidates.Add(parent.FullName);
            parent = parent.Parent;
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => Directory.Exists(Path.Combine(path, "resources")))
            ?? Environment.CurrentDirectory;
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        Controls.Add(root);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 9, 10, 7),
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };

        var importZip = CreateAccentButton("IMPORT ZIP");
        importZip.Size = new Size(145, 34);
        importZip.Click += (_, _) => ImportZip();

        var refresh = CreateDarkButton("REFRESH");
        refresh.Size = new Size(120, 34);
        refresh.Click += (_, _) => ReloadAssets();

        var open = CreateDarkButton("OPEN ANIMATIONIMPORT");
        open.Size = new Size(205, 34);
        open.Click += (_, _) => OpenImportFolder();

        toolbar.Controls.Add(importZip);
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(open);
        root.Controls.Add(toolbar, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(10),
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);

        var categoryPanel = CreateSection("CATEGORIES");
        _categoryList.Dock = DockStyle.Fill;
        StyleListBox(_categoryList);
        _categoryList.SelectedIndexChanged += (_, _) => PopulateAssetList();
        categoryPanel.Controls.Add(_categoryList, 0, 1);
        body.Controls.Add(categoryPanel, 0, 0);

        var assetPanel = CreateSection("ANIMATIONS");
        _assetImages.ImageSize = new Size(112, 112);
        _assetImages.ColorDepth = ColorDepth.Depth32Bit;

        _assetList.Dock = DockStyle.Fill;
        _assetList.View = View.LargeIcon;
        _assetList.LargeImageList = _assetImages;
        _assetList.MultiSelect = false;
        _assetList.HideSelection = false;
        _assetList.ShowItemToolTips = true;
        _assetList.BorderStyle = BorderStyle.None;
        _assetList.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        _assetList.ForeColor = System.Drawing.Color.Gainsboro;
        _assetList.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9);
        _assetList.SelectedIndexChanged += (_, _) => SelectAsset();

        assetPanel.Controls.Add(_assetList, 0, 1);
        body.Controls.Add(assetPanel, 1, 0);

        var previewPanel = CreateSection("PREVIEW");
        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(12, 12, 12),
        };

        _assetInfo.Dock = DockStyle.Top;
        _assetInfo.Height = 42;
        _assetInfo.ForeColor = System.Drawing.Color.Silver;
        _assetInfo.TextAlign = ContentAlignment.MiddleLeft;
        _assetInfo.Padding = new Padding(8, 0, 8, 0);

        _preview.Dock = DockStyle.Fill;

        previewHost.Controls.Add(_preview);
        previewHost.Controls.Add(_assetInfo);
        _assetInfo.BringToFront();
        previewPanel.Controls.Add(previewHost, 0, 1);
        body.Controls.Add(previewPanel, 2, 0);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };
        root.Controls.Add(footer, 0, 2);

        AddFooterLabel(footer, "Name:", 14, 15);
        _name.Location = new System.Drawing.Point(62, 10);
        _name.Size = new Size(265, 28);
        StyleTextBox(_name);
        footer.Controls.Add(_name);

        AddFooterLabel(footer, "Folder:", 345, 15);
        _category.Location = new System.Drawing.Point(398, 10);
        _category.Size = new Size(150, 28);
        _category.DropDownStyle = ComboBoxStyle.DropDown;
        _category.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _category.ForeColor = System.Drawing.Color.White;
        _category.Items.AddRange(Categories);
        footer.Controls.Add(_category);

        AddFooterLabel(footer, "X:", 570, 15);
        ConfigureNumber(footer, _xFrames, 596, 10, 1, 32, 1);
        AddFooterLabel(footer, "Y:", 668, 15);
        ConfigureNumber(footer, _yFrames, 694, 10, 1, 32, 1);
        AddFooterLabel(footer, "Frames:", 768, 15);
        ConfigureNumber(footer, _frameCount, 826, 10, 1, 1024, 1);
        AddFooterLabel(footer, "ms:", 910, 15);
        ConfigureNumber(footer, _frameDuration, 942, 10, 15, 2000, 80);

        var importSelected = CreateAccentButton("IMPORT TO GAME + OPEN EDITOR");
        importSelected.Location = new System.Drawing.Point(1040, 8);
        importSelected.Size = new Size(290, 34);
        importSelected.Click += (_, _) => ImportSelectedAsset();
        footer.Controls.Add(importSelected);

        _status.AutoSize = false;
        _status.Location = new System.Drawing.Point(14, 57);
        _status.Size = new Size(1315, 48);
        _status.ForeColor = System.Drawing.Color.Silver;
        footer.Controls.Add(_status);

        _xFrames.ValueChanged += (_, _) => GridChanged();
        _yFrames.ValueChanged += (_, _) => GridChanged();
        _frameCount.ValueChanged += (_, _) =>
        {
            var maximum = (int)_xFrames.Value * (int)_yFrames.Value;
            if (_frameCount.Value > maximum)
            {
                _frameCount.Value = maximum;
            }
        };
        _frameDuration.ValueChanged += (_, _) =>
        {
            _previewTimer.Interval = Math.Max(15, (int)_frameDuration.Value);
        };
    }

    private static TableLayoutPanel CreateSection(string title)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = System.Drawing.Color.FromArgb(247, 69, 96),
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(label, 0, 0);
        return panel;
    }

    private static Button CreateAccentButton(string text)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(247, 69, 96),
            ForeColor = System.Drawing.Color.White,
            FlatAppearance = { BorderColor = System.Drawing.Color.FromArgb(247, 69, 96) },
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
    }

    private static Button CreateDarkButton(string text)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(55, 47, 49),
            ForeColor = System.Drawing.Color.Gainsboro,
            FlatAppearance = { BorderColor = System.Drawing.Color.FromArgb(90, 78, 81) },
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
    }

    private static void StyleListBox(ListBox list)
    {
        list.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        list.ForeColor = System.Drawing.Color.Gainsboro;
        list.BorderStyle = BorderStyle.None;
        list.IntegralHeight = false;
        list.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10);
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        textBox.ForeColor = System.Drawing.Color.White;
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void AddFooterLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(
            new Label
            {
                AutoSize = true,
                Text = text,
                ForeColor = System.Drawing.Color.Gainsboro,
                Location = new System.Drawing.Point(x, y),
            }
        );
    }

    private static void ConfigureNumber(
        Control parent,
        NumericUpDown number,
        int x,
        int y,
        int minimum,
        int maximum,
        int value
    )
    {
        number.Location = new System.Drawing.Point(x, y);
        number.Size = new Size(62, 28);
        number.Minimum = minimum;
        number.Maximum = maximum;
        number.Value = value;
        number.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        number.ForeColor = System.Drawing.Color.White;
        number.BorderStyle = BorderStyle.FixedSingle;
        number.TextAlign = HorizontalAlignment.Center;
        parent.Controls.Add(number);
    }

    private void OpenImportFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _importRoot,
                    UseShellExecute = true,
                }
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Animations Import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportZip()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "ZIP archives (*.zip)|*.zip",
            Title = "Import animation ZIP",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var imported = 0;
        var skipped = 0;

        try
        {
            using var archive = ZipFile.OpenRead(dialog.FileName);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    entry.Length <= 0)
                {
                    continue;
                }

                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                buffer.Position = 0;

                using var bitmap = new Bitmap(buffer);
                var sourceName = Path.GetFileNameWithoutExtension(entry.Name);
                var categoryName = ClassifyAnimation(sourceName, bitmap);
                var categoryDirectory = Path.Combine(_importRoot, categoryName);
                Directory.CreateDirectory(categoryDirectory);

                var fileName = Path.GetFileName(entry.Name);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    skipped++;
                    continue;
                }

                var destination = GetUniquePath(Path.Combine(categoryDirectory, fileName));
                buffer.Position = 0;
                using var output = File.Create(destination);
                buffer.CopyTo(output);
                imported++;
            }

            ReloadAssets();
            _status.Text =
                $"ZIP imported: {imported} PNG(s), {skipped} skipped. Files were categorized automatically in animationimport.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Unable to import ZIP: " + ex.Message,
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 2; i < 10000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem}_{Guid.NewGuid():N}{extension}");
    }

    private void ReloadAssets()
    {
        _assets.Clear();

        foreach (var file in Directory.GetFiles(_importRoot, "*.png", SearchOption.AllDirectories))
        {
            try
            {
                using var bitmap = new Bitmap(file);
                var categoryName = Path.GetFileName(Path.GetDirectoryName(file)) ?? "Misc";
                var fileName = Path.GetFileName(file);
                var stem = Path.GetFileNameWithoutExtension(file);
                var (xFrames, yFrames) = DetectGrid(bitmap, stem);

                _assets.Add(
                    new AnimationAsset
                    {
                        FilePath = file,
                        FileName = fileName,
                        SuggestedName = FriendlyName(stem),
                        Category = categoryName,
                        XFrames = xFrames,
                        YFrames = yFrames,
                    }
                );
            }
            catch
            {
                // Ignore invalid images but leave the import tool usable.
            }
        }

        var selectedCategory = _categoryList.SelectedItem?.ToString();
        _categoryList.BeginUpdate();
        _categoryList.Items.Clear();

        var categories = _assets
            .Select(asset => asset.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(categoryName =>
            {
                var index = Array.FindIndex(
                    Categories,
                    value => string.Equals(value, categoryName, StringComparison.OrdinalIgnoreCase)
                );
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var categoryName in categories)
        {
            _categoryList.Items.Add(categoryName);
        }

        _categoryList.EndUpdate();

        if (!string.IsNullOrWhiteSpace(selectedCategory))
        {
            var index = _categoryList.FindStringExact(selectedCategory);
            if (index >= 0)
            {
                _categoryList.SelectedIndex = index;
            }
        }

        if (_categoryList.SelectedIndex < 0 && _categoryList.Items.Count > 0)
        {
            _categoryList.SelectedIndex = 0;
        }

        PopulateAssetList();
        _status.Text = $"Watching {_importRoot} - {_assets.Count} animation sheet(s) detected.";
    }

    private void PopulateAssetList()
    {
        var categoryName = _categoryList.SelectedItem?.ToString();

        _assetList.BeginUpdate();
        _assetList.Items.Clear();
        _assetImages.Images.Clear();

        foreach (var asset in _assets
                     .Where(asset =>
                         string.IsNullOrWhiteSpace(categoryName) ||
                         string.Equals(asset.Category, categoryName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(asset => asset.SuggestedName, StringComparer.OrdinalIgnoreCase))
        {
            using var thumbnail = CreateThumbnail(asset);
            _assetImages.Images.Add(asset.FilePath, new Bitmap(thumbnail));

            var item = new ListViewItem(asset.SuggestedName)
            {
                ImageKey = asset.FilePath,
                Tag = asset,
                ToolTipText =
                    $"{asset.FileName}\n{asset.Category}\nDetected grid: {asset.XFrames}x{asset.YFrames} ({asset.FrameCount} frames)",
            };
            _assetList.Items.Add(item);
        }

        _assetList.EndUpdate();

        if (_assetList.Items.Count > 0)
        {
            _assetList.Items[0].Selected = true;
        }
        else
        {
            _selectedAsset = null;
            _preview.SetAnimation(null, 1, 1);
            _assetInfo.Text = string.Empty;
        }
    }

    private static Bitmap CreateThumbnail(AnimationAsset asset)
    {
        const int size = 112;
        var output = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using var bitmap = new Bitmap(asset.FilePath);
        var frameWidth = Math.Max(1, bitmap.Width / Math.Max(1, asset.XFrames));
        var frameHeight = Math.Max(1, bitmap.Height / Math.Max(1, asset.YFrames));
        var source = new Rectangle(0, 0, frameWidth, frameHeight);

        using var graphics = Graphics.FromImage(output);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var scale = Math.Min(
            (double)(size - 8) / source.Width,
            (double)(size - 8) / source.Height
        );
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var destination = new Rectangle((size - width) / 2, (size - height) / 2, width, height);

        graphics.DrawImage(bitmap, destination, source, GraphicsUnit.Pixel);
        return output;
    }

    private void SelectAsset()
    {
        if (_assetList.SelectedItems.Count == 0)
        {
            return;
        }

        _selectedAsset = _assetList.SelectedItems[0].Tag as AnimationAsset;
        if (_selectedAsset == null)
        {
            return;
        }

        _name.Text = _selectedAsset.SuggestedName;
        _category.Text = _selectedAsset.Category;
        _xFrames.Value = Math.Clamp(_selectedAsset.XFrames, (int)_xFrames.Minimum, (int)_xFrames.Maximum);
        _yFrames.Value = Math.Clamp(_selectedAsset.YFrames, (int)_yFrames.Minimum, (int)_yFrames.Maximum);
        _frameCount.Value = Math.Min(
            _frameCount.Maximum,
            Math.Max(1, (int)_xFrames.Value * (int)_yFrames.Value)
        );
        _frameDuration.Value = 80;

        using var source = new Bitmap(_selectedAsset.FilePath);
        _preview.SetAnimation(new Bitmap(source), (int)_xFrames.Value, (int)_yFrames.Value);
        _previewTimer.Interval = (int)_frameDuration.Value;

        _assetInfo.Text =
            $"{_selectedAsset.FileName}   |   {_selectedAsset.Category}   |   " +
            $"{source.Width}x{source.Height}px   |   detected {(int)_xFrames.Value}x{(int)_yFrames.Value}";
    }

    private void GridChanged()
    {
        var maximum = Math.Max(1, (int)_xFrames.Value * (int)_yFrames.Value);
        _frameCount.Maximum = maximum;
        if (_frameCount.Value > maximum)
        {
            _frameCount.Value = maximum;
        }

        if (_frameCount.Value < 1)
        {
            _frameCount.Value = maximum;
        }

        _preview.SetGrid((int)_xFrames.Value, (int)_yFrames.Value);
    }

    private void ImportSelectedAsset()
    {
        if (_selectedAsset == null || !File.Exists(_selectedAsset.FilePath))
        {
            MessageBox.Show(
                this,
                "Select an animation first.",
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var animationName = _name.Text.Trim();
        var folder = _category.Text.Trim();
        if (string.IsNullOrWhiteSpace(animationName))
        {
            MessageBox.Show(
                this,
                "Enter an animation name before importing.",
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = "Misc";
        }

        var fileStem = MakeFileStem(animationName);
        if (string.IsNullOrWhiteSpace(fileStem))
        {
            MessageBox.Show(
                this,
                "The animation name cannot be converted to a valid filename.",
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        var spriteFile = fileStem + ".png";
        var destination = Path.Combine(_animationsRoot, spriteFile);
        var temporary = destination + ".tmp";

        try
        {
            File.Copy(_selectedAsset.FilePath, temporary, true);
            File.Move(temporary, destination, true);

            GameContentManager.ReloadAnimationTextures();

            _afterImport?.Invoke(
                new GeneratedAnimationRequest
                {
                    Name = animationName,
                    SpriteFile = spriteFile,
                    Folder = folder,
                    XFrames = (int)_xFrames.Value,
                    YFrames = (int)_yFrames.Value,
                    FrameCount = (int)_frameCount.Value,
                    FrameDuration = (int)_frameDuration.Value,
                }
            );

            _status.Text =
                $"Imported {animationName}: resources/animations/{spriteFile} - " +
                $"{(int)_xFrames.Value}x{(int)_yFrames.Value}, {(int)_frameCount.Value} frames.";

            MessageBox.Show(
                this,
                $"Animation imported.\n\n" +
                $"Name: {animationName}\n" +
                $"Folder: {folder}\n" +
                $"Grid: {(int)_xFrames.Value}x{(int)_yFrames.Value}\n" +
                $"Frames: {(int)_frameCount.Value}\n" +
                $"Sprite: resources\\animations\\{spriteFile}\n\n" +
                "The Animation Editor will open and prefill the detected settings.",
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Unable to import animation: " + ex.Message,
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static string FriendlyName(string stem)
    {
        var name = Regex.Replace(stem, @"[_\-]+", " ");
        name = Regex.Replace(name, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Animation";
        }

        return string.Join(
            " ",
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word =>
                    word.Length == 1
                        ? word.ToUpperInvariant()
                        : char.ToUpperInvariant(word[0]) + word[1..])
        );
    }

    private static string MakeFileStem(string value)
    {
        var cleaned = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalid, '_');
        }

        cleaned = Regex.Replace(cleaned, @"\s+", "_");
        cleaned = Regex.Replace(cleaned, @"_+", "_");
        return cleaned.Trim('_', '.', ' ');
    }

    private static (int X, int Y) DetectGrid(Bitmap bitmap, string stem)
    {
        var named = Regex.Match(stem, @"(?<!\d)(\d{1,2})\s*[xX]\s*(\d{1,2})(?!\d)");
        if (named.Success &&
            int.TryParse(named.Groups[1].Value, out var namedX) &&
            int.TryParse(named.Groups[2].Value, out var namedY) &&
            namedX > 0 &&
            namedY > 0 &&
            bitmap.Width % namedX == 0 &&
            bitmap.Height % namedY == 0)
        {
            return (namedX, namedY);
        }

        var candidates = new List<(int X, int Y, double Score)>();

        for (var x = 1; x <= 16; x++)
        {
            if (bitmap.Width % x != 0)
            {
                continue;
            }

            for (var y = 1; y <= 16; y++)
            {
                if (x * y <= 1 || x * y > 128 || bitmap.Height % y != 0)
                {
                    continue;
                }

                var frameWidth = bitmap.Width / x;
                var frameHeight = bitmap.Height / y;
                if (frameWidth < 8 || frameHeight < 8)
                {
                    continue;
                }

                var ratio = (double)frameWidth / frameHeight;
                var squarePenalty = Math.Abs(Math.Log(ratio)) * 40d;
                var preferredSizePenalty = new[] { 32, 48, 64, 96, 128, 192, 256 }
                    .Min(size => Math.Abs(frameWidth - size) + Math.Abs(frameHeight - size)) / 24d;
                var boundaryPenalty = MeasureBoundaryOpacity(bitmap, x, y) * 30d;
                var framePenalty = Math.Abs(x * y - 24) / 30d;

                candidates.Add((x, y, squarePenalty + preferredSizePenalty + boundaryPenalty + framePenalty));
            }
        }

        if (candidates.Count == 0)
        {
            return (1, 1);
        }

        var best = candidates.OrderBy(candidate => candidate.Score).First();
        return (best.X, best.Y);
    }

    private static double MeasureBoundaryOpacity(Bitmap bitmap, int xFrames, int yFrames)
    {
        var samples = 0;
        var alphaTotal = 0d;
        var stepX = Math.Max(1, bitmap.Width / 96);
        var stepY = Math.Max(1, bitmap.Height / 96);

        for (var x = 1; x < xFrames; x++)
        {
            var boundaryX = x * bitmap.Width / xFrames;
            for (var y = 0; y < bitmap.Height; y += stepY)
            {
                alphaTotal += bitmap.GetPixel(Math.Min(bitmap.Width - 1, boundaryX), y).A / 255d;
                samples++;
            }
        }

        for (var y = 1; y < yFrames; y++)
        {
            var boundaryY = y * bitmap.Height / yFrames;
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                alphaTotal += bitmap.GetPixel(x, Math.Min(bitmap.Height - 1, boundaryY)).A / 255d;
                samples++;
            }
        }

        return samples == 0 ? 0.5d : alphaTotal / samples;
    }

    private static string ClassifyAnimation(string name, Bitmap bitmap)
    {
        foreach (var pair in CategoryKeywords)
        {
            if (pair.Value.Any(keyword =>
                    name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return pair.Key;
            }
        }

        double saturationTotal = 0;
        double brightnessTotal = 0;
        double redScore = 0;
        double orangeScore = 0;
        double yellowScore = 0;
        double greenScore = 0;
        double cyanScore = 0;
        double blueScore = 0;
        double purpleScore = 0;
        var count = 0;

        var stepX = Math.Max(1, bitmap.Width / 80);
        var stepY = Math.Max(1, bitmap.Height / 80);

        for (var y = 0; y < bitmap.Height; y += stepY)
        {
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.A < 32)
                {
                    continue;
                }

                var hue = color.GetHue();
                var saturation = color.GetSaturation();
                var brightness = color.GetBrightness();
                var weight = Math.Max(0.05, saturation) * Math.Max(0.1, brightness);

                saturationTotal += saturation;
                brightnessTotal += brightness;
                count++;

                if (hue < 15 || hue >= 345) redScore += weight;
                else if (hue < 48) orangeScore += weight;
                else if (hue < 72) yellowScore += weight;
                else if (hue < 165) greenScore += weight;
                else if (hue < 200) cyanScore += weight;
                else if (hue < 265) blueScore += weight;
                else if (hue < 345) purpleScore += weight;
            }
        }

        if (count == 0)
        {
            return "Misc";
        }

        var avgSaturation = saturationTotal / count;
        var avgBrightness = brightnessTotal / count;

        if (avgBrightness < 0.24)
        {
            return "Dark";
        }

        if (avgSaturation < 0.16)
        {
            return avgBrightness > 0.72 ? "Holy" : "Smoke";
        }

        var scores = new Dictionary<string, double>
        {
            ["Fire"] = orangeScore + redScore * 0.55,
            ["Blood"] = redScore,
            ["Holy"] = yellowScore,
            ["Nature"] = greenScore,
            ["Water"] = cyanScore + blueScore * 0.65,
            ["Ice"] = cyanScore * 0.75 + blueScore,
            ["Magic"] = purpleScore,
        };

        var winner = scores.OrderByDescending(pair => pair.Value).First();
        if (winner.Value <= 0.01)
        {
            return "Misc";
        }

        if (winner.Key == "Holy" && avgBrightness < 0.58)
        {
            return "Lightning";
        }

        if (winner.Key == "Nature" && avgBrightness < 0.40)
        {
            return "Poison";
        }

        return winner.Key;
    }
}
