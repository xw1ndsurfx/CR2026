using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DarkUI.Forms;
using Intersect.Editor.Content;

namespace Intersect.Editor.Forms;

public sealed class FrmCharacterGenerator : DarkForm
{
    private enum CharacterAnimation
    {
        Move,
        Attack,
        Cast,
        Idle,
        Shoot,
        Weapon,
    }

    private sealed class PartFamily
    {
        public required string Name { get; init; }

        public Dictionary<CharacterAnimation, string> Files { get; } = new();

        public string? Resolve(CharacterAnimation animation)
        {
            if (Files.TryGetValue(animation, out var exact))
            {
                return exact;
            }

            if (Files.TryGetValue(CharacterAnimation.Move, out var move))
            {
                return move;
            }

            return Files.Values.FirstOrDefault();
        }
    }

    private sealed class PixelPreview : Control
    {
        private Bitmap? _image;

        public PixelPreview()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(17, 17, 17);
        }

        public void SetImage(Bitmap? image)
        {
            _image?.Dispose();
            _image = image;
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
            using var dark = new SolidBrush(Color.FromArgb(35, 35, 35));
            using var light = new SolidBrush(Color.FromArgb(50, 50, 50));

            for (var y = 0; y < Height; y += checker)
            {
                for (var x = 0; x < Width; x += checker)
                {
                    e.Graphics.FillRectangle(((x / checker + y / checker) & 1) == 0 ? dark : light, x, y, checker, checker);
                }
            }

            if (_image == null || _image.Width == 0 || _image.Height == 0)
            {
                return;
            }

            var scale = Math.Min((double)Width / _image.Width, (double)Height / _image.Height);
            scale = Math.Max(1d, Math.Floor(scale));
            if (_image.Width * scale > Width || _image.Height * scale > Height)
            {
                scale = Math.Min((double)Width / _image.Width, (double)Height / _image.Height);
            }

            var width = Math.Max(1, (int)Math.Round(_image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(_image.Height * scale));
            var xPos = (Width - width) / 2;
            var yPos = (Height - height) / 2;

            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(_image, new Rectangle(xPos, yPos, width, height));
        }
    }

    private static readonly string[] DefaultCategories =
    {
        "Artifact",
        "Base",
        "Beard",
        "Body",
        "Bow",
        "Bundles",
        "Cape",
        "Chest",
        "Feet",
        "Female",
        "FX",
        "Hair",
        "Hands",
        "Head",
        "Lines",
        "Offhand",
        "One Handed",
        "Overall",
        "Pants",
        "Quiver",
        "Rifle",
        "Skirt",
        "Staff",
        "Top",
    };

    private static readonly string[] PreferredLayerOrder =
    {
        "Base",
        "Body",
        "Female",
        "Pants",
        "Skirt",
        "Feet",
        "Top",
        "Chest",
        "Overall",
        "Hands",
        "Beard",
        "Hair",
        "Head",
        "Cape",
        "Quiver",
        "Offhand",
        "One Handed",
        "Staff",
        "Bow",
        "Rifle",
        "Artifact",
        "FX",
        "Lines",
        "Bundles",
    };

    private static readonly (CharacterAnimation Animation, string Suffix, string Label)[] AnimationDefinitions =
    {
        (CharacterAnimation.Move, string.Empty, "MOVE"),
        (CharacterAnimation.Attack, "_attack", "ATTACK"),
        (CharacterAnimation.Cast, "_cast", "CAST"),
        (CharacterAnimation.Idle, "_idle", "IDLE"),
        (CharacterAnimation.Shoot, "_shoot", "SHOOT"),
        (CharacterAnimation.Weapon, "_weapon", "WEAPON"),
    };

    private readonly string _gameRoot;
    private readonly string _charagenRoot;
    private readonly string _entitiesRoot;
    private readonly Action? _afterExport;

    private readonly ListBox _categoryList = new();
    private readonly ListBox _partsList = new();
    private readonly Label _categoryTitle = new();
    private readonly PixelPreview _preview = new();
    private readonly TextBox _exportName = new();
    private readonly Label _status = new();
    private readonly FlowLayoutPanel _animationButtons = new();
    private readonly Dictionary<CharacterAnimation, Button> _animationButtonLookup = new();

    private readonly Dictionary<string, List<PartFamily>> _partsByCategory =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _selectedPartByCategory =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 250 };

    private FileSystemWatcher? _watcher;
    private CharacterAnimation _previewAnimation = CharacterAnimation.Move;
    private bool _reloading;

    public FrmCharacterGenerator(Action? afterExport = null)
    {
        _afterExport = afterExport;
        _gameRoot = ResolveGameRoot();
        _charagenRoot = Path.Combine(_gameRoot, "charagen");
        _entitiesRoot = Path.Combine(_gameRoot, "resources", "entities");

        Text = "Corps Royaux Character Generator";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1280, 800);
        BackColor = Color.FromArgb(18, 18, 18);
        ForeColor = Color.Gainsboro;

        BuildInterface();
        EnsureFolders();

        _reloadTimer.Tick += (_, _) =>
        {
            _reloadTimer.Stop();
            ReloadAssets();
        };

        Shown += (_, _) =>
        {
            ReloadAssets();
            StartWatcher();
        };

        FormClosed += (_, _) => _watcher?.Dispose();
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

    private void EnsureFolders()
    {
        Directory.CreateDirectory(_charagenRoot);
        Directory.CreateDirectory(_entitiesRoot);

        foreach (var category in DefaultCategories)
        {
            Directory.CreateDirectory(Path.Combine(_charagenRoot, category));
        }
    }

    private void BuildInterface()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.FromArgb(247, 69, 96),
            Padding = new Padding(18, 8, 18, 8),
        };

        var title = new Label
        {
            AutoSize = true,
            Text = "CORPS ROYAUX CHARACTER CREATOR",
            Font = new Font(Font.FontFamily, 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(20, 20, 20),
            Location = new Point(16, 18),
        };
        header.Controls.Add(title);
        Controls.Add(header);

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 82,
            BackColor = Color.FromArgb(25, 22, 23),
            Padding = new Padding(14),
        };
        Controls.Add(footer);

        var exportLabel = new Label
        {
            AutoSize = true,
            Text = "Export name:",
            ForeColor = Color.Gainsboro,
            Location = new Point(18, 16),
        };
        footer.Controls.Add(exportLabel);

        _exportName.Location = new Point(110, 12);
        _exportName.Width = 300;
        _exportName.BackColor = Color.FromArgb(45, 38, 40);
        _exportName.ForeColor = Color.White;
        _exportName.BorderStyle = BorderStyle.FixedSingle;
        footer.Controls.Add(_exportName);

        var exportButton = CreateAccentButton("EXPORT 6 ANIMATIONS");
        exportButton.Location = new Point(425, 10);
        exportButton.Size = new Size(210, 34);
        exportButton.Click += (_, _) => ExportCharacter();
        footer.Controls.Add(exportButton);

        var refreshButton = CreateDarkButton("REFRESH CHARAGEN");
        refreshButton.Location = new Point(645, 10);
        refreshButton.Size = new Size(180, 34);
        refreshButton.Click += (_, _) => ReloadAssets();
        footer.Controls.Add(refreshButton);

        var openButton = CreateDarkButton("OPEN CHARAGEN FOLDER");
        openButton.Location = new Point(835, 10);
        openButton.Size = new Size(200, 34);
        openButton.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _charagenRoot,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Character Generator", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        footer.Controls.Add(openButton);

        _status.AutoSize = false;
        _status.Location = new Point(18, 50);
        _status.Size = new Size(1000, 20);
        _status.ForeColor = Color.Silver;
        footer.Controls.Add(_status);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(18, 18, 18),
            Padding = new Padding(12),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(body);
        body.BringToFront();
        header.BringToFront();
        footer.BringToFront();

        var categoriesPanel = CreateSection("CATEGORIES");
        _categoryList.Dock = DockStyle.Fill;
        StyleListBox(_categoryList);
        _categoryList.SelectedIndexChanged += (_, _) => PopulatePartsList();
        categoriesPanel.Controls.Add(_categoryList);
        body.Controls.Add(categoriesPanel, 0, 0);

        var partsPanel = CreateSection("PAPERDOLLS");
        _partsList.Dock = DockStyle.Fill;
        StyleListBox(_partsList);
        _partsList.SelectedIndexChanged += (_, _) => SelectCurrentPart();
        partsPanel.Controls.Add(_partsList);
        body.Controls.Add(partsPanel, 1, 0);

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 12, 12),
            Padding = new Padding(12),
        };

        _categoryTitle.Dock = DockStyle.Top;
        _categoryTitle.Height = 36;
        _categoryTitle.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
        _categoryTitle.ForeColor = Color.FromArgb(247, 69, 96);
        _categoryTitle.TextAlign = ContentAlignment.MiddleLeft;
        previewPanel.Controls.Add(_categoryTitle);

        _animationButtons.Dock = DockStyle.Top;
        _animationButtons.Height = 48;
        _animationButtons.FlowDirection = FlowDirection.LeftToRight;
        _animationButtons.WrapContents = false;
        _animationButtons.BackColor = Color.FromArgb(12, 12, 12);
        previewPanel.Controls.Add(_animationButtons);

        foreach (var definition in AnimationDefinitions)
        {
            var animation = definition.Animation;
            var button = CreateDarkButton(definition.Label);
            button.Width = 92;
            button.Height = 32;
            button.Margin = new Padding(0, 6, 6, 6);
            button.Click += (_, _) =>
            {
                _previewAnimation = animation;
                UpdateAnimationButtonState();
                DrawPreview();
            };
            _animationButtons.Controls.Add(button);
            _animationButtonLookup[animation] = button;
        }

        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.FromArgb(12, 12, 12),
        };
        _preview.Dock = DockStyle.Fill;
        previewHost.Controls.Add(_preview);
        previewPanel.Controls.Add(previewHost);

        var resetButton = CreateAccentButton("RESET CHARACTER");
        resetButton.Dock = DockStyle.Bottom;
        resetButton.Height = 42;
        resetButton.Click += (_, _) =>
        {
            _selectedPartByCategory.Clear();
            PopulatePartsList();
            DrawPreview();
        };
        previewPanel.Controls.Add(resetButton);

        body.Controls.Add(previewPanel, 2, 0);
        UpdateAnimationButtonState();
    }

    private static Panel CreateSection(string title)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 24, 25),
            Margin = new Padding(4),
            Padding = new Padding(8, 40, 8, 8),
        };

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.FromArgb(247, 69, 96),
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(label);
        label.BringToFront();
        return panel;
    }

    private static void StyleListBox(ListBox list)
    {
        list.BackColor = Color.FromArgb(38, 32, 34);
        list.ForeColor = Color.Gainsboro;
        list.BorderStyle = BorderStyle.None;
        list.IntegralHeight = false;
        list.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10);
    }

    private static Button CreateAccentButton(string text)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(247, 69, 96),
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = Color.FromArgb(247, 69, 96) },
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
            BackColor = Color.FromArgb(55, 47, 49),
            ForeColor = Color.Gainsboro,
            FlatAppearance = { BorderColor = Color.FromArgb(90, 78, 81) },
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
    }

    private void UpdateAnimationButtonState()
    {
        foreach (var pair in _animationButtonLookup)
        {
            pair.Value.BackColor = pair.Key == _previewAnimation
                ? Color.FromArgb(247, 69, 96)
                : Color.FromArgb(55, 47, 49);
        }
    }

    private void StartWatcher()
    {
        _watcher?.Dispose();

        _watcher = new FileSystemWatcher(_charagenRoot, "*.png")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
        };

        FileSystemEventHandler changed = (_, _) => ScheduleReload();
        RenamedEventHandler renamed = (_, _) => ScheduleReload();

        _watcher.Created += changed;
        _watcher.Changed += changed;
        _watcher.Deleted += changed;
        _watcher.Renamed += renamed;
    }

    private void ScheduleReload()
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke((MethodInvoker)(() =>
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        }));
    }

    private void ReloadAssets()
    {
        if (_reloading)
        {
            return;
        }

        _reloading = true;
        try
        {
            var selectedCategory = _categoryList.SelectedItem?.ToString();
            _partsByCategory.Clear();

            foreach (var directory in Directory.GetDirectories(_charagenRoot))
            {
                var category = Path.GetFileName(directory);
                _partsByCategory[category] = DiscoverFamilies(directory);
            }

            _categoryList.BeginUpdate();
            _categoryList.Items.Clear();
            foreach (var category in SortCategories(_partsByCategory.Keys))
            {
                _categoryList.Items.Add(category);
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

            _status.Text = $"Watching {_charagenRoot} — {_partsByCategory.Values.Sum(parts => parts.Count)} paperdoll sets detected.";
            DrawPreview();
        }
        catch (Exception ex)
        {
            _status.Text = "Unable to refresh charagen: " + ex.Message;
        }
        finally
        {
            _reloading = false;
        }
    }

    private static IEnumerable<string> SortCategories(IEnumerable<string> categories)
    {
        var order = PreferredLayerOrder
            .Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);

        return categories
            .OrderBy(name => order.TryGetValue(name, out var index) ? index : int.MaxValue)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    private static List<PartFamily> DiscoverFamilies(string categoryDirectory)
    {
        var groups = new Dictionary<string, PartFamily>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(categoryDirectory, "*.png", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(categoryDirectory, file);
            var animation = DetectAnimation(relative, out var cleanName);
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                cleanName = Path.GetFileNameWithoutExtension(file);
            }

            if (!groups.TryGetValue(cleanName, out var family))
            {
                family = new PartFamily { Name = cleanName };
                groups[cleanName] = family;
            }

            family.Files[animation] = file;
        }

        return groups.Values
            .OrderBy(part => part.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CharacterAnimation DetectAnimation(string relativePath, out string cleanName)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        var directorySegments = (Path.GetDirectoryName(relativePath) ?? string.Empty)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        foreach (var definition in AnimationDefinitions)
        {
            if (directorySegments.Any(segment => string.Equals(segment, definition.Label, StringComparison.OrdinalIgnoreCase)))
            {
                cleanName = fileName;
                return definition.Animation;
            }
        }

        foreach (var definition in AnimationDefinitions.Where(definition => definition.Animation != CharacterAnimation.Move))
        {
            var words = new[]
            {
                definition.Suffix,
                "-" + definition.Label.ToLowerInvariant(),
                " " + definition.Label.ToLowerInvariant(),
            };

            foreach (var word in words)
            {
                if (fileName.EndsWith(word, StringComparison.OrdinalIgnoreCase))
                {
                    cleanName = fileName[..^word.Length].TrimEnd('_', '-', ' ');
                    return definition.Animation;
                }
            }
        }

        if (fileName.EndsWith("_move", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = fileName[..^5].TrimEnd('_', '-', ' ');
            return CharacterAnimation.Move;
        }

        cleanName = fileName;
        return CharacterAnimation.Move;
    }

    private void PopulatePartsList()
    {
        if (_reloading)
        {
            return;
        }

        var category = _categoryList.SelectedItem?.ToString();
        _categoryTitle.Text = category == null ? "PREVIEW" : $"PREVIEW — {category.ToUpperInvariant()}";

        _partsList.BeginUpdate();
        _partsList.Items.Clear();
        _partsList.Items.Add("None");

        if (category != null && _partsByCategory.TryGetValue(category, out var parts))
        {
            foreach (var part in parts)
            {
                _partsList.Items.Add(part.Name);
            }

            if (_selectedPartByCategory.TryGetValue(category, out var selected))
            {
                var index = _partsList.FindStringExact(selected);
                _partsList.SelectedIndex = index >= 0 ? index : 0;
            }
            else
            {
                _partsList.SelectedIndex = 0;
            }
        }
        else
        {
            _partsList.SelectedIndex = 0;
        }

        _partsList.EndUpdate();
    }

    private void SelectCurrentPart()
    {
        if (_reloading)
        {
            return;
        }

        var category = _categoryList.SelectedItem?.ToString();
        var part = _partsList.SelectedItem?.ToString();
        if (category == null || part == null)
        {
            return;
        }

        if (string.Equals(part, "None", StringComparison.OrdinalIgnoreCase))
        {
            _selectedPartByCategory.Remove(category);
        }
        else
        {
            _selectedPartByCategory[category] = part;
        }

        DrawPreview();
    }

    private void DrawPreview()
    {
        try
        {
            _preview.SetImage(RenderCharacter(_previewAnimation));
        }
        catch (Exception ex)
        {
            _status.Text = "Preview error: " + ex.Message;
        }
    }

    private Bitmap? RenderCharacter(CharacterAnimation animation)
    {
        var files = GetSelectedLayerFiles(animation).ToArray();
        if (files.Length == 0)
        {
            return null;
        }

        var layers = new List<Bitmap>();
        try
        {
            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    using var source = new Bitmap(file);
                    layers.Add(new Bitmap(source));
                }
            }

            if (layers.Count == 0)
            {
                return null;
            }

            var width = layers.Max(image => image.Width);
            var height = layers.Max(image => image.Height);
            var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(output);
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;

            foreach (var layer in layers)
            {
                var x = (width - layer.Width) / 2;
                var y = (height - layer.Height) / 2;
                graphics.DrawImageUnscaled(layer, x, y);
            }

            return output;
        }
        finally
        {
            foreach (var layer in layers)
            {
                layer.Dispose();
            }
        }
    }

    private IEnumerable<string> GetSelectedLayerFiles(CharacterAnimation animation)
    {
        foreach (var category in SortCategories(_selectedPartByCategory.Keys))
        {
            if (!_selectedPartByCategory.TryGetValue(category, out var selectedName) ||
                !_partsByCategory.TryGetValue(category, out var parts))
            {
                continue;
            }

            var family = parts.FirstOrDefault(part =>
                string.Equals(part.Name, selectedName, StringComparison.OrdinalIgnoreCase));

            var file = family?.Resolve(animation);
            if (!string.IsNullOrWhiteSpace(file))
            {
                yield return file;
            }
        }
    }

    private void ExportCharacter()
    {
        var name = Path.GetFileNameWithoutExtension(_exportName.Text.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Enter a character name before exporting.", "Character Generator",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "The character name contains invalid filename characters.", "Character Generator",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_selectedPartByCategory.Count == 0)
        {
            MessageBox.Show(this, "Select at least one paperdoll before exporting.", "Character Generator",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Directory.CreateDirectory(_entitiesRoot);
        var written = new List<string>();

        try
        {
            foreach (var definition in AnimationDefinitions)
            {
                using var bitmap = RenderCharacter(definition.Animation);
                if (bitmap == null)
                {
                    continue;
                }

                var fileName = name + definition.Suffix + ".png";
                var destination = Path.Combine(_entitiesRoot, fileName);
                var temporary = destination + ".tmp";

                bitmap.Save(temporary, ImageFormat.Png);
                File.Move(temporary, destination, true);
                written.Add(fileName);
            }

            if (written.Count != AnimationDefinitions.Length)
            {
                throw new InvalidOperationException(
                    $"Only {written.Count} of {AnimationDefinitions.Length} animations could be rendered.");
            }

            GameContentManager.ReloadEntityTextures();
            _afterExport?.Invoke();

            _status.Text = $"Exported: {string.Join(", ", written)}";
            MessageBox.Show(
                this,
                $"Character exported to resources\\entities.\n\n{string.Join("\n", written)}\n\nThe Game Editor entity lists were refreshed automatically.",
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Export failed: " + ex.Message, "Character Generator",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
