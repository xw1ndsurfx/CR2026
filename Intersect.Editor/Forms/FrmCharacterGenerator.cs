using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;
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

    private enum CharacterGender
    {
        Male,
        Female,
    }

    public sealed class GeneratedItemRequest
    {
        public required string ItemName { get; init; }

        public required string IconFile { get; init; }

        public required string PaperdollFile { get; init; }

        public required string SourceCategory { get; init; }

        public required string SourcePartName { get; init; }

        public bool MaleCompatible { get; init; }

        public bool FemaleCompatible { get; init; }
    }

    private sealed class CharacterProject
    {
        public int Version { get; set; } = 1;

        public string Gender { get; set; } = CharacterGender.Male.ToString();

        public bool AdvancedMode { get; set; }

        public string ExportName { get; set; } = string.Empty;

        public string PreviewAnimation { get; set; } = CharacterAnimation.Move.ToString();

        public int PreviewDirection { get; set; }

        public Dictionary<string, string> Selections { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CharacterSnapshot
    {
        public CharacterGender Gender { get; init; }

        public bool AdvancedMode { get; init; }

        public string ExportName { get; init; } = string.Empty;

        public CharacterAnimation PreviewAnimation { get; init; }

        public int PreviewDirection { get; init; }

        public Dictionary<string, string> Selections { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PartFamily
    {
        public required string Name { get; init; }

        public Dictionary<CharacterAnimation, string> Files { get; } = new();

        // Several equipment packs use B_/F_ as Back/Front halves, not
        // Boy/Female variants. Keep those halves in the same selectable family.
        public Dictionary<CharacterAnimation, string> BackFiles { get; } = new();

        public Dictionary<CharacterAnimation, string> FrontFiles { get; } = new();

        public string? Resolve(CharacterAnimation animation)
        {
            return ResolveFrom(Files, animation);
        }

        public string? ResolveBack(CharacterAnimation animation)
        {
            return ResolveFrom(BackFiles, animation);
        }

        public string? ResolveFront(CharacterAnimation animation)
        {
            return ResolveFrom(FrontFiles, animation);
        }

        public string? ResolveAny(CharacterAnimation animation)
        {
            return Resolve(animation) ?? ResolveFront(animation) ?? ResolveBack(animation);
        }

        public bool Has(CharacterAnimation animation)
        {
            return Files.ContainsKey(animation) ||
                   BackFiles.ContainsKey(animation) ||
                   FrontFiles.ContainsKey(animation);
        }

        private static string? ResolveFrom(
            Dictionary<CharacterAnimation, string> files,
            CharacterAnimation animation
        )
        {
            if (files.TryGetValue(animation, out var exact))
            {
                return exact;
            }

            if (files.TryGetValue(CharacterAnimation.Move, out var move))
            {
                return move;
            }

            return files.Values.FirstOrDefault();
        }
    }

    private enum LayerRule
    {
        Normal,
        AlwaysBehind,
        AlwaysFront,
        DirectionalWeapon,
        DirectionalOffhand,
        DirectionalBackAccessory,
        KeywordDriven,
    }

    private enum PaperdollDepth
    {
        Back,
        Normal,
        Front,
    }

    private sealed class SelectedLayer
    {
        public required string Category { get; init; }

        public required string PartName { get; init; }

        public required string File { get; init; }

        public required PaperdollDepth Depth { get; init; }

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

    private static readonly HashSet<string> SplitDepthCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Artifact",
            "Bow",
            "Cape",
            "FX",
            "Offhand",
            "One Handed",
            "Quiver",
            "Rifle",
            "Staff",
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

            ["Cape"] = LayerRule.DirectionalBackAccessory,
            ["Quiver"] = LayerRule.DirectionalBackAccessory,

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

    private static readonly Dictionary<int, string[]> ServerPaperdollOrder =
        new()
        {
            // Artist/Intersect sheet row order: 0=Down, 1=Left, 2=Right, 3=Up.
            [0] = new[]
            {
                "Bag", "Cape", "Player", "Earring", "Armor", "Legs", "Belt", "Head",
                "Gloves", "Ring", "Shield", "Weapon", "Boots", "Necklace", "Tag", "Ship",
            },
            [1] = new[]
            {
                "Shield", "Player", "Earring", "Bag", "Cape", "Armor", "Belt", "Head",
                "Weapon", "Gloves", "Ring", "Legs", "Boots", "Necklace", "Tag", "Ship",
            },
            [2] = new[]
            {
                "Shield", "Player", "Earring", "Bag", "Cape", "Armor", "Belt", "Head",
                "Weapon", "Gloves", "Ring", "Legs", "Boots", "Necklace", "Tag", "Ship",
            },
            [3] = new[]
            {
                "Shield", "Weapon", "Player", "Earring", "Armor", "Belt", "Head", "Gloves",
                "Ring", "Legs", "Boots", "Bag", "Cape", "Necklace", "Tag", "Ship",
            },
        };

    private static readonly Dictionary<string, string> CategoryToPaperdollSlot =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Base"] = "Player",
            ["Body"] = "Player",
            ["Female"] = "Player",
            ["Lines"] = "Player",

            ["Top"] = "Armor",
            ["Chest"] = "Armor",
            ["Overall"] = "Armor",

            ["Pants"] = "Legs",
            ["Skirt"] = "Legs",

            ["Feet"] = "Boots",
            ["Hands"] = "Gloves",

            ["Hair"] = "Head",
            ["Beard"] = "Head",
            ["Head"] = "Head",

            ["Cape"] = "Cape",
            ["Quiver"] = "Bag",

            ["Offhand"] = "Shield",

            ["One Handed"] = "Weapon",
            ["Staff"] = "Weapon",
            ["Bow"] = "Weapon",
            ["Rifle"] = "Weapon",

            ["Artifact"] = "Tag",
            ["FX"] = "Tag",
            ["Bundles"] = "Ship",
        };

    private static readonly HashSet<string> SimpleVisibleCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Base",
            "Body",
            "Hair",
            "Beard",
            "Head",
            "Top",
            "Chest",
            "Overall",
            "Pants",
            "Skirt",
            "Feet",
            "Hands",
            "Cape",
            "Quiver",
            "Offhand",
            "One Handed",
            "Staff",
            "Bow",
            "Rifle",
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
    private readonly string _paperdollsRoot;
    private readonly string _itemsRoot;
    private readonly Action? _afterExport;
    private readonly Action<GeneratedItemRequest>? _afterPaperdollExport;

    private readonly ListBox _categoryList = new();
    private readonly ListView _partsView = new();
    private readonly ImageList _partImages = new();
    private readonly TextBox _partSearch = new();
    private readonly Button _favoritePartButton = new();
    private readonly Button _favoritesOnlyButton = new();
    private readonly Button _undoButton = new();
    private readonly Button _redoButton = new();
    private readonly Label _categoryTitle = new();
    private readonly PixelPreview _preview = new();
    private readonly TextBox _exportName = new();
    private readonly TextBox _paperdollExportName = new();
    private readonly TextBox _itemExportName = new();
    private readonly CheckBox _createItemAfterPaperdollExport = new();
    private readonly Label _status = new();
    private readonly FlowLayoutPanel _animationButtons = new();
    private readonly Dictionary<CharacterAnimation, Button> _animationButtonLookup = new();
    private readonly Button _maleButton = new();
    private readonly Button _femaleButton = new();
    private readonly Button _advancedModeButton = new();
    private readonly FlowLayoutPanel _directionButtons = new();
    private readonly Dictionary<int, Button> _directionButtonLookup = new();
    private readonly Random _random = new();

    private readonly Dictionary<string, List<PartFamily>> _partsByCategory =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _selectedPartByCategory =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _favoriteParts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Stack<CharacterSnapshot> _undoHistory = new();
    private readonly Stack<CharacterSnapshot> _redoHistory = new();

    private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 140 };

    private FileSystemWatcher? _watcher;
    private Bitmap? _previewSheet;
    private CharacterAnimation _previewAnimation = CharacterAnimation.Move;
    private CharacterGender _selectedGender = CharacterGender.Male;
    private int _previewDirection;
    private int _previewFrame;
    private bool _advancedMode;
    private bool _favoritesOnly;
    private bool _populatingParts;
    private bool _restoringHistory;
    private bool _reloading;

    public FrmCharacterGenerator(
        Action? afterExport = null,
        Action<GeneratedItemRequest>? afterPaperdollExport = null
    )
    {
        _afterExport = afterExport;
        _afterPaperdollExport = afterPaperdollExport;
        _gameRoot = ResolveGameRoot();
        _charagenRoot = Path.Combine(_gameRoot, "charagen");
        _entitiesRoot = Path.Combine(_gameRoot, "resources", "entities");
        _paperdollsRoot = Path.Combine(_gameRoot, "resources", "paperdolls");
        _itemsRoot = Path.Combine(_gameRoot, "resources", "items");

        Text = "Corps Royaux Character Generator";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1280, 800);
        BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        ForeColor = System.Drawing.Color.Gainsboro;

        BuildInterface();
        EnsureFolders();
        LoadFavorites();

        _reloadTimer.Tick += (_, _) =>
        {
            _reloadTimer.Stop();
            ReloadAssets();
        };

        _previewTimer.Tick += (_, _) =>
        {
            AdvancePreviewFrame();
        };

        Shown += (_, _) =>
        {
            ReloadAssets();
            StartWatcher();
            _previewTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            _previewTimer.Stop();
            _watcher?.Dispose();
            _previewSheet?.Dispose();
            _partImages.Dispose();
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

    private void EnsureFolders()
    {
        Directory.CreateDirectory(_charagenRoot);
        Directory.CreateDirectory(_entitiesRoot);
        Directory.CreateDirectory(_paperdollsRoot);
        Directory.CreateDirectory(_itemsRoot);

        foreach (var category in DefaultCategories)
        {
            Directory.CreateDirectory(Path.Combine(_charagenRoot, category));
        }
    }

    private void BuildInterface()
    {
        // Use a single root layout so the toolbars reserve real space instead
        // of floating over the center content via WinForms docking/z-order.
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        Controls.Add(rootLayout);

        var topTools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
            Padding = Padding.Empty,
        };
        topTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        topTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var simpleBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
            Padding = new Padding(12, 8, 12, 8),
            Margin = Padding.Empty,
        };

        var presetBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = System.Drawing.Color.FromArgb(32, 28, 29),
            Padding = new Padding(12, 6, 12, 6),
            Margin = Padding.Empty,
        };

        ConfigureGenderButton(_maleButton, "MALE", CharacterGender.Male);
        ConfigureGenderButton(_femaleButton, "FEMALE", CharacterGender.Female);

        var randomizeButton = CreateAccentButton("RANDOMIZE CHARACTER");
        randomizeButton.Size = new Size(190, 34);
        randomizeButton.Margin = new Padding(8, 0, 0, 0);
        randomizeButton.Click += (_, _) => RandomizeCharacter();

        _advancedModeButton.Text = "SIMPLE MODE";
        _advancedModeButton.Size = new Size(140, 34);
        _advancedModeButton.Margin = new Padding(8, 0, 0, 0);
        _advancedModeButton.FlatStyle = FlatStyle.Flat;
        _advancedModeButton.ForeColor = System.Drawing.Color.White;
        _advancedModeButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _advancedModeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(90, 78, 81);
        _advancedModeButton.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold);
        _advancedModeButton.Cursor = Cursors.Hand;
        _advancedModeButton.Click += (_, _) => ToggleAdvancedMode();

        var faceButton = CreateDarkButton("FACE & HAIR");
        faceButton.Size = new Size(118, 34);
        faceButton.Margin = new Padding(8, 0, 0, 0);
        faceButton.Click += (_, _) => RandomizeFaceAndHair();

        var clothesButton = CreateDarkButton("CLOTHES");
        clothesButton.Size = new Size(110, 34);
        clothesButton.Margin = new Padding(8, 0, 0, 0);
        clothesButton.Click += (_, _) => RandomizeClothes();

        var equipmentButton = CreateDarkButton("EQUIPMENT");
        equipmentButton.Size = new Size(120, 34);
        equipmentButton.Margin = new Padding(8, 0, 0, 0);
        equipmentButton.Click += (_, _) => RandomizeEquipment();

        var clearGearButton = CreateDarkButton("CLEAR GEAR");
        clearGearButton.Size = new Size(110, 34);
        clearGearButton.Margin = new Padding(8, 0, 0, 0);
        clearGearButton.Click += (_, _) => ClearEquipment();

        simpleBar.Controls.Add(_maleButton);
        simpleBar.Controls.Add(_femaleButton);
        simpleBar.Controls.Add(randomizeButton);
        simpleBar.Controls.Add(_advancedModeButton);
        simpleBar.Controls.Add(faceButton);
        simpleBar.Controls.Add(clothesButton);
        simpleBar.Controls.Add(equipmentButton);
        simpleBar.Controls.Add(clearGearButton);

        var presetLabel = new Label
        {
            AutoSize = false,
            Text = "PRESETS",
            Width = 92,
            Height = 32,
            ForeColor = System.Drawing.Color.FromArgb(247, 69, 96),
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 0),
        };
        presetBar.Controls.Add(presetLabel);

        foreach (var preset in new[] { "KNIGHT", "ARCHER", "MAGE", "PIRATE", "CIVILIAN" })
        {
            var presetName = preset;
            var button = CreateDarkButton(presetName);
            button.Size = new Size(112, 32);
            button.Margin = new Padding(0, 0, 8, 0);
            button.Click += (_, _) => ApplyPreset(presetName);
            presetBar.Controls.Add(button);
        }

        var saveBuildButton = CreateDarkButton("SAVE BUILD");
        saveBuildButton.Size = new Size(112, 32);
        saveBuildButton.Margin = new Padding(8, 0, 0, 0);
        saveBuildButton.Click += (_, _) => SaveCharacterProject();
        presetBar.Controls.Add(saveBuildButton);

        var loadBuildButton = CreateDarkButton("LOAD BUILD");
        loadBuildButton.Size = new Size(112, 32);
        loadBuildButton.Margin = new Padding(0, 0, 8, 0);
        loadBuildButton.Click += (_, _) => LoadCharacterProject();
        presetBar.Controls.Add(loadBuildButton);

        _undoButton.Text = "↶ UNDO";
        _undoButton.Size = new Size(96, 32);
        _undoButton.Margin = new Padding(8, 0, 0, 0);
        _undoButton.FlatStyle = FlatStyle.Flat;
        _undoButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _undoButton.ForeColor = System.Drawing.Color.Gainsboro;
        _undoButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(90, 78, 81);
        _undoButton.Click += (_, _) => UndoCharacter();
        presetBar.Controls.Add(_undoButton);

        _redoButton.Text = "↷ REDO";
        _redoButton.Size = new Size(96, 32);
        _redoButton.Margin = new Padding(0, 0, 8, 0);
        _redoButton.FlatStyle = FlatStyle.Flat;
        _redoButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _redoButton.ForeColor = System.Drawing.Color.Gainsboro;
        _redoButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(90, 78, 81);
        _redoButton.Click += (_, _) => RedoCharacter();
        presetBar.Controls.Add(_redoButton);

        UpdateHistoryButtons();

        topTools.Controls.Add(simpleBar, 0, 0);
        topTools.Controls.Add(presetBar, 0, 1);
        rootLayout.Controls.Add(topTools, 0, 0);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
            Padding = new Padding(14),
        };
        rootLayout.Controls.Add(footer, 0, 2);

        var exportLabel = new Label
        {
            AutoSize = true,
            Text = "Character:",
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(18, 17),
        };
        footer.Controls.Add(exportLabel);

        _exportName.Location = new System.Drawing.Point(90, 12);
        _exportName.Width = 240;
        _exportName.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _exportName.ForeColor = System.Drawing.Color.White;
        _exportName.BorderStyle = BorderStyle.FixedSingle;
        footer.Controls.Add(_exportName);

        var exportButton = CreateAccentButton("EXPORT CHARACTER");
        exportButton.Location = new System.Drawing.Point(340, 10);
        exportButton.Size = new Size(185, 34);
        exportButton.Click += (_, _) => ExportCharacter();
        footer.Controls.Add(exportButton);

        var refreshButton = CreateDarkButton("REFRESH CHARAGEN");
        refreshButton.Location = new System.Drawing.Point(535, 10);
        refreshButton.Size = new Size(160, 34);
        refreshButton.Click += (_, _) => ReloadAssets();
        footer.Controls.Add(refreshButton);

        var importButton = CreateDarkButton("IMPORT ARTIST ZIP");
        importButton.Location = new System.Drawing.Point(705, 10);
        importButton.Size = new Size(160, 34);
        importButton.Click += (_, _) => ImportArtistZip();
        footer.Controls.Add(importButton);

        var openButton = CreateDarkButton("OPEN CHARAGEN FOLDER");
        openButton.Location = new System.Drawing.Point(875, 10);
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

        var paperdollLabel = new Label
        {
            AutoSize = true,
            Text = "Paperdoll:",
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(18, 57),
        };
        footer.Controls.Add(paperdollLabel);

        _paperdollExportName.Location = new System.Drawing.Point(90, 52);
        _paperdollExportName.Width = 205;
        _paperdollExportName.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _paperdollExportName.ForeColor = System.Drawing.Color.White;
        _paperdollExportName.BorderStyle = BorderStyle.FixedSingle;
        footer.Controls.Add(_paperdollExportName);

        var itemLabel = new Label
        {
            AutoSize = true,
            Text = "Item:",
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(310, 57),
        };
        footer.Controls.Add(itemLabel);

        _itemExportName.Location = new System.Drawing.Point(350, 52);
        _itemExportName.Width = 205;
        _itemExportName.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _itemExportName.ForeColor = System.Drawing.Color.White;
        _itemExportName.BorderStyle = BorderStyle.FixedSingle;
        footer.Controls.Add(_itemExportName);

        _createItemAfterPaperdollExport.Text = "Create in Item Editor";
        _createItemAfterPaperdollExport.Checked = true;
        _createItemAfterPaperdollExport.AutoSize = true;
        _createItemAfterPaperdollExport.ForeColor = System.Drawing.Color.Gainsboro;
        _createItemAfterPaperdollExport.Location = new System.Drawing.Point(570, 56);
        footer.Controls.Add(_createItemAfterPaperdollExport);

        var exportPaperdollButton = CreateAccentButton("EXPORT PAPERDOLL + ITEM");
        exportPaperdollButton.Location = new System.Drawing.Point(735, 50);
        exportPaperdollButton.Size = new Size(230, 34);
        exportPaperdollButton.Click += (_, _) => ExportSelectedPaperdoll();
        footer.Controls.Add(exportPaperdollButton);

        _status.AutoSize = false;
        _status.Location = new System.Drawing.Point(18, 94);
        _status.Size = new Size(1180, 22);
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
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Margin = Padding.Empty;
        rootLayout.Controls.Add(body, 0, 1);

        var categoriesPanel = CreateSection("CATEGORIES");
        _categoryList.Dock = DockStyle.Fill;
        StyleListBox(_categoryList);
        _categoryList.SelectedIndexChanged += (_, _) =>
        {
            PopulatePartsList();
            UpdateFavoriteButtonState();
        };
        categoriesPanel.Controls.Add(_categoryList, 0, 1);
        body.Controls.Add(categoriesPanel, 0, 0);

        var partsPanel = CreateSection("PAPERDOLLS");
        ConfigurePartThumbnailView();
        _partsView.SelectedIndexChanged += (_, _) =>
        {
            SelectCurrentPart();
            UpdateFavoriteButtonState();
        };

        var partsHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(38, 32, 34),
        };

        var partsToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = System.Drawing.Color.FromArgb(32, 28, 29),
            Padding = new Padding(4, 4, 4, 2),
        };

        _partSearch.Width = 160;
        _partSearch.Height = 28;
        _partSearch.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _partSearch.ForeColor = System.Drawing.Color.White;
        _partSearch.BorderStyle = BorderStyle.FixedSingle;
        _partSearch.PlaceholderText = "Search paperdolls...";
        _partSearch.TextChanged += (_, _) => PopulatePartsList();

        _favoritePartButton.Text = "☆ FAVORITE";
        _favoritePartButton.Size = new Size(102, 28);
        _favoritePartButton.Margin = new Padding(4, 0, 0, 0);
        _favoritePartButton.FlatStyle = FlatStyle.Flat;
        _favoritePartButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _favoritePartButton.ForeColor = System.Drawing.Color.Gainsboro;
        _favoritePartButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(90, 78, 81);
        _favoritePartButton.Click += (_, _) => ToggleSelectedFavorite();

        _favoritesOnlyButton.Text = "★ ONLY";
        _favoritesOnlyButton.Size = new Size(82, 28);
        _favoritesOnlyButton.Margin = new Padding(4, 0, 0, 0);
        _favoritesOnlyButton.FlatStyle = FlatStyle.Flat;
        _favoritesOnlyButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _favoritesOnlyButton.ForeColor = System.Drawing.Color.Gainsboro;
        _favoritesOnlyButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(90, 78, 81);
        _favoritesOnlyButton.Click += (_, _) =>
        {
            _favoritesOnly = !_favoritesOnly;
            UpdateFavoritesOnlyButtonState();
            PopulatePartsList();
        };

        partsToolbar.Controls.Add(_partSearch);
        partsToolbar.Controls.Add(_favoritePartButton);
        partsToolbar.Controls.Add(_favoritesOnlyButton);

        partsHost.Controls.Add(_partsView);
        partsHost.Controls.Add(partsToolbar);
        partsToolbar.BringToFront();

        partsPanel.Controls.Add(partsHost, 0, 1);
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
                _previewFrame = 0;
                UpdateAnimationButtonState();
                DrawPreview();
            };
            _animationButtons.Controls.Add(button);
            _animationButtonLookup[animation] = button;
        }

        _directionButtons.Dock = DockStyle.Top;
        _directionButtons.Height = 42;
        _directionButtons.FlowDirection = FlowDirection.LeftToRight;
        _directionButtons.WrapContents = false;
        _directionButtons.BackColor = System.Drawing.Color.FromArgb(12, 12, 12);
        previewPanel.Controls.Add(_directionButtons);

        var directionDefinitions = new[]
        {
            (Row: 0, Label: "↓ DOWN"),
            (Row: 1, Label: "← LEFT"),
            (Row: 2, Label: "→ RIGHT"),
            (Row: 3, Label: "↑ UP"),
        };

        foreach (var definition in directionDefinitions)
        {
            var row = definition.Row;
            var button = CreateDarkButton(definition.Label);
            button.Width = 90;
            button.Height = 30;
            button.Margin = new Padding(0, 4, 6, 4);
            button.Click += (_, _) =>
            {
                _previewDirection = row;
                _previewFrame = 0;
                UpdateDirectionButtonState();
                ShowPreviewFrame();
            };
            _directionButtons.Controls.Add(button);
            _directionButtonLookup[row] = button;
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
            RememberState();
            _selectedPartByCategory.Clear();
            _previewFrame = 0;
            PopulatePartsList();
            DrawPreview();
        };
        previewPanel.Controls.Add(resetButton);

        body.Controls.Add(previewPanel, 2, 0);
        UpdateAnimationButtonState();
        UpdateDirectionButtonState();
        UpdateFavoritesOnlyButtonState();
        UpdateFavoriteButtonState();
        UpdateHistoryButtons();
    }

    private CharacterSnapshot CaptureSnapshot()
    {
        return new CharacterSnapshot
        {
            Gender = _selectedGender,
            AdvancedMode = _advancedMode,
            ExportName = _exportName.Text,
            PreviewAnimation = _previewAnimation,
            PreviewDirection = _previewDirection,
            Selections = new Dictionary<string, string>(
                _selectedPartByCategory,
                StringComparer.OrdinalIgnoreCase
            ),
        };
    }

    private void RememberState()
    {
        if (_restoringHistory)
        {
            return;
        }

        _undoHistory.Push(CaptureSnapshot());
        _redoHistory.Clear();

        // Keep memory bounded during long editing sessions.
        if (_undoHistory.Count > 50)
        {
            var recent = _undoHistory.Reverse().Skip(1).ToArray();
            _undoHistory.Clear();
            foreach (var snapshot in recent)
            {
                _undoHistory.Push(snapshot);
            }
        }

        UpdateHistoryButtons();
    }

    private void UndoCharacter()
    {
        if (_undoHistory.Count == 0)
        {
            return;
        }

        _redoHistory.Push(CaptureSnapshot());
        ApplySnapshot(_undoHistory.Pop());
        UpdateHistoryButtons();
    }

    private void RedoCharacter()
    {
        if (_redoHistory.Count == 0)
        {
            return;
        }

        _undoHistory.Push(CaptureSnapshot());
        ApplySnapshot(_redoHistory.Pop());
        UpdateHistoryButtons();
    }

    private void ApplySnapshot(CharacterSnapshot snapshot)
    {
        _restoringHistory = true;
        try
        {
            _selectedGender = snapshot.Gender;
            _previewAnimation = snapshot.PreviewAnimation;
            _previewDirection = Math.Clamp(snapshot.PreviewDirection, 0, 3);
            _exportName.Text = snapshot.ExportName;

            _selectedPartByCategory.Clear();
            foreach (var pair in snapshot.Selections)
            {
                if (_partsByCategory.TryGetValue(pair.Key, out var parts) &&
                    parts.Any(part =>
                        string.Equals(part.Name, pair.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedPartByCategory[pair.Key] = pair.Value;
                }
            }

            UpdateGenderButtonState();
            SetAdvancedMode(snapshot.AdvancedMode);
            UpdateAnimationButtonState();
            UpdateDirectionButtonState();
            PopulatePartsList();
            DrawPreview();
        }
        finally
        {
            _restoringHistory = false;
        }
    }

    private void UpdateHistoryButtons()
    {
        _undoButton.Enabled = _undoHistory.Count > 0;
        _redoButton.Enabled = _redoHistory.Count > 0;
    }

    private string FavoritesFilePath =>
        Path.Combine(_charagenRoot, "favorites.json");

    private static string MakeFavoriteKey(string category, string partName)
    {
        return category + "::" + partName;
    }

    private void LoadFavorites()
    {
        _favoriteParts.Clear();

        try
        {
            if (!File.Exists(FavoritesFilePath))
            {
                return;
            }

            var json = File.ReadAllText(FavoritesFilePath);
            var favorites = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            foreach (var favorite in favorites.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                _favoriteParts.Add(favorite);
            }
        }
        catch
        {
            // Favorites are optional metadata; never block the generator if the
            // file is missing, old or malformed.
        }
    }

    private void SaveFavorites()
    {
        try
        {
            Directory.CreateDirectory(_charagenRoot);
            File.WriteAllText(
                FavoritesFilePath,
                JsonSerializer.Serialize(
                    _favoriteParts.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                    new JsonSerializerOptions { WriteIndented = true }
                )
            );
        }
        catch (Exception ex)
        {
            _status.Text = "Unable to save favorites: " + ex.Message;
        }
    }

    private void ToggleSelectedFavorite()
    {
        if (_partsView.SelectedItems.Count == 0)
        {
            return;
        }

        var category = _categoryList.SelectedItem?.ToString();
        var partName = _partsView.SelectedItems[0].Tag?.ToString();
        if (string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(partName) ||
            string.Equals(partName, "None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var key = MakeFavoriteKey(category, partName);
        if (!_favoriteParts.Add(key))
        {
            _favoriteParts.Remove(key);
        }

        SaveFavorites();
        PopulatePartsList();
        UpdateFavoriteButtonState();
    }

    private void UpdateFavoriteButtonState()
    {
        var category = _categoryList.SelectedItem?.ToString();
        var partName = _partsView.SelectedItems.Count > 0
            ? _partsView.SelectedItems[0].Tag?.ToString()
            : null;

        var canFavorite =
            !string.IsNullOrWhiteSpace(category) &&
            !string.IsNullOrWhiteSpace(partName) &&
            !string.Equals(partName, "None", StringComparison.OrdinalIgnoreCase);

        _favoritePartButton.Enabled = canFavorite;

        var isFavorite = canFavorite &&
            _favoriteParts.Contains(MakeFavoriteKey(category!, partName!));

        _favoritePartButton.Text = isFavorite ? "★ FAVORITE" : "☆ FAVORITE";
        _favoritePartButton.BackColor = isFavorite
            ? System.Drawing.Color.FromArgb(247, 69, 96)
            : System.Drawing.Color.FromArgb(55, 47, 49);
        _favoritePartButton.FlatAppearance.BorderColor = _favoritePartButton.BackColor;
    }

    private void UpdateFavoritesOnlyButtonState()
    {
        _favoritesOnlyButton.BackColor = _favoritesOnly
            ? System.Drawing.Color.FromArgb(247, 69, 96)
            : System.Drawing.Color.FromArgb(55, 47, 49);
        _favoritesOnlyButton.FlatAppearance.BorderColor = _favoritesOnlyButton.BackColor;
    }

    private void ConfigurePartThumbnailView()
    {
        _partImages.ImageSize = new Size(96, 96);
        _partImages.ColorDepth = ColorDepth.Depth32Bit;

        _partsView.Dock = DockStyle.Fill;
        _partsView.View = View.LargeIcon;
        _partsView.LargeImageList = _partImages;
        _partsView.MultiSelect = false;
        _partsView.HideSelection = false;
        _partsView.ShowItemToolTips = true;
        _partsView.BorderStyle = BorderStyle.None;
        _partsView.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        _partsView.ForeColor = System.Drawing.Color.Gainsboro;
        _partsView.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9);
        _partsView.TileSize = new Size(118, 124);
    }

    private void ToggleAdvancedMode()
    {
        RememberState();
        SetAdvancedMode(!_advancedMode);
    }

    private void SetAdvancedMode(bool advancedMode)
    {
        _advancedMode = advancedMode;
        _advancedModeButton.Text = _advancedMode ? "ADVANCED MODE" : "SIMPLE MODE";
        _advancedModeButton.BackColor = _advancedMode
            ? System.Drawing.Color.FromArgb(247, 69, 96)
            : System.Drawing.Color.FromArgb(55, 47, 49);
        _advancedModeButton.FlatAppearance.BorderColor = _advancedModeButton.BackColor;
        ReloadCategoryList();
    }

    private void ReloadCategoryList()
    {
        var selectedCategory = _categoryList.SelectedItem?.ToString();

        _categoryList.BeginUpdate();
        _categoryList.Items.Clear();

        var categories = SortCategories(_partsByCategory.Keys);
        if (!_advancedMode)
        {
            categories = categories.Where(SimpleVisibleCategories.Contains);
        }

        foreach (var category in categories)
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
    }

    private Bitmap CreatePartThumbnail(PartFamily part)
    {
        const int thumbSize = 96;
        var thumbnail = new Bitmap(thumbSize, thumbSize, PixelFormat.Format32bppArgb);

        var file =
            part.ResolveAny(CharacterAnimation.Move) ??
            part.Files.Values.FirstOrDefault() ??
            part.FrontFiles.Values.FirstOrDefault() ??
            part.BackFiles.Values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            return thumbnail;
        }

        using var sheet = new Bitmap(file);
        var frameHeight = Math.Max(1, sheet.Height / 4);
        var frameWidth = Math.Min(frameHeight, sheet.Width);
        var source = new Rectangle(0, 0, frameWidth, frameHeight);

        using var graphics = Graphics.FromImage(thumbnail);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;

        var scale = Math.Min((double)(thumbSize - 8) / frameWidth, (double)(thumbSize - 8) / frameHeight);
        var width = Math.Max(1, (int)Math.Round(frameWidth * scale));
        var height = Math.Max(1, (int)Math.Round(frameHeight * scale));
        var destination = new Rectangle((thumbSize - width) / 2, (thumbSize - height) / 2, width, height);

        graphics.DrawImage(sheet, destination, source, GraphicsUnit.Pixel);
        return thumbnail;
    }

    private void UpdateDirectionButtonState()
    {
        foreach (var pair in _directionButtonLookup)
        {
            pair.Value.BackColor = pair.Key == _previewDirection
                ? System.Drawing.Color.FromArgb(247, 69, 96)
                : System.Drawing.Color.FromArgb(55, 47, 49);
        }
    }

    private void AdvancePreviewFrame()
    {
        if (_previewSheet == null)
        {
            return;
        }

        var frameHeight = Math.Max(1, _previewSheet.Height / 4);
        var frameWidth = Math.Min(frameHeight, _previewSheet.Width);
        var frameCount = Math.Max(1, _previewSheet.Width / frameWidth);

        _previewFrame = (_previewFrame + 1) % frameCount;
        ShowPreviewFrame();
    }

    private void ShowPreviewFrame()
    {
        if (_previewSheet == null)
        {
            _preview.SetImage(null);
            return;
        }

        var frameHeight = Math.Max(1, _previewSheet.Height / 4);
        var frameWidth = Math.Min(frameHeight, _previewSheet.Width);
        var frameCount = Math.Max(1, _previewSheet.Width / frameWidth);
        var frame = Math.Clamp(_previewFrame, 0, frameCount - 1);
        var direction = Math.Clamp(_previewDirection, 0, 3);

        var x = frame * frameWidth;
        var y = direction * frameHeight;
        if (x + frameWidth > _previewSheet.Width || y + frameHeight > _previewSheet.Height)
        {
            return;
        }

        var output = new Bitmap(frameWidth, frameHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(output);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(
            _previewSheet,
            new Rectangle(0, 0, frameWidth, frameHeight),
            new Rectangle(x, y, frameWidth, frameHeight),
            GraphicsUnit.Pixel
        );

        _preview.SetImage(output);
    }

    private void ConfigureGenderButton(Button button, string text, CharacterGender gender)
    {
        button.Text = text;
        button.Size = new Size(110, 34);
        button.Margin = new Padding(0, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.ForeColor = System.Drawing.Color.White;
        button.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Click += (_, _) => SetGender(gender);
        UpdateGenderButtonState();
    }

    private void SetGender(CharacterGender gender)
    {
        if (_selectedGender == gender)
        {
            UpdateGenderButtonState();
            return;
        }

        RememberState();
        _selectedGender = gender;

        foreach (var category in _selectedPartByCategory.Keys.ToArray())
        {
            var selected = _selectedPartByCategory[category];
            if (!IsPartCompatibleWithGender(selected))
            {
                _selectedPartByCategory.Remove(category);
            }
        }

        UpdateGenderButtonState();
        PopulatePartsList();
        DrawPreview();
    }

    private void UpdateGenderButtonState()
    {
        var accent = System.Drawing.Color.FromArgb(247, 69, 96);
        var dark = System.Drawing.Color.FromArgb(55, 47, 49);

        _maleButton.BackColor = _selectedGender == CharacterGender.Male ? accent : dark;
        _femaleButton.BackColor = _selectedGender == CharacterGender.Female ? accent : dark;

        _maleButton.FlatAppearance.BorderColor = _maleButton.BackColor;
        _femaleButton.FlatAppearance.BorderColor = _femaleButton.BackColor;
    }

    private bool IsPartCompatibleWithGender(string partName)
    {
        if (partName.StartsWith("F_", StringComparison.OrdinalIgnoreCase))
        {
            return _selectedGender == CharacterGender.Female;
        }

        if (partName.StartsWith("B_", StringComparison.OrdinalIgnoreCase) ||
            partName.StartsWith("M_", StringComparison.OrdinalIgnoreCase))
        {
            return _selectedGender == CharacterGender.Male;
        }

        return true;
    }

    private void SaveCharacterProject()
    {
        var projectsRoot = Path.Combine(_charagenRoot, "projects");
        Directory.CreateDirectory(projectsRoot);

        using var dialog = new SaveFileDialog
        {
            Filter = "Character Creator build (*.crchar)|*.crchar",
            Title = "Save character build",
            InitialDirectory = projectsRoot,
            FileName = string.IsNullOrWhiteSpace(_exportName.Text)
                ? "character.crchar"
                : Path.GetFileNameWithoutExtension(_exportName.Text.Trim()) + ".crchar",
            AddExtension = true,
            DefaultExt = "crchar",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var project = new CharacterProject
            {
                Gender = _selectedGender.ToString(),
                AdvancedMode = _advancedMode,
                ExportName = _exportName.Text.Trim(),
                PreviewAnimation = _previewAnimation.ToString(),
                PreviewDirection = _previewDirection,
                Selections = new Dictionary<string, string>(
                    _selectedPartByCategory,
                    StringComparer.OrdinalIgnoreCase
                ),
            };

            var json = JsonSerializer.Serialize(
                project,
                new JsonSerializerOptions { WriteIndented = true }
            );
            File.WriteAllText(dialog.FileName, json);
            _status.Text = $"Saved build: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Unable to save build: " + ex.Message,
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void LoadCharacterProject()
    {
        var projectsRoot = Path.Combine(_charagenRoot, "projects");
        Directory.CreateDirectory(projectsRoot);

        using var dialog = new OpenFileDialog
        {
            Filter = "Character Creator build (*.crchar)|*.crchar",
            Title = "Load character build",
            InitialDirectory = projectsRoot,
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var project = JsonSerializer.Deserialize<CharacterProject>(json);
            if (project == null)
            {
                throw new InvalidOperationException("The build file is empty or invalid.");
            }

            RememberState();

            _selectedGender = Enum.TryParse<CharacterGender>(
                project.Gender,
                ignoreCase: true,
                out var gender
            )
                ? gender
                : CharacterGender.Male;

            _previewAnimation = Enum.TryParse<CharacterAnimation>(
                project.PreviewAnimation,
                ignoreCase: true,
                out var animation
            )
                ? animation
                : CharacterAnimation.Move;

            _previewDirection = Math.Clamp(project.PreviewDirection, 0, 3);
            _exportName.Text = project.ExportName ?? string.Empty;

            _selectedPartByCategory.Clear();
            var skipped = 0;

            foreach (var pair in project.Selections ?? new Dictionary<string, string>())
            {
                if (!_partsByCategory.TryGetValue(pair.Key, out var parts) ||
                    !parts.Any(part =>
                        string.Equals(part.Name, pair.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }

                if (!IsPartCompatibleWithGender(pair.Value))
                {
                    skipped++;
                    continue;
                }

                _selectedPartByCategory[pair.Key] = pair.Value;
            }

            UpdateGenderButtonState();
            SetAdvancedMode(project.AdvancedMode);
            UpdateAnimationButtonState();
            UpdateDirectionButtonState();
            PopulatePartsList();
            DrawPreview();

            _status.Text = skipped == 0
                ? $"Loaded build: {Path.GetFileName(dialog.FileName)}"
                : $"Loaded build: {Path.GetFileName(dialog.FileName)} — skipped {skipped} missing/incompatible selection(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Unable to load build: " + ex.Message,
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ApplyPreset(string preset)
    {
        RememberState();
        _selectedPartByCategory.Clear();

        PickRandomPart("Base", required: true);
        PickRandomPart("Body", required: true);
        PickPresetPart("Hair", new[] { preset }, chance: 0.90);
        PickRandomPart("Feet", chance: 0.95);

        if (_selectedGender == CharacterGender.Male)
        {
            PickRandomPart("Beard", chance: preset == "PIRATE" ? 0.68 : 0.34);
        }

        switch (preset)
        {
            case "KNIGHT":
                PickOneCategory(new[] { "Pants" }, required: true);
                PickPresetPart("Chest", new[] { "knight", "plate", "armor", "chain", "guard" }, required: true);
                PickPresetPart("Head", new[] { "helm", "helmet", "knight", "guard" }, chance: 0.75);
                PickPresetPart("Hands", new[] { "gauntlet", "armor", "plate" }, chance: 0.78);
                PickPresetPart("One Handed", new[] { "sword", "blade", "axe", "mace" }, required: true);
                PickPresetPart("Offhand", new[] { "shield", "buckler" }, chance: 0.82);
                PickPresetPart("Cape", new[] { "cape", "cloak", "royal" }, chance: 0.40);
                break;

            case "ARCHER":
                PickOneCategory(new[] { "Pants" }, required: true);
                PickPresetPart("Top", new[] { "archer", "ranger", "leather", "hunter" }, required: true);
                PickPresetPart("Chest", new[] { "archer", "ranger", "leather", "hunter" }, chance: 0.42);
                PickPresetPart("Head", new[] { "hood", "archer", "ranger", "hunter" }, chance: 0.48);
                PickPresetPart("Bow", new[] { "bow", "longbow", "archer" }, required: true);
                PickPresetPart("Quiver", new[] { "quiver", "arrow" }, required: true);
                _selectedPartByCategory.Remove("Offhand");
                break;

            case "MAGE":
                PickOneCategory(new[] { "Pants", "Skirt" }, required: true);
                if (!PickPresetPart("Overall", new[] { "robe", "mage", "wizard", "sorcer" }, chance: 0.58))
                {
                    PickPresetPart("Top", new[] { "robe", "mage", "wizard", "sorcer" }, required: true);
                    PickPresetPart("Chest", new[] { "robe", "mage", "wizard", "sorcer" }, chance: 0.36);
                }
                else
                {
                    _selectedPartByCategory.Remove("Top");
                    _selectedPartByCategory.Remove("Chest");
                }
                PickPresetPart("Head", new[] { "wizard", "mage", "hood", "hat" }, chance: 0.52);
                PickPresetPart("Staff", new[] { "staff", "wand", "mage", "wizard" }, required: true);
                PickPresetPart("Cape", new[] { "cape", "cloak", "mage" }, chance: 0.38);
                _selectedPartByCategory.Remove("Offhand");
                _selectedPartByCategory.Remove("Quiver");
                break;

            case "PIRATE":
                PickOneCategory(new[] { "Pants" }, required: true);
                PickPresetPart("Top", new[] { "pirate", "sailor", "corsair", "shirt" }, required: true);
                PickPresetPart("Chest", new[] { "pirate", "sailor", "corsair", "vest" }, chance: 0.44);
                PickPresetPart("Head", new[] { "pirate", "tricorn", "bandana", "captain" }, chance: 0.72);
                PickPresetPart("One Handed", new[] { "cutlass", "saber", "sword", "rapier" }, required: true);
                PickPresetPart("Offhand", new[] { "pistol", "shield", "lantern" }, chance: 0.24);
                _selectedPartByCategory.Remove("Quiver");
                break;

            default: // CIVILIAN
                PickOneCategory(new[] { "Pants", "Skirt" }, required: true);
                PickPresetPart("Top", new[] { "civil", "shirt", "common", "villager", "worker" }, required: true);
                PickPresetPart("Chest", new[] { "civil", "vest", "common", "villager" }, chance: 0.20);
                PickPresetPart("Head", new[] { "cap", "hat", "common", "villager" }, chance: 0.22);
                ClearEquipmentSelectionsOnly();
                break;
        }

        // Presets are mutually exclusive for main-hand weapon families.
        NormalizeWeaponSelection(preset);
        _status.Text = $"Preset applied: {preset}.";
        RefreshAfterRandomize();
    }

    private bool PickPresetPart(
        string category,
        IEnumerable<string> keywords,
        double chance = 1.0,
        bool required = false
    )
    {
        if (!required && _random.NextDouble() > chance)
        {
            _selectedPartByCategory.Remove(category);
            return false;
        }

        if (!_partsByCategory.TryGetValue(category, out var allParts))
        {
            _selectedPartByCategory.Remove(category);
            return false;
        }

        var compatible = allParts
            .Where(part => IsPartCompatibleWithGender(part.Name))
            .ToArray();

        if (compatible.Length == 0)
        {
            _selectedPartByCategory.Remove(category);
            return false;
        }

        var keywordArray = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .ToArray();

        var themed = compatible
            .Where(part => keywordArray.Any(keyword =>
                part.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var pool = themed.Length > 0 ? themed : compatible;
        _selectedPartByCategory[category] = pool[_random.Next(pool.Length)].Name;
        return true;
    }

    private void NormalizeWeaponSelection(string preset)
    {
        var allowed = preset switch
        {
            "ARCHER" => "Bow",
            "MAGE" => "Staff",
            "KNIGHT" => "One Handed",
            "PIRATE" => "One Handed",
            _ => null,
        };

        foreach (var category in new[] { "One Handed", "Staff", "Bow", "Rifle" })
        {
            if (!string.Equals(category, allowed, StringComparison.OrdinalIgnoreCase))
            {
                _selectedPartByCategory.Remove(category);
            }
        }

        if (allowed == null)
        {
            _selectedPartByCategory.Remove("Offhand");
            _selectedPartByCategory.Remove("Quiver");
        }
    }

    private void ClearEquipmentSelectionsOnly()
    {
        foreach (var category in new[]
                 {
                     "One Handed",
                     "Staff",
                     "Bow",
                     "Rifle",
                     "Offhand",
                     "Quiver",
                     "Cape",
                     "Artifact",
                     "FX",
                     "Bundles",
                 })
        {
            _selectedPartByCategory.Remove(category);
        }
    }

    private void RandomizeFaceAndHair()
    {
        RememberState();
        PickRandomPart("Hair", chance: 0.95);
        PickRandomPart("Head", chance: 0.30);

        if (_selectedGender == CharacterGender.Male)
        {
            PickRandomPart("Beard", chance: 0.42);
        }
        else
        {
            _selectedPartByCategory.Remove("Beard");
        }

        RefreshAfterRandomize();
    }

    private void RandomizeClothes()
    {
        RememberState();
        PickRandomPart("Hands", chance: 0.62);
        PickRandomPart("Feet", chance: 0.95);

        // Pick exactly one bottom when possible.
        PickOneCategory(new[] { "Pants", "Skirt" }, required: true);

        // Overall replaces Top + Chest. Otherwise build a normal layered outfit.
        var hasOverall = _partsByCategory.ContainsKey("Overall") && _random.NextDouble() < 0.20;
        if (hasOverall && PickRandomPart("Overall", required: true))
        {
            _selectedPartByCategory.Remove("Top");
            _selectedPartByCategory.Remove("Chest");
        }
        else
        {
            PickRandomPart("Top", chance: 0.90);
            PickRandomPart("Chest", chance: 0.45);
            _selectedPartByCategory.Remove("Overall");
        }

        RefreshAfterRandomize();
    }

    private void RandomizeEquipment()
    {
        RememberState();
        var weapon = PickOneCategory(
            new[] { "One Handed", "Staff", "Bow", "Rifle" },
            required: false,
            overallChance: 0.72
        );

        if (string.Equals(weapon, "Bow", StringComparison.OrdinalIgnoreCase))
        {
            PickRandomPart("Quiver", required: true);
            _selectedPartByCategory.Remove("Offhand");
        }
        else if (string.Equals(weapon, "One Handed", StringComparison.OrdinalIgnoreCase))
        {
            PickRandomPart("Offhand", chance: 0.52);
            _selectedPartByCategory.Remove("Quiver");
        }
        else
        {
            _selectedPartByCategory.Remove("Offhand");
            _selectedPartByCategory.Remove("Quiver");
        }

        PickRandomPart("Cape", chance: 0.30);

        if (_advancedMode)
        {
            PickRandomPart("Artifact", chance: 0.12);
            PickRandomPart("FX", chance: 0.08);
            PickRandomPart("Bundles", chance: 0.05);
        }

        RefreshAfterRandomize();
    }

    private void ClearEquipment()
    {
        RememberState();
        ClearEquipmentSelectionsOnly();
        RefreshAfterRandomize();
    }

    private void RefreshAfterRandomize()
    {
        _previewFrame = 0;
        PopulatePartsList();
        DrawPreview();
    }

    private void RandomizeCharacter()
    {
        RememberState();
        _selectedPartByCategory.Clear();

        // Character foundation.
        PickRandomPart("Base", required: true);
        PickRandomPart("Body", required: true);

        // Identity / face.
        PickRandomPart("Hair", chance: 0.92);
        PickRandomPart("Head", chance: 0.30);
        PickRandomPart("Hands", chance: 0.62);
        PickRandomPart("Feet", chance: 0.95);

        if (_selectedGender == CharacterGender.Male)
        {
            PickRandomPart("Beard", chance: 0.42);
        }

        // Bottoms are mutually exclusive.
        PickOneCategory(new[] { "Pants", "Skirt" }, required: true);

        // Overall replaces the normal top/chest stack. Otherwise a shirt/top can
        // optionally receive an additional chest layer.
        var hasOverall = _partsByCategory.ContainsKey("Overall") && _random.NextDouble() < 0.20;
        if (hasOverall && PickRandomPart("Overall", required: true))
        {
            _selectedPartByCategory.Remove("Top");
            _selectedPartByCategory.Remove("Chest");
        }
        else
        {
            PickRandomPart("Top", chance: 0.90);
            PickRandomPart("Chest", chance: 0.45);
            _selectedPartByCategory.Remove("Overall");
        }

        // Only one main weapon family can be active at a time.
        var weapon = PickOneCategory(
            new[] { "One Handed", "Staff", "Bow", "Rifle" },
            required: false,
            overallChance: 0.42
        );

        // Equipment dependencies.
        if (string.Equals(weapon, "Bow", StringComparison.OrdinalIgnoreCase))
        {
            PickRandomPart("Quiver", required: true);
            _selectedPartByCategory.Remove("Offhand");
        }
        else if (string.Equals(weapon, "One Handed", StringComparison.OrdinalIgnoreCase))
        {
            PickRandomPart("Offhand", chance: 0.48);
            _selectedPartByCategory.Remove("Quiver");
        }
        else
        {
            _selectedPartByCategory.Remove("Offhand");
            _selectedPartByCategory.Remove("Quiver");
        }

        // Back accessories are intentionally uncommon so randomized characters
        // do not all look overloaded.
        PickRandomPart("Cape", chance: 0.22);

        if (_advancedMode)
        {
            PickRandomPart("Artifact", chance: 0.10);
            PickRandomPart("FX", chance: 0.08);
            PickRandomPart("Bundles", chance: 0.05);
        }

        RefreshAfterRandomize();
    }

    private bool PickRandomPart(string category, double chance = 1.0, bool required = false)
    {
        if (!required && _random.NextDouble() > chance)
        {
            _selectedPartByCategory.Remove(category);
            return false;
        }

        if (!_partsByCategory.TryGetValue(category, out var allParts))
        {
            _selectedPartByCategory.Remove(category);
            return false;
        }

        var compatible = allParts
            .Where(part => IsPartCompatibleWithGender(part.Name))
            .ToArray();

        if (compatible.Length == 0)
        {
            _selectedPartByCategory.Remove(category);
            return false;
        }

        _selectedPartByCategory[category] =
            compatible[_random.Next(compatible.Length)].Name;
        return true;
    }

    private string? PickOneCategory(
        IEnumerable<string> categories,
        bool required,
        double overallChance = 1.0
    )
    {
        var categoryArray = categories.ToArray();

        foreach (var category in categoryArray)
        {
            _selectedPartByCategory.Remove(category);
        }

        if (!required && _random.NextDouble() > overallChance)
        {
            return null;
        }

        var available = categoryArray
            .Where(category =>
                _partsByCategory.TryGetValue(category, out var parts) &&
                parts.Any(part => IsPartCompatibleWithGender(part.Name)))
            .ToArray();

        if (available.Length == 0)
        {
            return null;
        }

        var selectedCategory = available[_random.Next(available.Length)];
        return PickRandomPart(selectedCategory, required: true)
            ? selectedCategory
            : null;
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
            _partsByCategory.Clear();

            foreach (var directory in Directory.GetDirectories(_charagenRoot))
            {
                var category = Path.GetFileName(directory);
                _partsByCategory[category] = DiscoverFamilies(directory);
            }

            ReloadCategoryList();

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

    private static bool IsIgnoredArtistAnimationFile(string fileName)
    {
        var pieces = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 2)
        {
            return false;
        }

        var animationIndex = 0;
        if (pieces.Length >= 3 &&
            (string.Equals(pieces[0], "M", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pieces[0], "F", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pieces[0], "B", StringComparison.OrdinalIgnoreCase)))
        {
            animationIndex = 1;
        }

        return animationIndex < pieces.Length &&
               ArtistIgnoredAnimationCodes.Contains(pieces[animationIndex]);
    }

    private static string GetFriendlyPartName(string partName)
    {
        var displayName = partName;

        if (displayName.Length > 2 &&
            displayName[1] == '_' &&
            (char.ToUpperInvariant(displayName[0]) == 'B' ||
             char.ToUpperInvariant(displayName[0]) == 'M' ||
             char.ToUpperInvariant(displayName[0]) == 'F'))
        {
            displayName = displayName[2..];
        }

        return displayName.Replace('_', ' ').Trim();
    }

    private static string GetAnimationSummary(PartFamily part)
    {
        var missing = AnimationDefinitions
            .Where(definition => !part.Has(definition.Animation))
            .Select(definition => definition.Label)
            .ToArray();

        return missing.Length == 0
            ? "Animations: 6/6"
            : $"Animations: {AnimationDefinitions.Length - missing.Length}/{AnimationDefinitions.Length} — Missing: {string.Join(", ", missing)}";
    }

    private static List<PartFamily> DiscoverFamilies(string categoryDirectory)
    {
        var groups = new Dictionary<string, PartFamily>(StringComparer.OrdinalIgnoreCase);
        var category = Path.GetFileName(categoryDirectory);

        foreach (var file in Directory.GetFiles(categoryDirectory, "*.png", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(categoryDirectory, file);
            var artistFileName = Path.GetFileNameWithoutExtension(relative);
            if (IsIgnoredArtistAnimationFile(artistFileName))
            {
                continue;
            }

            var animation = DetectAnimation(relative, category, out var cleanName, out var depth);
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                cleanName = Path.GetFileNameWithoutExtension(file);
            }

            if (!groups.TryGetValue(cleanName, out var family))
            {
                family = new PartFamily { Name = cleanName };
                groups[cleanName] = family;
            }

            switch (depth)
            {
                case PaperdollDepth.Back:
                    family.BackFiles[animation] = file;
                    break;
                case PaperdollDepth.Front:
                    family.FrontFiles[animation] = file;
                    break;
                default:
                    family.Files[animation] = file;
                    break;
            }
        }

        return groups.Values
            .OrderBy(part => part.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CharacterAnimation DetectAnimation(
        string relativePath,
        string category,
        out string cleanName,
        out PaperdollDepth depth
    )
    {
        depth = PaperdollDepth.Normal;
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

        if (TryParseArtistFileName(
                fileName,
                category,
                out var artistAnimation,
                out var artistName,
                out depth
            ))
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
        string category,
        out CharacterAnimation animation,
        out string cleanName,
        out PaperdollDepth depth
    )
    {
        animation = CharacterAnimation.Move;
        cleanName = fileName;
        depth = PaperdollDepth.Normal;

        var pieces = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 2)
        {
            return false;
        }

        var animationIndex = 0;
        string? genderPrefix = null;

        // IMPORTANT: in equipment categories the artist pack uses B_/F_ for
        // Back/Front drawing halves. Treating them as Boy/Female caused the
        // Female/front half to be filtered out while MALE was selected, which is
        // why the Down/front direction showed no equipment.
        if (SplitDepthCategories.Contains(category) &&
            pieces.Length >= 3 &&
            (string.Equals(pieces[0], "B", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pieces[0], "F", StringComparison.OrdinalIgnoreCase)))
        {
            depth = string.Equals(pieces[0], "B", StringComparison.OrdinalIgnoreCase)
                ? PaperdollDepth.Back
                : PaperdollDepth.Front;
            animationIndex = 1;
        }
        else if (pieces.Length >= 3 &&
                 (string.Equals(pieces[0], "M", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(pieces[0], "F", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(pieces[0], "B", StringComparison.OrdinalIgnoreCase)))
        {
            // Outside split equipment categories B/F/M keep their gender meaning.
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

        _populatingParts = true;
        _partsView.BeginUpdate();
        _partsView.Items.Clear();
        _partImages.Images.Clear();

        using (var noneImage = new Bitmap(96, 96, PixelFormat.Format32bppArgb))
        {
            using var graphics = Graphics.FromImage(noneImage);
            graphics.Clear(System.Drawing.Color.Transparent);
            using var pen = new Pen(System.Drawing.Color.FromArgb(100, 100, 100), 2);
            graphics.DrawLine(pen, 18, 18, 78, 78);
            graphics.DrawLine(pen, 78, 18, 18, 78);
            _partImages.Images.Add("None", new Bitmap(noneImage));
        }

        var noneItem = new ListViewItem("None")
        {
            ImageKey = "None",
            Tag = "None",
        };
        _partsView.Items.Add(noneItem);

        if (category != null && _partsByCategory.TryGetValue(category, out var parts))
        {
            var search = _partSearch.Text.Trim();

            var visibleParts = parts
                .Where(part => IsPartCompatibleWithGender(part.Name))
                .Where(part =>
                    string.IsNullOrWhiteSpace(search) ||
                    part.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    GetFriendlyPartName(part.Name).Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(part =>
                    !_favoritesOnly ||
                    _favoriteParts.Contains(MakeFavoriteKey(category, part.Name)))
                .OrderByDescending(part =>
                    _favoriteParts.Contains(MakeFavoriteKey(category, part.Name)))
                .ThenBy(part => GetFriendlyPartName(part.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var part in visibleParts)
            {
                var key = category + "::" + part.Name;
                using var thumbnail = CreatePartThumbnail(part);
                _partImages.Images.Add(key, new Bitmap(thumbnail));

                var favorite = _favoriteParts.Contains(MakeFavoriteKey(category, part.Name));
                var friendlyName = GetFriendlyPartName(part.Name);
                var animationSummary = GetAnimationSummary(part);
                var item = new ListViewItem((favorite ? "★ " : string.Empty) + friendlyName)
                {
                    ImageKey = key,
                    Tag = part.Name,
                    ToolTipText = $"{part.Name}\n{animationSummary}",
                };
                _partsView.Items.Add(item);
            }

            if (_selectedPartByCategory.TryGetValue(category, out var selected))
            {
                var selectedItem = _partsView.Items
                    .Cast<ListViewItem>()
                    .FirstOrDefault(item =>
                        string.Equals(item.Tag?.ToString(), selected, StringComparison.OrdinalIgnoreCase));
                selectedItem?.Selected = true;
                selectedItem?.EnsureVisible();
            }
            else
            {
                noneItem.Selected = true;
            }
        }
        else
        {
            noneItem.Selected = true;
        }

        _partsView.EndUpdate();
        _populatingParts = false;
        UpdateFavoriteButtonState();
    }

    private void SelectCurrentPart()
    {
        if (_reloading || _populatingParts || _partsView.SelectedItems.Count == 0)
        {
            return;
        }

        var category = _categoryList.SelectedItem?.ToString();
        var part = _partsView.SelectedItems[0].Tag?.ToString();
        if (category == null || part == null)
        {
            return;
        }

        var current = _selectedPartByCategory.TryGetValue(category, out var currentPart)
            ? currentPart
            : "None";

        if (string.Equals(current, part, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RememberState();

        if (string.Equals(part, "None", StringComparison.OrdinalIgnoreCase))
        {
            _selectedPartByCategory.Remove(category);
        }
        else
        {
            _selectedPartByCategory[category] = part;
            SuggestPaperdollExportNames(part);
        }

        _previewFrame = 0;
        DrawPreview();
    }

    private void DrawPreview()
    {
        try
        {
            _previewSheet?.Dispose();
            _previewSheet = RenderCharacter(_previewAnimation);
            _previewFrame = 0;
            ShowPreviewFrame();
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
                    Depth = selected.Depth,
                    Bitmap = new Bitmap(source),
                });
            }

            if (layers.Count == 0)
            {
                return null;
            }

            // Paperdoll packs are not always exported at exactly the same sheet
            // dimensions. Using the largest sheet as the preview canvas causes a
            // 2x accessory sheet to make the player look tiny and, more
            // importantly, makes directional rows no longer line up.
            //
            // Normalize every layer per frame instead. The player/base layer is
            // the authoritative frame size/count and accessories are sampled
            // from their own 4 directional rows, then mapped into that canvas.
            var playerLayers = layers
                .Where(layer =>
                    CategoryToPaperdollSlot.TryGetValue(layer.Category, out var slot) &&
                    string.Equals(slot, "Player", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var referenceLayer = playerLayers.FirstOrDefault() ?? layers[0];
            var frameSize = GetLayerFrameSize(referenceLayer.Bitmap);
            var frameCount = GetLayerFrameCount(referenceLayer.Bitmap);

            if (frameSize <= 0)
            {
                frameSize = layers
                    .Select(layer => GetLayerFrameSize(layer.Bitmap))
                    .Where(size => size > 0)
                    .DefaultIfEmpty(128)
                    .Min();
            }

            if (frameCount <= 0)
            {
                frameCount = layers
                    .Select(layer => GetLayerFrameCount(layer.Bitmap))
                    .DefaultIfEmpty(1)
                    .Max();
            }

            frameSize = Math.Max(1, frameSize);
            frameCount = Math.Max(1, frameCount);

            var width = checked(frameSize * frameCount);
            var height = checked(frameSize * 4);
            var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(output);
            graphics.Clear(System.Drawing.Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;

            for (var directionRow = 0; directionRow < 4; directionRow++)
            {
                var orderedLayers = OrderLayersForDirection(layers, directionRow).ToArray();

                for (var frame = 0; frame < frameCount; frame++)
                {
                    var destination = new Rectangle(
                        frame * frameSize,
                        directionRow * frameSize,
                        frameSize,
                        frameSize
                    );

                    foreach (var layer in orderedLayers)
                    {
                        DrawLayerFrame(
                            graphics,
                            layer.Bitmap,
                            directionRow,
                            frame,
                            destination
                        );
                    }
                }
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

    private static int GetLayerFrameSize(Bitmap bitmap)
    {
        // Character/paperdoll sheets use four direction rows. Frames are square,
        // so row height is the reliable cell size even when the sheet has a
        // different number of animation columns.
        return Math.Max(1, bitmap.Height / 4);
    }

    private static int GetLayerFrameCount(Bitmap bitmap)
    {
        var frameSize = GetLayerFrameSize(bitmap);
        return Math.Max(1, bitmap.Width / frameSize);
    }

    private static void DrawLayerFrame(
        Graphics graphics,
        Bitmap bitmap,
        int directionRow,
        int frame,
        Rectangle destination
    )
    {
        var sourceFrameSize = GetLayerFrameSize(bitmap);
        var sourceFrameCount = GetLayerFrameCount(bitmap);

        // Accessories frequently fall back from IDLE/CAST/etc. to MOVE and can
        // therefore have a different number of columns than the player sheet.
        // Cycle their available frames instead of letting them disappear.
        var sourceFrame = sourceFrameCount <= 1
            ? 0
            : frame % sourceFrameCount;

        var sourceX = sourceFrame * sourceFrameSize;
        var sourceY = Math.Clamp(directionRow, 0, 3) * sourceFrameSize;

        if (sourceX >= bitmap.Width || sourceY >= bitmap.Height)
        {
            return;
        }

        var source = new Rectangle(
            sourceX,
            sourceY,
            Math.Min(sourceFrameSize, bitmap.Width - sourceX),
            Math.Min(sourceFrameSize, bitmap.Height - sourceY)
        );

        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }

        graphics.DrawImage(
            bitmap,
            destination,
            source,
            GraphicsUnit.Pixel
        );
    }

    private static IEnumerable<SelectedLayer> OrderLayersForDirection(
        IEnumerable<SelectedLayer> layers,
        int directionRow
    )
    {
        if (!ServerPaperdollOrder.TryGetValue(directionRow, out var slotOrder))
        {
            return layers;
        }

        var slotPriority = slotOrder
            .Select((slot, index) => (slot, index))
            .ToDictionary(pair => pair.slot, pair => pair.index, StringComparer.OrdinalIgnoreCase);

        return layers
            .Select((layer, originalIndex) => new
            {
                Layer = layer,
                OriginalIndex = originalIndex,
                Slot = CategoryToPaperdollSlot.TryGetValue(layer.Category, out var slot)
                    ? slot
                    : "Tag",
            })
            .OrderBy(item => item.Layer.Depth switch
            {
                PaperdollDepth.Back => 0,
                PaperdollDepth.Normal => 1,
                PaperdollDepth.Front => 2,
                _ => 1,
            })
            .ThenBy(item => slotPriority.TryGetValue(item.Slot, out var priority)
                ? priority
                : int.MaxValue)
            .ThenBy(item => item.OriginalIndex)
            .Select(item => item.Layer);
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

        if (rule == LayerRule.DirectionalBackAccessory)
        {
            return IsBackAccessoryBehind(directionRow);
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
                return IsBackAccessoryBehind(directionRow);
            }
        }

        if (TopRowBehindPartKeywords.Any(keyword =>
                partName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return IsBackAccessoryBehind(directionRow);
        }

        return false;
    }

    private static bool IsWeaponBehind(int directionRow)
    {
        // Artist/Intersect row order in these sheets:
        // 0 = front/down, 1 = left, 2 = right, 3 = back/up.
        // Main-hand equipment is behind on the back view and on the side where
        // the weapon arm crosses behind the torso.
        return directionRow switch
        {
            0 => false,
            1 => true,
            2 => false,
            3 => true,
            _ => false,
        };
    }

    private static bool IsOffhandBehind(int directionRow)
    {
        // Offhand/shields mirror the side-view behavior of the main hand.
        return directionRow switch
        {
            0 => false,
            1 => false,
            2 => true,
            3 => true,
            _ => false,
        };
    }

    private static bool IsBackAccessoryBehind(int directionRow)
    {
        // Capes, quivers, backpacks, balloons, tails, wings, etc. sit behind
        // the body when viewed from the front or sides. On the back/up row the
        // accessory itself must be drawn over the body so it remains visible.
        return directionRow switch
        {
            0 => true,
            1 => true,
            2 => true,
            3 => false,
            _ => true,
        };
    }

    private IEnumerable<(string Category, string PartName, string File, PaperdollDepth Depth)> GetSelectedLayers(
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
            if (family == null)
            {
                continue;
            }

            var back = family.ResolveBack(animation);
            if (!string.IsNullOrWhiteSpace(back))
            {
                yield return (category, selectedName, back, PaperdollDepth.Back);
            }

            var normal = family.Resolve(animation);
            if (!string.IsNullOrWhiteSpace(normal))
            {
                yield return (category, selectedName, normal, PaperdollDepth.Normal);
            }

            var front = family.ResolveFront(animation);
            if (!string.IsNullOrWhiteSpace(front))
            {
                yield return (category, selectedName, front, PaperdollDepth.Front);
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
                "The original artist filenames were preserved. Mov/Mel/Mag/Idl/Ran/Use are recognized automatically; Blo/Fis/Rif are ignored by the character generator.",
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

    private List<string> GetCharacterWarnings()
    {
        var warnings = new List<string>();

        if (!_selectedPartByCategory.ContainsKey("Base"))
        {
            warnings.Add("No Base paperdoll is selected.");
        }

        if (!_selectedPartByCategory.ContainsKey("Body"))
        {
            warnings.Add("No Body paperdoll is selected.");
        }

        if (_selectedPartByCategory.ContainsKey("Pants") &&
            _selectedPartByCategory.ContainsKey("Skirt"))
        {
            warnings.Add("Pants and Skirt are both selected.");
        }

        if (_selectedPartByCategory.ContainsKey("Overall") &&
            (_selectedPartByCategory.ContainsKey("Top") ||
             _selectedPartByCategory.ContainsKey("Chest")))
        {
            warnings.Add("Overall overlaps the Top/Chest outfit stack.");
        }

        var weaponCategories = new[] { "One Handed", "Staff", "Bow", "Rifle" };
        var selectedWeapons = weaponCategories
            .Where(_selectedPartByCategory.ContainsKey)
            .ToArray();

        if (selectedWeapons.Length > 1)
        {
            warnings.Add("More than one main weapon family is selected.");
        }

        if (_selectedPartByCategory.ContainsKey("Bow") &&
            !_selectedPartByCategory.ContainsKey("Quiver"))
        {
            warnings.Add("Bow is selected without a Quiver.");
        }

        foreach (var pair in _selectedPartByCategory)
        {
            if (!_partsByCategory.TryGetValue(pair.Key, out var parts))
            {
                warnings.Add($"Category '{pair.Key}' no longer exists.");
                continue;
            }

            var family = parts.FirstOrDefault(part =>
                string.Equals(part.Name, pair.Value, StringComparison.OrdinalIgnoreCase));

            if (family == null)
            {
                warnings.Add($"Paperdoll '{pair.Value}' in {pair.Key} no longer exists.");
                continue;
            }

            foreach (var definition in AnimationDefinitions)
            {
                if (!family.Has(definition.Animation))
                {
                    warnings.Add(
                        $"{pair.Key} / {pair.Value} is missing {definition.Label}; MOVE fallback will be used."
                    );
                }
            }
        }

        return warnings
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private void SuggestPaperdollExportNames(string partName)
    {
        var friendly = GetFriendlyPartName(partName);
        if (string.IsNullOrWhiteSpace(_paperdollExportName.Text))
        {
            _paperdollExportName.Text = MakeFileStem(friendly);
        }

        if (string.IsNullOrWhiteSpace(_itemExportName.Text))
        {
            _itemExportName.Text = friendly;
        }
    }

    private static string MakeFileStem(string value)
    {
        var cleaned = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalid, '_');
        }

        cleaned = string.Join(
            "_",
            cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
        );

        return cleaned.Trim('_', '.', ' ');
    }

    private bool TryGetSelectedPaperdoll(
        out string category,
        out string partName,
        out PartFamily? family
    )
    {
        category = _categoryList.SelectedItem?.ToString() ?? string.Empty;
        partName = _partsView.SelectedItems.Count > 0
            ? _partsView.SelectedItems[0].Tag?.ToString() ?? string.Empty
            : string.Empty;
        family = null;

        if (string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(partName) ||
            string.Equals(partName, "None", StringComparison.OrdinalIgnoreCase) ||
            !_partsByCategory.TryGetValue(category, out var parts))
        {
            return false;
        }

        var selectedPartName = partName;
        family = parts.FirstOrDefault(part =>
            string.Equals(part.Name, selectedPartName, StringComparison.OrdinalIgnoreCase));

        return family != null;
    }

    private static Rectangle FindOpaqueBounds(Bitmap bitmap, Rectangle source)
    {
        var left = source.Right;
        var top = source.Bottom;
        var right = source.Left - 1;
        var bottom = source.Top - 1;

        for (var y = source.Top; y < source.Bottom; y++)
        {
            for (var x = source.Left; x < source.Right; x++)
            {
                if (bitmap.GetPixel(x, y).A == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
        {
            return source;
        }

        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static Bitmap CreateItemPreview32(Bitmap sheet)
    {
        var frameHeight = Math.Max(1, sheet.Height / 4);
        var frameWidth = Math.Min(frameHeight, sheet.Width);
        var firstDownFrame = new Rectangle(
            0,
            0,
            Math.Min(frameWidth, sheet.Width),
            Math.Min(frameHeight, sheet.Height)
        );

        var opaque = FindOpaqueBounds(sheet, firstDownFrame);
        var output = new Bitmap(32, 32, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(output);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;

        const int padding = 2;
        var maxSize = 32 - padding * 2;
        var scale = Math.Min(
            (double)maxSize / Math.Max(1, opaque.Width),
            (double)maxSize / Math.Max(1, opaque.Height)
        );

        var width = Math.Max(1, (int)Math.Round(opaque.Width * scale));
        var height = Math.Max(1, (int)Math.Round(opaque.Height * scale));
        var destination = new Rectangle(
            (32 - width) / 2,
            (32 - height) / 2,
            width,
            height
        );

        graphics.DrawImage(sheet, destination, opaque, GraphicsUnit.Pixel);
        return output;
    }

    private static Bitmap? RenderPaperdollFamily(
        PartFamily family,
        CharacterAnimation animation
    )
    {
        var paths = new[]
        {
            family.ResolveBack(animation),
            family.Resolve(animation),
            family.ResolveFront(animation),
        }
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Cast<string>()
            .ToArray();

        if (paths.Length == 0)
        {
            return null;
        }

        var bitmaps = new List<Bitmap>(paths.Length);
        try
        {
            foreach (var path in paths)
            {
                using var source = new Bitmap(path);
                bitmaps.Add(new Bitmap(source));
            }

            var width = bitmaps.Max(bitmap => bitmap.Width);
            var height = bitmaps.Max(bitmap => bitmap.Height);
            var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(output);
            graphics.Clear(System.Drawing.Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;

            foreach (var bitmap in bitmaps)
            {
                graphics.DrawImageUnscaled(bitmap, 0, 0);
            }

            return output;
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    private void ExportSelectedPaperdoll()
    {
        if (!TryGetSelectedPaperdoll(out var category, out var partName, out var family) ||
            family == null)
        {
            MessageBox.Show(
                this,
                "Select a paperdoll thumbnail before exporting.",
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var paperdollName = MakeFileStem(_paperdollExportName.Text);
        var itemName = _itemExportName.Text.Trim();

        if (string.IsNullOrWhiteSpace(paperdollName))
        {
            MessageBox.Show(
                this,
                "Enter a paperdoll name.",
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(itemName))
        {
            MessageBox.Show(
                this,
                "Enter an item name.",
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var itemIconStem = MakeFileStem(itemName);
        if (string.IsNullOrWhiteSpace(itemIconStem))
        {
            MessageBox.Show(
                this,
                "The item name cannot be converted to a valid icon filename.",
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        Directory.CreateDirectory(_paperdollsRoot);
        Directory.CreateDirectory(_itemsRoot);

        var writtenPaperdolls = new List<string>();
        try
        {
            foreach (var definition in AnimationDefinitions)
            {
                using var copy = RenderPaperdollFamily(family, definition.Animation);
                if (copy == null)
                {
                    throw new InvalidOperationException(
                        $"No source sprite could be resolved for {definition.Label}."
                    );
                }

                var fileName = paperdollName + definition.Suffix + ".png";
                var destination = Path.Combine(_paperdollsRoot, fileName);
                var temporary = destination + ".tmp";

                copy.Save(temporary, ImageFormat.Png);
                File.Move(temporary, destination, true);
                writtenPaperdolls.Add(fileName);
            }

            using var previewSource =
                RenderPaperdollFamily(family, CharacterAnimation.Idle) ??
                RenderPaperdollFamily(family, CharacterAnimation.Move);

            if (previewSource == null)
            {
                throw new InvalidOperationException("Unable to find a source image for the item preview.");
            }

            var itemIconFile = itemIconStem + ".png";
            var itemDestination = Path.Combine(_itemsRoot, itemIconFile);
            var itemTemporary = itemDestination + ".tmp";

            using (var preview = CreateItemPreview32(previewSource))
            {
                preview.Save(itemTemporary, ImageFormat.Png);
            }

            File.Move(itemTemporary, itemDestination, true);

            GameContentManager.ReloadPaperdollAndItemTextures();

            var maleCompatible =
                !partName.StartsWith("F_", StringComparison.OrdinalIgnoreCase);
            var femaleCompatible =
                !partName.StartsWith("B_", StringComparison.OrdinalIgnoreCase) &&
                !partName.StartsWith("M_", StringComparison.OrdinalIgnoreCase);

            if (_createItemAfterPaperdollExport.Checked && _afterPaperdollExport != null)
            {
                _afterPaperdollExport(
                    new GeneratedItemRequest
                    {
                        ItemName = itemName,
                        IconFile = itemIconFile,
                        PaperdollFile = paperdollName + ".png",
                        SourceCategory = category,
                        SourcePartName = partName,
                        MaleCompatible = maleCompatible,
                        FemaleCompatible = femaleCompatible,
                    }
                );
            }

            _status.Text =
                $"Paperdoll exported: {paperdollName} — Item icon: {itemIconFile} (32x32).";

            MessageBox.Show(
                this,
                $"Paperdoll export complete.\n\n" +
                $"Paperdoll: resources\\paperdolls\\{paperdollName}.png (+ 5 animation overrides)\n" +
                $"Item icon: resources\\items\\{itemIconFile} (32x32)\n" +
                (_createItemAfterPaperdollExport.Checked
                    ? "\nThe Item Editor will create and prefill the equipment item."
                    : string.Empty),
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Paperdoll export failed: " + ex.Message,
                "Character Generator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
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

        var warnings = GetCharacterWarnings();
        if (warnings.Count > 0)
        {
            var warningText =
                "The character can be exported, but the following issues were detected:\n\n" +
                string.Join("\n", warnings.Select(warning => "• " + warning)) +
                "\n\nExport anyway?";

            if (MessageBox.Show(
                    this,
                    warningText,
                    "Character Generator — Validation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                ) != DialogResult.Yes)
            {
                return;
            }
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
