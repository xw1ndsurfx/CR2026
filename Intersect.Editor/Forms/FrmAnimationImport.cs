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

        public int XFrames { get; set; } = 1;

        public int YFrames { get; set; } = 1;

        public int FrameCount { get; set; } = 1;

        public DateTime LastWriteTimeUtc { get; init; }

        public bool MetadataLoaded { get; set; }
    }

    private sealed class AssetPreviewLoad
    {
        public required Bitmap Bitmap { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }

        public int XFrames { get; init; }

        public int YFrames { get; init; }

        public int FrameCount { get; init; }
    }

    private sealed class ZipFrameEntry
    {
        public required ZipArchiveEntry Entry { get; init; }

        public required string Directory { get; init; }

        public required string BaseName { get; init; }

        public int FrameIndex { get; init; }
    }

    private sealed class ZipImportProgress
    {
        public int CompletedUnits { get; init; }

        public int TotalUnits { get; init; }

        public required string Stage { get; init; }

        public string CurrentItem { get; init; } = string.Empty;

        public int CurrentIndex { get; init; }

        public int CurrentTotal { get; init; }
    }

    private sealed class ZipImportResult
    {
        public int ImportedAnimations { get; set; }

        public int CombinedAnimations { get; set; }

        public int CombinedFrames { get; set; }

        public int StandaloneSheets { get; set; }

        public int Skipped { get; set; }

        public int RenamedDuplicates { get; set; }

        public bool Cancelled { get; set; }
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

    private const string FavoritesView = "[Favorites]";
    private const string NewView = "[New]";

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
    private readonly string _thumbnailRoot;
    private readonly string _favoritesPath;
    private readonly Action<GeneratedAnimationRequest>? _afterImport;

    private readonly ListBox _categoryList = new();
    private readonly ListView _assetList = new();
    private readonly ImageList _assetImages = new();
    private readonly TextBox _assetSearch = new();
    private readonly ComboBox _assetSort = new();
    private readonly Button _favoriteButton = new();
    private readonly Button _previousPageButton = new();
    private readonly Button _nextPageButton = new();
    private readonly Label _pageLabel = new();
    private readonly AnimationPreview _preview = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _category = new();
    private readonly NumericUpDown _xFrames = new();
    private readonly NumericUpDown _yFrames = new();
    private readonly NumericUpDown _frameCount = new();
    private readonly NumericUpDown _frameDuration = new();
    private readonly Label _assetInfo = new();
    private readonly Label _status = new();
    private readonly Button _importZipButton = new();
    private readonly Button _cancelImportButton = new();
    private readonly ProgressBar _importProgress = new();
    private readonly Label _importProgressLabel = new();
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 80 };

    private const int AssetPageSize = 120;

    private CancellationTokenSource? _importCancellation;
    private CancellationTokenSource? _libraryLoadCancellation;
    private CancellationTokenSource? _thumbnailCancellation;
    private CancellationTokenSource? _selectionCancellation;

    private readonly List<AnimationAsset> _assets = new();
    private readonly List<AnimationAsset> _visibleAssets = new();
    private readonly HashSet<string> _favoriteAssetKeys =
        new(StringComparer.OrdinalIgnoreCase);
    private AnimationAsset? _selectedAsset;
    private int _assetPage;
    private bool _libraryLoading;

    public FrmAnimationImport(Action<GeneratedAnimationRequest>? afterImport = null)
    {
        _afterImport = afterImport;
        _gameRoot = ResolveGameRoot();
        _importRoot = Path.Combine(_gameRoot, "animationimport");
        _animationsRoot = Path.Combine(_gameRoot, "resources", "animations");
        _thumbnailRoot = Path.Combine(
            _gameRoot,
            ".animationimport-cache",
            "thumbnails"
        );
        _favoritesPath = Path.Combine(
            _gameRoot,
            ".animationimport-cache",
            "favorites.txt"
        );

        Text = "Animations Import";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1380, 860);
        BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        ForeColor = System.Drawing.Color.Gainsboro;

        Directory.CreateDirectory(_importRoot);
        Directory.CreateDirectory(_animationsRoot);
        Directory.CreateDirectory(_thumbnailRoot);
        LoadFavorites();
        foreach (var categoryName in Categories)
        {
            Directory.CreateDirectory(Path.Combine(_importRoot, categoryName));
        }

        BuildInterface();

        _previewTimer.Tick += (_, _) =>
        {
            _preview.Advance((int)_frameCount.Value);
        };

        Shown += async (_, _) =>
        {
            _previewTimer.Start();
            await ReloadAssetsAsync();
        };

        FormClosed += (_, _) =>
        {
            _importCancellation?.Cancel();
            _libraryLoadCancellation?.Cancel();
            _thumbnailCancellation?.Cancel();
            _selectionCancellation?.Cancel();

            _importCancellation?.Dispose();
            _libraryLoadCancellation?.Dispose();
            _thumbnailCancellation?.Dispose();
            _selectionCancellation?.Dispose();

            _importCancellation = null;
            _libraryLoadCancellation = null;
            _thumbnailCancellation = null;
            _selectionCancellation = null;

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

        _importZipButton.Text = "IMPORT ZIP";
        _importZipButton.Size = new Size(145, 34);
        _importZipButton.FlatStyle = FlatStyle.Flat;
        _importZipButton.BackColor = System.Drawing.Color.FromArgb(247, 69, 96);
        _importZipButton.ForeColor = System.Drawing.Color.White;
        _importZipButton.FlatAppearance.BorderColor = _importZipButton.BackColor;
        _importZipButton.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            9,
            FontStyle.Bold
        );
        _importZipButton.Cursor = Cursors.Hand;
        _importZipButton.Click += async (_, _) => await ImportZipAsync();

        var refresh = CreateDarkButton("REFRESH");
        refresh.Size = new Size(120, 34);
        refresh.Click += async (_, _) => await ReloadAssetsAsync();

        var open = CreateDarkButton("OPEN ANIMATIONIMPORT");
        open.Size = new Size(205, 34);
        open.Click += (_, _) => OpenImportFolder();

        _cancelImportButton.Text = "CANCEL";
        _cancelImportButton.Size = new Size(105, 34);
        _cancelImportButton.FlatStyle = FlatStyle.Flat;
        _cancelImportButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _cancelImportButton.ForeColor = System.Drawing.Color.Gainsboro;
        _cancelImportButton.FlatAppearance.BorderColor =
            System.Drawing.Color.FromArgb(90, 78, 81);
        _cancelImportButton.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            9,
            FontStyle.Bold
        );
        _cancelImportButton.Cursor = Cursors.Hand;
        _cancelImportButton.Enabled = false;
        _cancelImportButton.Click += (_, _) =>
        {
            _importCancellation?.Cancel();
            _cancelImportButton.Enabled = false;
            _cancelImportButton.Text = "CANCELLING...";
            _status.Text = "Cancelling ZIP import safely after the current frame...";
        };

        _importProgress.Width = 270;
        _importProgress.Height = 24;
        _importProgress.Margin = new Padding(14, 5, 0, 0);
        _importProgress.Minimum = 0;
        _importProgress.Maximum = 100;
        _importProgress.Value = 0;
        _importProgress.Visible = false;

        _importProgressLabel.AutoSize = false;
        _importProgressLabel.Width = 330;
        _importProgressLabel.Height = 34;
        _importProgressLabel.Margin = new Padding(8, 0, 0, 0);
        _importProgressLabel.ForeColor = System.Drawing.Color.Silver;
        _importProgressLabel.TextAlign = ContentAlignment.MiddleLeft;
        _importProgressLabel.Visible = false;

        toolbar.Controls.Add(_importZipButton);
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(open);
        toolbar.Controls.Add(_cancelImportButton);
        toolbar.Controls.Add(_importProgress);
        toolbar.Controls.Add(_importProgressLabel);
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
        _categoryList.SelectedIndexChanged += (_, _) =>
        {
            _assetPage = 0;
            PopulateAssetList();
        };
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
        _assetList.SelectedIndexChanged += async (_, _) => await SelectAssetAsync();

        var assetHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = System.Drawing.Color.FromArgb(38, 32, 34),
        };
        assetHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        assetHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        assetHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        assetHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var filterBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = System.Drawing.Color.FromArgb(38, 32, 34),
        };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        _assetSearch.Dock = DockStyle.Fill;
        _assetSearch.Margin = new Padding(0, 0, 6, 6);
        _assetSearch.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _assetSearch.ForeColor = System.Drawing.Color.White;
        _assetSearch.BorderStyle = BorderStyle.FixedSingle;
        _assetSearch.PlaceholderText = "Search animations...";
        _assetSearch.TextChanged += (_, _) =>
        {
            _assetPage = 0;
            PopulateAssetList();
        };

        _assetSort.Dock = DockStyle.Fill;
        _assetSort.Margin = new Padding(0, 0, 6, 6);
        _assetSort.DropDownStyle = ComboBoxStyle.DropDownList;
        _assetSort.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _assetSort.ForeColor = System.Drawing.Color.White;
        _assetSort.Items.AddRange(
            new object[]
            {
                "Name A-Z",
                "Newest First",
                "Oldest First",
                "Favorites First",
            }
        );
        _assetSort.SelectedIndex = 0;
        _assetSort.SelectedIndexChanged += (_, _) =>
        {
            _assetPage = 0;
            PopulateAssetList();
        };

        _favoriteButton.Dock = DockStyle.Fill;
        _favoriteButton.Margin = new Padding(0, 0, 0, 6);
        _favoriteButton.Text = "FAVORITE";
        _favoriteButton.FlatStyle = FlatStyle.Flat;
        _favoriteButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _favoriteButton.ForeColor = System.Drawing.Color.Gainsboro;
        _favoriteButton.FlatAppearance.BorderColor =
            System.Drawing.Color.FromArgb(90, 78, 81);
        _favoriteButton.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            8,
            FontStyle.Bold
        );
        _favoriteButton.Enabled = false;
        _favoriteButton.Click += (_, _) => ToggleFavorite();

        filterBar.Controls.Add(_assetSearch, 0, 0);
        filterBar.Controls.Add(_assetSort, 1, 0);
        filterBar.Controls.Add(_favoriteButton, 2, 0);

        var pager = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0),
            BackColor = System.Drawing.Color.FromArgb(38, 32, 34),
        };

        _previousPageButton.Text = "<";
        _previousPageButton.Size = new Size(42, 28);
        _previousPageButton.FlatStyle = FlatStyle.Flat;
        _previousPageButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _previousPageButton.ForeColor = System.Drawing.Color.Gainsboro;
        _previousPageButton.Click += (_, _) =>
        {
            if (_assetPage <= 0)
            {
                return;
            }

            _assetPage--;
            PopulateAssetList();
        };

        _pageLabel.AutoSize = false;
        _pageLabel.Size = new Size(245, 28);
        _pageLabel.ForeColor = System.Drawing.Color.Silver;
        _pageLabel.TextAlign = ContentAlignment.MiddleCenter;

        _nextPageButton.Text = ">";
        _nextPageButton.Size = new Size(42, 28);
        _nextPageButton.FlatStyle = FlatStyle.Flat;
        _nextPageButton.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _nextPageButton.ForeColor = System.Drawing.Color.Gainsboro;
        _nextPageButton.Click += (_, _) =>
        {
            _assetPage++;
            PopulateAssetList();
        };

        pager.Controls.Add(_previousPageButton);
        pager.Controls.Add(_pageLabel);
        pager.Controls.Add(_nextPageButton);

        assetHost.Controls.Add(filterBar, 0, 0);
        assetHost.Controls.Add(_assetList, 0, 1);
        assetHost.Controls.Add(pager, 0, 2);
        assetPanel.Controls.Add(assetHost, 0, 1);
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

    private async Task ImportZipAsync()
    {
        if (_importCancellation != null)
        {
            return;
        }

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

        _importCancellation = new CancellationTokenSource();
        var cancellationToken = _importCancellation.Token;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _importZipButton.Enabled = false;
        _cancelImportButton.Enabled = true;
        _cancelImportButton.Text = "CANCEL";
        _importProgress.Visible = true;
        _importProgress.Style = ProgressBarStyle.Marquee;
        _importProgress.MarqueeAnimationSpeed = 30;
        _importProgressLabel.Visible = true;
        _importProgressLabel.Text = "Opening ZIP...";
        _status.Text =
            $"Opening {Path.GetFileName(dialog.FileName)} in the background. " +
            "The editor remains usable while frames are processed.";

        var progress = new Progress<ZipImportProgress>(
            value =>
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (value.TotalUnits > 0)
                {
                    if (_importProgress.Style != ProgressBarStyle.Blocks)
                    {
                        _importProgress.Style = ProgressBarStyle.Blocks;
                    }

                    var percent = Math.Clamp(
                        (int)Math.Round(
                            value.CompletedUnits * 100d / value.TotalUnits
                        ),
                        0,
                        100
                    );

                    _importProgress.Value = percent;

                    var etaText = string.Empty;
                    if (value.CompletedUnits > 0 &&
                        value.CompletedUnits < value.TotalUnits &&
                        stopwatch.Elapsed.TotalSeconds >= 1d)
                    {
                        var unitsPerSecond =
                            value.CompletedUnits / stopwatch.Elapsed.TotalSeconds;

                        if (unitsPerSecond > 0.001d)
                        {
                            var remainingSeconds =
                                (value.TotalUnits - value.CompletedUnits) /
                                unitsPerSecond;

                            var eta = TimeSpan.FromSeconds(
                                Math.Max(0d, remainingSeconds)
                            );

                            etaText = eta.TotalHours >= 1d
                                ? $" | ETA {eta:hh\\:mm\\:ss}"
                                : $" | ETA {eta:mm\\:ss}";
                        }
                    }

                    var itemText = string.IsNullOrWhiteSpace(value.CurrentItem)
                        ? string.Empty
                        : $" | {value.CurrentItem}";

                    var currentText =
                        value.CurrentTotal > 0
                            ? $" | {value.CurrentIndex:N0}/{value.CurrentTotal:N0}"
                            : string.Empty;

                    _importProgressLabel.Text =
                        $"{percent}% | {value.Stage}{currentText}{etaText}";
                    _status.Text =
                        $"{value.Stage}{itemText}{currentText} " +
                        $"({value.CompletedUnits:N0}/{value.TotalUnits:N0} work units)";
                }
                else
                {
                    _importProgressLabel.Text = value.Stage;
                    _status.Text = value.Stage;
                }
            }
        );

        try
        {
            var result = await Task.Run(
                () => ProcessZipImport(
                    dialog.FileName,
                    progress,
                    cancellationToken
                ),
                cancellationToken
            );

            if (IsDisposed || Disposing)
            {
                return;
            }

            if (!result.Cancelled)
            {
                _importProgress.Style = ProgressBarStyle.Blocks;
                _importProgress.Value = 100;
                _importProgressLabel.Text =
                    $"100% | Complete | {stopwatch.Elapsed:mm\\:ss}";
            }

            // The expensive ZIP/image work happens off the UI thread. Refresh
            // the final list only once after the import has completed/cancelled.
            await ReloadAssetsAsync();

            _status.Text = result.Cancelled
                ? $"Import cancelled. {result.ImportedAnimations:N0} completed " +
                  $"animation(s) remain available in animationimport; " +
                  $"{result.Skipped:N0} skipped."
                : $"ZIP imported: {result.ImportedAnimations:N0} animation(s). " +
                  $"{result.CombinedAnimations:N0} sequence(s) combined from " +
                  $"{result.CombinedFrames:N0} frame image(s), " +
                  $"{result.StandaloneSheets:N0} ready-made sheet(s), " +
                  $"{result.RenamedDuplicates:N0} duplicate(s) renamed, " +
                  $"{result.Skipped:N0} skipped.";
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
            {
                _status.Text =
                    "Import cancelled. Completed files remain available in animationimport.";
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
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
        finally
        {
            stopwatch.Stop();

            _importCancellation?.Dispose();
            _importCancellation = null;

            if (!IsDisposed && !Disposing)
            {
                _importZipButton.Enabled = true;
                _cancelImportButton.Enabled = false;
                _cancelImportButton.Text = "CANCEL";
                _importProgress.MarqueeAnimationSpeed = 0;
            }
        }
    }

    private ZipImportResult ProcessZipImport(
        string zipPath,
        IProgress<ZipImportProgress> progress,
        CancellationToken cancellationToken
    )
    {
        var result = new ZipImportResult();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress.Report(
                new ZipImportProgress
                {
                    CompletedUnits = 0,
                    TotalUnits = 0,
                    Stage = "Reading ZIP directory...",
                }
            );

            using var archive = ZipFile.OpenRead(zipPath);
            var pngEntries = archive.Entries
                .Where(entry =>
                    entry.Length > 0 &&
                    entry.FullName.EndsWith(
                        ".png",
                        StringComparison.OrdinalIgnoreCase
                    ))
                .ToArray();

            cancellationToken.ThrowIfCancellationRequested();

            var frameEntries = pngEntries
                .Select(
                    entry =>
                        TryCreateZipFrameEntry(entry, out var frame)
                            ? frame
                            : null
                )
                .Where(frame => frame != null)
                .Cast<ZipFrameEntry>()
                .ToArray();

            var sequenceGroups = frameEntries
                .GroupBy(
                    frame => $"{frame.Directory}\n{frame.BaseName}",
                    StringComparer.OrdinalIgnoreCase
                )
                .Where(group =>
                    group.Count() >= 2 &&
                    group.Select(frame => frame.FrameIndex)
                        .Distinct()
                        .Count() >= 2)
                .Select(group =>
                    group
                        .OrderBy(frame => frame.FrameIndex)
                        .ThenBy(
                            frame => frame.Entry.FullName,
                            StringComparer.OrdinalIgnoreCase
                        )
                        .ToArray())
                .OrderBy(
                    group => group[0].Entry.FullName,
                    StringComparer.OrdinalIgnoreCase
                )
                .ToArray();

            // A combined sequence uses two streaming passes: one to discover
            // the maximum cell size and one to draw. Standalone images use one
            // unit. This gives the progress bar useful movement even when one
            // animation contains hundreds of frames.
            var totalUnits = Math.Max(
                1,
                sequenceGroups.Sum(group => group.Length * 2) +
                pngEntries.Length
            );
            var completedUnits = 0;

            progress.Report(
                new ZipImportProgress
                {
                    CompletedUnits = 0,
                    TotalUnits = totalUnits,
                    Stage =
                        $"Analyzed {pngEntries.Length:N0} PNG(s), " +
                        $"{sequenceGroups.Length:N0} sequence(s)",
                }
            );

            var consumedEntries =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var groupIndex = 0; groupIndex < sequenceGroups.Length; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = sequenceGroups[groupIndex];
                var sourceName = group[0].BaseName;

                try
                {
                    using var sheet = CombineZipFrames(
                        group,
                        cancellationToken,
                        (phase, frameIndex, frameTotal) =>
                        {
                            completedUnits++;
                            progress.Report(
                                new ZipImportProgress
                                {
                                    CompletedUnits = Math.Min(
                                        completedUnits,
                                        totalUnits
                                    ),
                                    TotalUnits = totalUnits,
                                    Stage =
                                        phase == 0
                                            ? "Measuring frames"
                                            : "Combining frames",
                                    CurrentItem = sourceName,
                                    CurrentIndex = frameIndex,
                                    CurrentTotal = frameTotal,
                                }
                            );
                        },
                        out var xFrames,
                        out var yFrames,
                        out var frameCount
                    );

                    cancellationToken.ThrowIfCancellationRequested();

                    var folderHint = group[0].Directory
                        .Replace('/', ' ')
                        .Replace('\\', ' ');
                    var categoryName = ClassifyAnimation(
                        $"{sourceName} {folderHint}",
                        sheet
                    );
                    var categoryDirectory =
                        Path.Combine(_importRoot, categoryName);
                    Directory.CreateDirectory(categoryDirectory);

                    var safeName = MakeFileStem(sourceName);
                    if (string.IsNullOrWhiteSpace(safeName))
                    {
                        safeName = "animation";
                    }

                    var requestedDestination = Path.Combine(
                        categoryDirectory,
                        safeName + ".png"
                    );
                    var destination = GetUniquePath(
                        requestedDestination
                    );

                    if (!string.Equals(
                            destination,
                            requestedDestination,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        result.RenamedDuplicates++;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    sheet.Save(destination, ImageFormat.Png);
                    WriteGridMetadata(
                        destination,
                        xFrames,
                        yFrames,
                        frameCount
                    );

                    foreach (var frame in group)
                    {
                        consumedEntries.Add(frame.Entry.FullName);
                    }

                    result.ImportedAnimations++;
                    result.CombinedAnimations++;
                    result.CombinedFrames += frameCount;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // If a sequence cannot be merged, its entries are still
                    // attempted below as standalone PNGs.
                    result.Skipped++;
                }
            }

            for (var entryIndex = 0; entryIndex < pngEntries.Length; entryIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = pngEntries[entryIndex];

                completedUnits++;
                progress.Report(
                    new ZipImportProgress
                    {
                        CompletedUnits = Math.Min(
                            completedUnits,
                            totalUnits
                        ),
                        TotalUnits = totalUnits,
                        Stage = consumedEntries.Contains(entry.FullName)
                            ? "Finalizing sequences"
                            : "Importing standalone sheets",
                        CurrentItem = entry.FullName,
                        CurrentIndex = entryIndex + 1,
                        CurrentTotal = pngEntries.Length,
                    }
                );

                if (consumedEntries.Contains(entry.FullName))
                {
                    continue;
                }

                try
                {
                    using var stream = entry.Open();
                    using var buffer = new MemoryStream();
                    CopyStreamWithCancellation(
                        stream,
                        buffer,
                        cancellationToken
                    );
                    buffer.Position = 0;

                    using var bitmap = new Bitmap(buffer);
                    var sourceName =
                        Path.GetFileNameWithoutExtension(entry.Name);
                    var directoryHint = GetZipDirectory(entry.FullName)
                        .Replace('/', ' ')
                        .Replace('\\', ' ');
                    var categoryName = ClassifyAnimation(
                        $"{sourceName} {directoryHint}",
                        bitmap
                    );
                    var categoryDirectory =
                        Path.Combine(_importRoot, categoryName);
                    Directory.CreateDirectory(categoryDirectory);

                    var fileName = Path.GetFileName(entry.Name);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var requestedDestination = Path.Combine(
                        categoryDirectory,
                        fileName
                    );
                    var destination = GetUniquePath(
                        requestedDestination
                    );

                    if (!string.Equals(
                            destination,
                            requestedDestination,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        result.RenamedDuplicates++;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    buffer.Position = 0;

                    try
                    {
                        using var output = File.Create(destination);
                        CopyStreamWithCancellation(
                            buffer,
                            output,
                            cancellationToken
                        );
                    }
                    catch
                    {
                        try
                        {
                            if (File.Exists(destination))
                            {
                                File.Delete(destination);
                            }
                        }
                        catch
                        {
                            // Best effort cleanup of a cancelled/failed copy.
                        }

                        throw;
                    }

                    result.ImportedAnimations++;
                    result.StandaloneSheets++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    result.Skipped++;
                }
            }

            progress.Report(
                new ZipImportProgress
                {
                    CompletedUnits = totalUnits,
                    TotalUnits = totalUnits,
                    Stage = "Import complete",
                    CurrentIndex = pngEntries.Length,
                    CurrentTotal = pngEntries.Length,
                }
            );
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        return result;
    }

    private static void CopyStreamWithCancellation(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[128 * 1024];
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
        }
    }

    private static bool TryCreateZipFrameEntry(
        ZipArchiveEntry entry,
        out ZipFrameEntry? frame
    )
    {
        frame = null;

        var stem = Path.GetFileNameWithoutExtension(entry.Name);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return false;
        }

        var directory = GetZipDirectory(entry.FullName);

        // Common asset-pack conventions:
        // skill_eff_413_1.png, skill_eff_413-2.png, frame 003.png, (04).png
        var match = Regex.Match(
            stem,
            @"^(?<base>.*?)(?:[_\-\s]+|\()(?<index>\d+)\)?$",
            RegexOptions.CultureInvariant
        );

        string baseName;
        int frameIndex;

        if (match.Success &&
            int.TryParse(match.Groups["index"].Value, out frameIndex))
        {
            baseName = match.Groups["base"].Value.Trim(' ', '_', '-', '(', ')');
        }
        else if (int.TryParse(stem.Trim(' ', '(', ')'), out frameIndex))
        {
            // Some packs simply use 1.png, 2.png, 3.png inside one animation folder.
            baseName = Path.GetFileName(
                directory.TrimEnd('/', '\\')
            );
        }
        else
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "animation";
        }

        frame = new ZipFrameEntry
        {
            Entry = entry,
            Directory = directory,
            BaseName = baseName,
            FrameIndex = frameIndex,
        };
        return true;
    }

    private static string GetZipDirectory(string fullName)
    {
        var normalized = fullName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }

    private static Bitmap CombineZipFrames(
        IReadOnlyList<ZipFrameEntry> frames,
        CancellationToken cancellationToken,
        Action<int, int, int>? onProgress,
        out int xFrames,
        out int yFrames,
        out int frameCount
    )
    {
        if (frames.Count < 2)
        {
            throw new InvalidDataException(
                "At least two frame images are required."
            );
        }

        frameCount = frames.Count;

        // Build a near-square grid so large effects use both columns and rows
        // instead of producing extremely wide spritesheets.
        xFrames = Math.Clamp(
            (int)Math.Ceiling(Math.Sqrt(frameCount)),
            1,
            32
        );
        yFrames = (int)Math.Ceiling(frameCount / (double)xFrames);

        if (yFrames > 32)
        {
            xFrames = 32;
            yFrames = (int)Math.Ceiling(frameCount / 32d);
        }

        if (yFrames > 32)
        {
            throw new InvalidDataException(
                $"Sequence has {frameCount} frames; the importer supports up to 1024 frames."
            );
        }

        var cellWidth = 0;
        var cellHeight = 0;

        // First streaming pass only measures each frame. This keeps memory usage
        // essentially constant even for ZIPs containing hundreds of large PNGs.
        for (var i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stream = frames[i].Entry.Open();
            using var source = new Bitmap(stream);

            cellWidth = Math.Max(cellWidth, source.Width);
            cellHeight = Math.Max(cellHeight, source.Height);

            onProgress?.Invoke(0, i + 1, frames.Count);
        }

        if (cellWidth <= 0 || cellHeight <= 0)
        {
            throw new InvalidDataException(
                "Animation frame dimensions are invalid."
            );
        }

        var sheetWidth = checked(cellWidth * xFrames);
        var sheetHeight = checked(cellHeight * yFrames);
        var output = new Bitmap(
            sheetWidth,
            sheetHeight,
            PixelFormat.Format32bppArgb
        );

        try
        {
            using var graphics = Graphics.FromImage(output);
            graphics.Clear(System.Drawing.Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;

            // Second streaming pass draws one frame at a time. No full list of
            // decoded bitmaps is retained, which is important for colossal ZIPs.
            for (var i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = frames[i].Entry.Open();
                using var source = new Bitmap(stream);

                var column = i % xFrames;
                var row = i / xFrames;

                var x =
                    column * cellWidth +
                    (cellWidth - source.Width) / 2;
                var y =
                    row * cellHeight +
                    (cellHeight - source.Height) / 2;

                graphics.DrawImageUnscaled(source, x, y);
                onProgress?.Invoke(1, i + 1, frames.Count);
            }

            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static string GridMetadataPath(string pngPath) => pngPath + ".grid";

    private static void WriteGridMetadata(
        string pngPath,
        int xFrames,
        int yFrames,
        int frameCount
    )
    {
        File.WriteAllText(
            GridMetadataPath(pngPath),
            $"{xFrames}x{yFrames};frames={frameCount}"
        );
    }

    private static bool TryReadGridMetadata(
        string pngPath,
        out int xFrames,
        out int yFrames,
        out int frameCount
    )
    {
        xFrames = 1;
        yFrames = 1;
        frameCount = 1;

        var metadataPath = GridMetadataPath(pngPath);
        if (!File.Exists(metadataPath))
        {
            return false;
        }

        try
        {
            var value = File.ReadAllText(metadataPath).Trim();
            var match = Regex.Match(
                value,
                @"^(?<x>\d+)x(?<y>\d+);frames=(?<frames>\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            if (!match.Success ||
                !int.TryParse(match.Groups["x"].Value, out xFrames) ||
                !int.TryParse(match.Groups["y"].Value, out yFrames) ||
                !int.TryParse(match.Groups["frames"].Value, out frameCount))
            {
                return false;
            }

            xFrames = Math.Clamp(xFrames, 1, 32);
            yFrames = Math.Clamp(yFrames, 1, 32);
            frameCount = Math.Clamp(frameCount, 1, xFrames * yFrames);
            return true;
        }
        catch
        {
            return false;
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

    private async Task ReloadAssetsAsync()
    {
        if (_libraryLoading)
        {
            _libraryLoadCancellation?.Cancel();
        }

        _libraryLoadCancellation?.Cancel();
        _libraryLoadCancellation?.Dispose();
        _libraryLoadCancellation = new CancellationTokenSource();
        var cancellationToken = _libraryLoadCancellation.Token;
        _libraryLoading = true;

        var selectedCategory = _categoryList.SelectedItem?.ToString();
        _status.Text =
            "Indexing animation library in the background... " +
            "PNG files are not decoded during startup.";

        var progress = new Progress<int>(
            count =>
            {
                if (!IsDisposed && !Disposing)
                {
                    _status.Text =
                        $"Indexing animation library... {count:N0} file(s) found.";
                }
            }
        );

        try
        {
            var assets = await Task.Run(
                () => BuildAssetIndex(progress, cancellationToken),
                cancellationToken
            );

            cancellationToken.ThrowIfCancellationRequested();

            _assets.Clear();
            _assets.AddRange(assets);

            _categoryList.BeginUpdate();
            try
            {
                _categoryList.Items.Clear();
                _categoryList.Items.Add("All");
                _categoryList.Items.Add(FavoritesView);
                _categoryList.Items.Add(NewView);

                var categories = _assets
                    .Select(asset => asset.Category)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(categoryName =>
                    {
                        var index = Array.FindIndex(
                            Categories,
                            value => string.Equals(
                                value,
                                categoryName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                        return index < 0 ? int.MaxValue : index;
                    })
                    .ThenBy(
                        value => value,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToArray();

                foreach (var categoryName in categories)
                {
                    _categoryList.Items.Add(categoryName);
                }
            }
            finally
            {
                _categoryList.EndUpdate();
            }

            if (!string.IsNullOrWhiteSpace(selectedCategory))
            {
                var selectedIndex =
                    _categoryList.FindStringExact(selectedCategory);

                if (selectedIndex >= 0)
                {
                    _categoryList.SelectedIndex = selectedIndex;
                }
            }

            if (_categoryList.SelectedIndex < 0 &&
                _categoryList.Items.Count > 0)
            {
                _categoryList.SelectedIndex = 0;
            }

            _assetPage = 0;
            PopulateAssetList();

            _status.Text =
                $"Indexed {_assets.Count:N0} animation sheet(s). " +
                $"Only {AssetPageSize:N0} are rendered per page.";
        }
        catch (OperationCanceledException)
        {
            // A newer refresh request replaced this one.
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                MessageBox.Show(
                    this,
                    "Unable to index animation library: " + ex.Message,
                    "Animations Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        finally
        {
            _libraryLoading = false;
        }
    }

    private List<AnimationAsset> BuildAssetIndex(
        IProgress<int> progress,
        CancellationToken cancellationToken
    )
    {
        var assets = new List<AnimationAsset>();
        var count = 0;

        foreach (var file in Directory.EnumerateFiles(
                     _importRoot,
                     "*.png",
                     SearchOption.AllDirectories
                 ))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var categoryName =
                    Path.GetFileName(Path.GetDirectoryName(file)) ?? "Misc";
                var fileName = Path.GetFileName(file);
                var stem = Path.GetFileNameWithoutExtension(file);

                var metadataLoaded = TryReadGridMetadata(
                    file,
                    out var xFrames,
                    out var yFrames,
                    out var frameCount
                );

                assets.Add(
                    new AnimationAsset
                    {
                        FilePath = file,
                        FileName = fileName,
                        SuggestedName = FriendlyName(stem),
                        Category = categoryName,
                        XFrames = metadataLoaded ? xFrames : 1,
                        YFrames = metadataLoaded ? yFrames : 1,
                        FrameCount = metadataLoaded ? frameCount : 1,
                        MetadataLoaded = metadataLoaded,
                        LastWriteTimeUtc = File.GetLastWriteTimeUtc(file),
                    }
                );

                count++;
                if (count % 250 == 0)
                {
                    progress.Report(count);
                }
            }
            catch
            {
                // Ignore invalid paths/metadata. Image decoding is deferred
                // until the item is actually visible or selected.
            }
        }

        progress.Report(count);
        return assets;
    }

    private void PopulateAssetList()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = new CancellationTokenSource();

        var cancellationToken = _thumbnailCancellation.Token;
        var categoryName = _categoryList.SelectedItem?.ToString();
        var search = _assetSearch.Text.Trim();

        IEnumerable<AnimationAsset> query = _assets;

        if (string.Equals(
                categoryName,
                FavoritesView,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            query = query.Where(IsFavorite);
        }
        else if (!string.IsNullOrWhiteSpace(categoryName) &&
                 !string.Equals(
                     categoryName,
                     "All",
                     StringComparison.OrdinalIgnoreCase
                 ) &&
                 !string.Equals(
                     categoryName,
                     NewView,
                     StringComparison.OrdinalIgnoreCase
                 ))
        {
            query = query.Where(
                asset => string.Equals(
                    asset.Category,
                    categoryName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(
                asset =>
                    asset.SuggestedName.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    asset.FileName.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    asset.Category.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        var sortMode = _assetSort.SelectedItem?.ToString() ?? "Name A-Z";

        IOrderedEnumerable<AnimationAsset> ordered;
        if (string.Equals(
                categoryName,
                NewView,
                StringComparison.OrdinalIgnoreCase
            ) ||
            string.Equals(
                sortMode,
                "Newest First",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            ordered = query
                .OrderByDescending(asset => asset.LastWriteTimeUtc)
                .ThenBy(
                    asset => asset.SuggestedName,
                    StringComparer.OrdinalIgnoreCase
                );
        }
        else if (string.Equals(
                     sortMode,
                     "Oldest First",
                     StringComparison.OrdinalIgnoreCase
                 ))
        {
            ordered = query
                .OrderBy(asset => asset.LastWriteTimeUtc)
                .ThenBy(
                    asset => asset.SuggestedName,
                    StringComparer.OrdinalIgnoreCase
                );
        }
        else if (string.Equals(
                     sortMode,
                     "Favorites First",
                     StringComparison.OrdinalIgnoreCase
                 ))
        {
            ordered = query
                .OrderByDescending(IsFavorite)
                .ThenByDescending(asset => asset.LastWriteTimeUtc)
                .ThenBy(
                    asset => asset.SuggestedName,
                    StringComparer.OrdinalIgnoreCase
                );
        }
        else
        {
            ordered = query.OrderBy(
                asset => asset.SuggestedName,
                StringComparer.OrdinalIgnoreCase
            );
        }

        var filtered = ordered.ToArray();

        var pageCount = Math.Max(
            1,
            (int)Math.Ceiling(filtered.Length / (double)AssetPageSize)
        );

        _assetPage = Math.Clamp(_assetPage, 0, pageCount - 1);

        _visibleAssets.Clear();
        _visibleAssets.AddRange(
            filtered
                .Skip(_assetPage * AssetPageSize)
                .Take(AssetPageSize)
        );

        _assetList.BeginUpdate();
        try
        {
            _assetList.Items.Clear();
            _assetImages.Images.Clear();

            using var placeholder = CreatePlaceholderThumbnail();
            _assetImages.Images.Add(
                "__placeholder",
                new Bitmap(placeholder)
            );

            foreach (var asset in _visibleAssets)
            {
                var favoritePrefix = IsFavorite(asset) ? "* " : string.Empty;
                var addedText = asset.LastWriteTimeUtc == DateTime.MinValue
                    ? "Unknown"
                    : asset.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                var item = new ListViewItem(
                    favoritePrefix + asset.SuggestedName
                )
                {
                    ImageKey = "__placeholder",
                    Tag = asset,
                    ToolTipText = asset.MetadataLoaded
                        ? $"{asset.FileName}\n{asset.Category}\n" +
                          $"Grid: {asset.XFrames}x{asset.YFrames} " +
                          $"({asset.FrameCount} frames)\n" +
                          $"Added/updated: {addedText}"
                        : $"{asset.FileName}\n{asset.Category}\n" +
                          $"Grid: loads on demand\n" +
                          $"Added/updated: {addedText}",
                };

                _assetList.Items.Add(item);
            }
        }
        finally
        {
            _assetList.EndUpdate();
        }

        _previousPageButton.Enabled = _assetPage > 0;
        _nextPageButton.Enabled = _assetPage + 1 < pageCount;
        _pageLabel.Text =
            $"Page {_assetPage + 1:N0}/{pageCount:N0} - " +
            $"{filtered.Length:N0} animation(s)";

        if (_assetList.Items.Count > 0)
        {
            _assetList.Items[0].Selected = true;
        }
        else
        {
            _selectedAsset = null;
            _preview.SetAnimation(null, 1, 1);
            _assetInfo.Text = string.Empty;
            UpdateFavoriteButton();
        }

        _ = LoadPageThumbnailsAsync(cancellationToken);
    }

    private async Task LoadPageThumbnailsAsync(
        CancellationToken cancellationToken
    )
    {
        var pageAssets = _visibleAssets.ToArray();

        for (var i = 0; i < pageAssets.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var asset = pageAssets[i];
            Bitmap? thumbnail = null;

            try
            {
                thumbnail = await Task.Run(
                    () => GetOrCreateThumbnail(
                        asset,
                        cancellationToken
                    ),
                    cancellationToken
                );

                cancellationToken.ThrowIfCancellationRequested();

                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (!_visibleAssets.Contains(asset))
                {
                    continue;
                }

                if (!_assetImages.Images.ContainsKey(asset.FilePath))
                {
                    _assetImages.Images.Add(
                        asset.FilePath,
                        new Bitmap(thumbnail)
                    );
                }

                foreach (ListViewItem item in _assetList.Items)
                {
                    if (!ReferenceEquals(item.Tag, asset))
                    {
                        continue;
                    }

                    item.ImageKey = asset.FilePath;
                    item.ToolTipText =
                        $"{asset.FileName}\n{asset.Category}\n" +
                        $"Grid: {asset.XFrames}x{asset.YFrames} " +
                        $"({asset.FrameCount} frames)";
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // A bad or huge image should not block the rest of the page.
            }
            finally
            {
                thumbnail?.Dispose();
            }
        }
    }

    private Bitmap GetOrCreateThumbnail(
        AnimationAsset asset,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cachePath = GetThumbnailCachePath(asset);
        try
        {
            if (File.Exists(cachePath) &&
                File.GetLastWriteTimeUtc(cachePath) >=
                File.GetLastWriteTimeUtc(asset.FilePath))
            {
                using var cached = new Bitmap(cachePath);
                return new Bitmap(cached);
            }
        }
        catch
        {
            // Rebuild stale/corrupt cache entries below.
        }

        using var source = new Bitmap(asset.FilePath);
        EnsureAssetMetadata(asset, source);

        using var generated = CreateThumbnail(asset, source);

        try
        {
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            generated.Save(cachePath, ImageFormat.Png);
        }
        catch
        {
            // Thumbnail cache is an optimization only.
        }

        return new Bitmap(generated);
    }

    private string GetThumbnailCachePath(AnimationAsset asset)
    {
        var category = MakeFileStem(asset.Category);
        if (string.IsNullOrWhiteSpace(category))
        {
            category = "Misc";
        }

        return Path.Combine(
            _thumbnailRoot,
            category,
            asset.FileName + ".thumb.png"
        );
    }

    private static Bitmap CreatePlaceholderThumbnail()
    {
        const int size = 112;
        var output = new Bitmap(
            size,
            size,
            PixelFormat.Format32bppArgb
        );

        using var graphics = Graphics.FromImage(output);
        graphics.Clear(System.Drawing.Color.FromArgb(38, 32, 34));

        using var pen = new Pen(
            System.Drawing.Color.FromArgb(80, 80, 80),
            2f
        );
        graphics.DrawRectangle(pen, 12, 12, size - 25, size - 25);

        return output;
    }

    private static Bitmap CreateThumbnail(
        AnimationAsset asset,
        Bitmap bitmap
    )
    {
        const int size = 112;
        var output = new Bitmap(
            size,
            size,
            PixelFormat.Format32bppArgb
        );

        var frameWidth = Math.Max(
            1,
            bitmap.Width / Math.Max(1, asset.XFrames)
        );
        var frameHeight = Math.Max(
            1,
            bitmap.Height / Math.Max(1, asset.YFrames)
        );
        var source = new Rectangle(
            0,
            0,
            Math.Min(frameWidth, bitmap.Width),
            Math.Min(frameHeight, bitmap.Height)
        );

        using var graphics = Graphics.FromImage(output);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var scale = Math.Min(
            (double)(size - 8) / source.Width,
            (double)(size - 8) / source.Height
        );
        var width = Math.Max(
            1,
            (int)Math.Round(source.Width * scale)
        );
        var height = Math.Max(
            1,
            (int)Math.Round(source.Height * scale)
        );
        var destination = new Rectangle(
            (size - width) / 2,
            (size - height) / 2,
            width,
            height
        );

        graphics.DrawImage(
            bitmap,
            destination,
            source,
            GraphicsUnit.Pixel
        );

        return output;
    }

    private static void EnsureAssetMetadata(
        AnimationAsset asset,
        Bitmap bitmap
    )
    {
        if (asset.MetadataLoaded)
        {
            return;
        }

        lock (asset)
        {
            if (asset.MetadataLoaded)
            {
                return;
            }

            var stem = Path.GetFileNameWithoutExtension(
                asset.FileName
            );
            var (xFrames, yFrames) = DetectGrid(bitmap, stem);

            asset.XFrames = Math.Max(1, xFrames);
            asset.YFrames = Math.Max(1, yFrames);
            asset.FrameCount = Math.Max(
                1,
                asset.XFrames * asset.YFrames
            );
            asset.MetadataLoaded = true;

            try
            {
                WriteGridMetadata(
                    asset.FilePath,
                    asset.XFrames,
                    asset.YFrames,
                    asset.FrameCount
                );
            }
            catch
            {
                // Metadata persistence is an optimization only.
            }
        }
    }

    private async Task SelectAssetAsync()
    {
        if (_assetList.SelectedItems.Count == 0)
        {
            return;
        }

        var asset =
            _assetList.SelectedItems[0].Tag as AnimationAsset;

        if (asset == null)
        {
            return;
        }

        _selectionCancellation?.Cancel();
        _selectionCancellation?.Dispose();
        _selectionCancellation = new CancellationTokenSource();
        var cancellationToken = _selectionCancellation.Token;

        _selectedAsset = asset;
        UpdateFavoriteButton();
        _name.Text = asset.SuggestedName;
        _category.Text = asset.Category;
        _assetInfo.Text = $"Loading {asset.FileName}...";

        AssetPreviewLoad? loaded = null;

        try
        {
            loaded = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var source = new Bitmap(asset.FilePath);
                    EnsureAssetMetadata(asset, source);

                    cancellationToken.ThrowIfCancellationRequested();

                    return new AssetPreviewLoad
                    {
                        Bitmap = new Bitmap(source),
                        Width = source.Width,
                        Height = source.Height,
                        XFrames = asset.XFrames,
                        YFrames = asset.YFrames,
                        FrameCount = asset.FrameCount,
                    };
                },
                cancellationToken
            );

            cancellationToken.ThrowIfCancellationRequested();

            if (!ReferenceEquals(_selectedAsset, asset))
            {
                loaded.Bitmap.Dispose();
                return;
            }

            _xFrames.Value = Math.Clamp(
                loaded.XFrames,
                (int)_xFrames.Minimum,
                (int)_xFrames.Maximum
            );
            _yFrames.Value = Math.Clamp(
                loaded.YFrames,
                (int)_yFrames.Minimum,
                (int)_yFrames.Maximum
            );
            _frameCount.Maximum = Math.Max(
                1,
                (int)_xFrames.Value * (int)_yFrames.Value
            );
            _frameCount.Value = Math.Min(
                _frameCount.Maximum,
                Math.Max(1, loaded.FrameCount)
            );
            _frameDuration.Value = 80;

            _preview.SetAnimation(
                loaded.Bitmap,
                (int)_xFrames.Value,
                (int)_yFrames.Value
            );
            loaded = null;

            _previewTimer.Interval =
                (int)_frameDuration.Value;

            var addedText = asset.LastWriteTimeUtc == DateTime.MinValue
                ? "Unknown"
                : asset.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            _assetInfo.Text =
                $"{asset.FileName}   |   {asset.Category}   |   " +
                $"{asset.XFrames}x{asset.YFrames} grid   |   " +
                $"{asset.FrameCount} frame(s)   |   Added {addedText}";
        }
        catch (OperationCanceledException)
        {
            loaded?.Bitmap.Dispose();
        }
        catch (Exception ex)
        {
            loaded?.Bitmap.Dispose();

            if (!IsDisposed && !Disposing)
            {
                _assetInfo.Text =
                    $"Unable to load {asset.FileName}: {ex.Message}";
            }
        }
    }

    private string GetFavoriteKey(string filePath)
    {
        try
        {
            return Path.GetRelativePath(_importRoot, filePath)
                .Replace('\\', '/');
        }
        catch
        {
            return filePath.Replace('\\', '/');
        }
    }

    private bool IsFavorite(AnimationAsset asset) =>
        _favoriteAssetKeys.Contains(GetFavoriteKey(asset.FilePath));

    private void LoadFavorites()
    {
        _favoriteAssetKeys.Clear();

        try
        {
            if (!File.Exists(_favoritesPath))
            {
                return;
            }

            foreach (var line in File.ReadLines(_favoritesPath))
            {
                var key = line.Trim().Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _favoriteAssetKeys.Add(key);
                }
            }
        }
        catch
        {
            // Favorites are convenience metadata only.
        }
    }

    private void SaveFavorites()
    {
        try
        {
            var directory = Path.GetDirectoryName(_favoritesPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(
                _favoritesPath,
                _favoriteAssetKeys.OrderBy(
                    key => key,
                    StringComparer.OrdinalIgnoreCase
                )
            );
        }
        catch (Exception ex)
        {
            _status.Text =
                "Unable to save animation favorites: " + ex.Message;
        }
    }

    private void ToggleFavorite()
    {
        if (_selectedAsset == null)
        {
            return;
        }

        var key = GetFavoriteKey(_selectedAsset.FilePath);
        if (!_favoriteAssetKeys.Add(key))
        {
            _favoriteAssetKeys.Remove(key);
        }

        SaveFavorites();
        UpdateFavoriteButton();

        _assetPage = 0;
        PopulateAssetList();
    }

    private void UpdateFavoriteButton()
    {
        var hasSelection = _selectedAsset != null;
        _favoriteButton.Enabled = hasSelection;
        _favoriteButton.Text =
            hasSelection && IsFavorite(_selectedAsset!)
                ? "* FAVORITE"
                : "FAVORITE";
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

        var requestedSpriteFile = fileStem + ".png";
        var requestedDestination = Path.Combine(
            _animationsRoot,
            requestedSpriteFile
        );
        var destination = GetUniquePath(requestedDestination);
        var spriteFile = Path.GetFileName(destination);
        var temporary =
            destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var wasRenamed = !string.Equals(
            destination,
            requestedDestination,
            StringComparison.OrdinalIgnoreCase
        );

        try
        {
            File.Copy(_selectedAsset.FilePath, temporary, false);
            File.Move(temporary, destination, false);

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
                $"{(int)_xFrames.Value}x{(int)_yFrames.Value}, {(int)_frameCount.Value} frames." +
                (wasRenamed ? " Existing filename preserved; duplicate was renamed." : string.Empty);

            MessageBox.Show(
                this,
                $"Animation imported.\n\n" +
                $"Name: {animationName}\n" +
                $"Folder: {folder}\n" +
                $"Grid: {(int)_xFrames.Value}x{(int)_yFrames.Value}\n" +
                $"Frames: {(int)_frameCount.Value}\n" +
                $"Sprite: resources\\animations\\{spriteFile}\n" +
                (wasRenamed
                    ? "A file with the requested name already existed, so this copy was renamed and the existing file was kept.\n"
                    : string.Empty) +
                "\nThe Animation Editor will open and prefill the detected settings.",
                "Animations Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }

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
