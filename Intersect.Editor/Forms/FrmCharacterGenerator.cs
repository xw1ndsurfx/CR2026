using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
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

    private enum LayerRule
    {
        Normal,
        AlwaysBehind,
        AlwaysFront,
        DirectionalWeapon,
        DirectionalOffhand,
        KeywordDriven,
    }

    private sealed class SelectedLayer
    {
        public required string Category { get; init; }

        public required string PartName { get; init; }

        public required string File { get; init; }

        public required Bitmap Bitmap { get; init; }
    }

    private sealed class PixelPreview : Control
    {
        private Bitmap? _image;

        public PixelPreview()
        {
            DoubleBuffered = true;
            BackColor = System.Drawing.Color.FromArgb(17, 17, 17);
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
            using var dark = new SolidBrush(System.Drawing.Color.FromArgb(35, 35, 35));
            using var light = new SolidBrush(System.Drawing.Color.FromArgb(50, 50, 50));

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

    private static readonly Dictionary<string, LayerRule> CategoryLayerRules =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Base"] = LayerRule.Normal,
            ["Body"] = LayerRule.Normal,
            ["Female"] = LayerRule.Normal,
            ["Pants"] = LayerRule.Normal,
            ["Skirt"] = LayerRule.Normal,
            ["Feet"] = LayerRule.Normal,
            ["Top"] = LayerRule.Normal,
            ["Chest"] = LayerRule.Normal,
            ["Overall"] = LayerRule.Normal,
            ["Hands"] = LayerRule.Normal,
            ["Beard"] = LayerRule.Normal,
            ["Hair"] = LayerRule.Normal,
            ["Head"] = LayerRule.Normal,
            ["Lines"] = LayerRule.Normal,

            ["Cape"] = LayerRule.AlwaysBehind,
            ["Quiver"] = LayerRule.AlwaysBehind,

            ["One Handed"] = LayerRule.DirectionalWeapon,
            ["Staff"] = LayerRule.DirectionalWeapon,
            ["Bow"] = LayerRule.DirectionalWeapon,
            ["Rifle"] = LayerRule.DirectionalWeapon,
            ["Offhand"] = LayerRule.DirectionalOffhand,

            ["Artifact"] = LayerRule.KeywordDriven,
            ["FX"] = LayerRule.KeywordDriven,
            ["Bundles"] = LayerRule.KeywordDriven,
        };

    private static readonly string[] BehindKeywords =
    {
        "balloon",
        "ballon",
        "backpack",
        "bag",
        "back",
        "wings",
        "wing",
        "tail",
        "cape",
        "quiver",
    };

    private static readonly string[] FrontKeywords =
    {
        "front",
        "glow",
        "spark",
        "sparkle",
        "flare",
        "flash",
        "aura",
        "torch",
        "lantern",
        "orb",
    };

    private static readonly HashSet<string> AlwaysBehindCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Cape",
            "Quiver",
        };

    private static readonly string[] TopRowBehindPartKeywords =
    {
        "Balloon",
        "Ballon",
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

    private static readonly Dictionary<string, CharacterAnimation> ArtistAnimationCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mov"] = CharacterAnimation.Move,
            ["Mel"] = CharacterAnimation.Attack,
            ["Mag"] = CharacterAnimation.Cast,
            ["Idl"] = CharacterAnimation.Idle,
            ["Ran"] = CharacterAnimation.Shoot,
            ["Use"] = CharacterAnimation.Weapon,
        };

    private static readonly HashSet<string> ArtistIgnoredAnimationCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Blo",
            "Fis",
            "Rif",
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
        BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        ForeColor = System.Drawing.Color.Gainsboro;

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
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 82,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
            Padding = new Padding(14),
        };
        Controls.Add(footer);

        var exportLabel = new Label
        {
            AutoSize = true,
            Text = "Export name:",
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(18, 16),
        };
        footer.Controls.Add(exportLabel);

        _exportName.Location = new System.Drawing.Point(110, 12);
        _exportName.Width = 300;
        _exportName.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _exportName.ForeColor = System.Drawing.Color.White;
        _exportName.BorderStyle = BorderStyle.FixedSingle;
        footer.Controls.Add(_exportName);

        var exportButton = CreateAccentButton("EXPORT 6 ANIMATIONS");
        exportButton.Location = new System.Drawing.Point(425, 10);
        exportButton.Size = new Size(210, 34);
        exportButton.Click += (_, _) => ExportCharacter();
        footer.Controls.Add(exportButton);

        var refreshButton = CreateDarkButton("REFRESH CHARAGEN");
        refreshButton.Location = new System.Drawing.Point(645, 10);
        refreshButton.Size = new Size(180, 34);
        refreshButton.Click += (_, _) => ReloadAssets();
        footer.Controls.Add(refreshButton);

        var importButton = CreateDarkButton("IMPORT ARTIST ZIP");
        importButton.Location = new System.Drawing.Point(835, 10);
        importButton.Size = new Size(170, 34);
        importButton.Click += (_, _) => ImportArtistZip();
        footer.Controls.Add(importButton);

        var openButton = CreateDarkButton("OPEN CHARAGEN FOLDER");
        openButton.Location = new System.Drawing.Point(1015, 10);
        openButton.Size = new Size(190, 34);
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
        _status.Location = new System.Drawing.Point(18, 50);
        _status.Size = new Size(1000, 20);
        _status.ForeColor = System.Drawing.Color.Silver;
        footer.Controls.Add(_status);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
            Padding = new Padding(12),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(body);
        body.BringToFront();
        footer.BringToFront();

        var categoriesPanel = CreateSection("CATEGORIES");
        _categoryList.Dock = DockStyle.Fill;
        StyleListBox(_categoryList);
        _categoryList.SelectedIndexChanged += (_, _) => PopulatePartsList();
        categoriesPanel.Controls.Add(_categoryList, 0, 1);
        body.Controls.Add(categoriesPanel, 0, 0);

        var partsPanel = CreateSection("PAPERDOLLS");
        _partsList.Dock = DockStyle.Fill;
        StyleListBox(_partsList);
        _partsList.SelectedIndexChanged += (_, _) => SelectCurrentPart();
        partsPanel.Controls.Add(_partsList, 0, 1);
        body.Controls.Add(partsPanel, 1, 0);

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(12, 12, 12),
            Padding = new Padding(12),
        };

        _categoryTitle.Dock = DockStyle.Top;
        _categoryTitle.Height = 36;
        _categoryTitle.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
        _categoryTitle.ForeColor = System.Drawing.Color.FromArgb(247, 69, 96);
        _categoryTitle.TextAlign = ContentAlignment.MiddleLeft;
        previewPanel.Controls.Add(_categoryTitle);

        _animationButtons.Dock = DockStyle.Top;
        _animationButtons.Height = 48;
        _animationButtons.FlowDirection = FlowDirection.LeftToRight;
        _animationButtons.WrapContents = false;
        _animationButtons.BackColor = System.Drawing.Color.FromArgb(12, 12, 12);
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
            BackColor = System.Drawing.Color.FromArgb(12, 12, 12),
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

    private static TableLayoutPanel CreateSection(string title)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(28, 24, 25),
            Margin = new Padding(4),
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 2,
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
            Margin = Padding.Empty,
        };

        panel.Controls.Add(label, 0, 0);
        return panel;
    }

    private static void StyleListBox(ListBox list)
    {
        list.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        list.ForeColor = System.Drawing.Color.Gainsboro;
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

    private void UpdateAnimationButtonState()
    {
        foreach (var pair in _animationButtonLookup)
        {
            pair.Value.BackColor = pair.Key == _previewAnimation
                ? System.Drawing.Color.FromArgb(247, 69, 96)
                : System.Drawing.Color.FromArgb(55, 47, 49);
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

        if (TryParseArtistFileName(fileName, out var artistAnimation, out var artistName))
        {
            cleanName = artistName;
            return artistAnimation;
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

    private static bool TryParseArtistFileName(
        string fileName,
        out CharacterAnimation animation,
        out string cleanName
    )
    {
        animation = CharacterAnimation.Move;
        cleanName = fileName;

        var pieces = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 2)
        {
            return false;
        }

        var animationIndex = 0;
        string? genderPrefix = null;

        if (pieces.Length >= 3 &&
            (string.Equals(pieces[0], "M", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pieces[0], "F", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pieces[0], "B", StringComparison.OrdinalIgnoreCase)))
        {
            genderPrefix = pieces[0].ToUpperInvariant();
            animationIndex = 1;
        }

        var code = pieces[animationIndex];
        if (ArtistIgnoredAnimationCodes.Contains(code))
        {
            return false;
        }

        if (!ArtistAnimationCodes.TryGetValue(code, out animation))
        {
            return false;
        }

        var remainder = string.Join("_", pieces.Skip(animationIndex + 1));
        cleanName = string.IsNullOrWhiteSpace(genderPrefix)
            ? remainder
            : genderPrefix + "_" + remainder;

        return !string.IsNullOrWhiteSpace(cleanName);
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
        var selectedLayers = GetSelectedLayers(animation).ToArray();
        if (selectedLayers.Length == 0)
        {
            return null;
        }

        var layers = new List<SelectedLayer>();
        try
        {
            foreach (var selected in selectedLayers)
            {
                if (!File.Exists(selected.File))
                {
                    continue;
                }

                using var source = new Bitmap(selected.File);
                layers.Add(new SelectedLayer
                {
                    Category = selected.Category,
                    PartName = selected.PartName,
                    File = selected.File,
                    Bitmap = new Bitmap(source),
                });
            }

            if (layers.Count == 0)
            {
                return null;
            }

            var width = layers.Max(layer => layer.Bitmap.Width);
            var height = layers.Max(layer => layer.Bitmap.Height);
            var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(output);
            graphics.Clear(System.Drawing.Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;

            const int directionRows = 4;
            var rowHeight = Math.Max(1, height / directionRows);

            for (var row = 0; row < directionRows; row++)
            {
                var rowTop = row * rowHeight;
                var rowBottom = row == directionRows - 1 ? height : Math.Min(height, rowTop + rowHeight);
                graphics.SetClip(new Rectangle(0, rowTop, width, rowBottom - rowTop));

                // Draw layers that must sit behind the body for this direction first.
                foreach (var layer in layers.Where(layer => IsBehindCharacter(layer.Category, layer.PartName, row)))
                {
                    DrawLayer(graphics, layer.Bitmap, width, height);
                }

                // Then draw the normal stack, skipping anything already drawn behind for this row.
                foreach (var layer in layers.Where(layer => !IsBehindCharacter(layer.Category, layer.PartName, row)))
                {
                    DrawLayer(graphics, layer.Bitmap, width, height);
                }

                graphics.ResetClip();
            }

            return output;
        }
        finally
        {
            foreach (var layer in layers)
            {
                layer.Bitmap.Dispose();
            }
        }
    }

    private static void DrawLayer(Graphics graphics, Bitmap bitmap, int outputWidth, int outputHeight)
    {
        var x = (outputWidth - bitmap.Width) / 2;
        var y = (outputHeight - bitmap.Height) / 2;
        graphics.DrawImageUnscaled(bitmap, x, y);
    }

    private static bool IsBehindCharacter(string category, string partName, int directionRow)
    {
        var rule = CategoryLayerRules.TryGetValue(category, out var configuredRule)
            ? configuredRule
            : LayerRule.Normal;

        if (rule == LayerRule.AlwaysBehind || AlwaysBehindCategories.Contains(category))
        {
            return true;
        }

        if (rule == LayerRule.AlwaysFront)
        {
            return false;
        }

        if (rule == LayerRule.DirectionalWeapon)
        {
            return IsWeaponBehind(directionRow);
        }

        if (rule == LayerRule.DirectionalOffhand)
        {
            return IsOffhandBehind(directionRow);
        }

        if (rule == LayerRule.KeywordDriven)
        {
            if (FrontKeywords.Any(keyword =>
                partName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (BehindKeywords.Any(keyword =>
                partName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (directionRow == 0 &&
            TopRowBehindPartKeywords.Any(keyword =>
                partName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsWeaponBehind(int directionRow)
    {
        // Intersect-style sheets use four direction rows. We treat:
        // row 0 as back/up, rows 1-2 as side views, row 3 as front/down.
        // Back/up always goes behind the body. Side views are intentionally
        // asymmetric so the weapon can visually pass behind the torso on one side.
        return directionRow switch
        {
            0 => true,
            1 => true,
            2 => false,
            3 => false,
            _ => false,
        };
    }

    private static bool IsOffhandBehind(int directionRow)
    {
        // Shields/offhand items use the mirrored side-view behavior compared
        // with the main-hand weapon so the visible hand stays convincing.
        return directionRow switch
        {
            0 => true,
            1 => false,
            2 => true,
            3 => false,
            _ => false,
        };
    }

    private IEnumerable<(string Category, string PartName, string File)> GetSelectedLayers(
        CharacterAnimation animation
    )
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
                yield return (category, selectedName, file);
            }
        }
    }

    private void ImportArtistZip()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "ZIP archive (*.zip)|*.zip",
            Title = "Import Corps Royaux paperdolls",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var imported = 0;
        var skipped = 0;
        var invalid = 0;

        try
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
            }

            using var archive = ZipFile.OpenRead(dialog.FileName);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var pieces = entry.FullName
                    .Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries);

                var paperdollsIndex = Array.FindIndex(
                    pieces,
                    piece => string.Equals(piece, "Paperdolls", StringComparison.OrdinalIgnoreCase)
                );

                if (paperdollsIndex < 0 || paperdollsIndex + 2 >= pieces.Length)
                {
                    continue;
                }

                var category = pieces[paperdollsIndex + 1];
                var fileName = Path.GetFileName(pieces[^1]);

                if (string.IsNullOrWhiteSpace(category) ||
                    string.IsNullOrWhiteSpace(fileName) ||
                    !string.Equals(category, Path.GetFileName(category), StringComparison.Ordinal))
                {
                    invalid++;
                    continue;
                }

                var categoryDirectory = Path.Combine(_charagenRoot, category);
                Directory.CreateDirectory(categoryDirectory);

                var destination = Path.Combine(categoryDirectory, fileName);
                if (File.Exists(destination))
                {
                    skipped++;
                    continue;
                }

                entry.ExtractToFile(destination, overwrite: false);
                imported++;
            }

            ReloadAssets();

            _status.Text =
                $"Imported {imported:N0} PNGs from {Path.GetFileName(dialog.FileName)}. " +
                $"Skipped {skipped:N0} duplicates/existing files.";

            MessageBox.Show(
                this,
                $"Import complete.\n\nImported: {imported:N0}\nSkipped: {skipped:N0}\nInvalid: {invalid:N0}\n\n" +
                "The original artist filenames were preserved. The generator now recognizes Mov/Mel/Mag/Idl/Ran/Use automatically.",
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Import failed: " + ex.Message,
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        finally
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = true;
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
