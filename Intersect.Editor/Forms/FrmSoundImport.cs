using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using DarkUI.Forms;
using Intersect.Editor.Content;

namespace Intersect.Editor.Forms;

public sealed partial class FrmSoundImport : DarkForm
{
    private sealed class SoundAsset
    {
        public required string FilePath { get; init; }

        public required string FileName { get; init; }

        public required string SuggestedName { get; init; }

        public required string Category { get; init; }
        public SoundImportKind Kind { get; init; }

        public long FileSize { get; init; }

        public DateTime LastWriteTimeUtc { get; init; }

        public TimeSpan Duration { get; set; }

        public int SampleRate { get; set; }

        public short Channels { get; set; }

        public short BitsPerSample { get; set; }

        public bool MetadataLoaded { get; set; }
    }

    private sealed class SoundImportProgress
    {
        public int Completed { get; init; }

        public int Total { get; init; }

        public required string Stage { get; init; }

        public string CurrentItem { get; init; } = string.Empty;
    }

    private sealed class SoundImportResult
    {
        public int Imported { get; set; }

        public int Skipped { get; set; }

        public int RenamedDuplicates { get; set; }

        public int Converted { get; set; }
        public List<string> Errors { get; } = [];

        public bool Cancelled { get; set; }
    }

    private static readonly string[] Categories =
    {
        "Combat",
        "Weapons",
        "Magic",
        "UI",
        "Footsteps",
        "Creatures",
        "Voice",
        "Items",
        "Ambient",
        "Nature",
        "Machines",
        "Music FX",
        "Misc",
    };

    private const string FavoritesView = "[Favorites]";
    private const string NewView = "[New]";

    private static readonly Dictionary<string, string[]> CategoryKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Combat"] = new[]
            {
                "hit", "impact", "damage", "hurt", "attack", "combat",
                "punch", "kick", "block", "critical", "crit"
            },
            ["Weapons"] = new[]
            {
                "sword", "blade", "axe", "hammer", "bow", "arrow", "gun",
                "rifle", "shot", "reload", "weapon", "spear", "knife"
            },
            ["Magic"] = new[]
            {
                "magic", "spell", "cast", "mana", "fire", "ice", "frost",
                "lightning", "thunder", "heal", "holy", "dark", "curse",
                "portal", "teleport", "aura"
            },
            ["UI"] = new[]
            {
                "ui", "menu", "click", "select", "confirm", "cancel",
                "button", "notification", "popup", "error", "success"
            },
            ["Footsteps"] = new[]
            {
                "footstep", "footsteps", "step", "walk", "run", "boots"
            },
            ["Creatures"] = new[]
            {
                "monster", "creature", "beast", "dragon", "wolf", "dog",
                "horse", "bird", "roar", "growl", "animal"
            },
            ["Voice"] = new[]
            {
                "voice", "dialog", "dialogue", "speech", "npc", "talk",
                "male", "female", "shout", "whisper", "laugh"
            },
            ["Items"] = new[]
            {
                "item", "inventory", "pickup", "drop", "coin", "gold",
                "potion", "bottle", "chest", "loot", "equip"
            },
            ["Ambient"] = new[]
            {
                "ambient", "ambience", "roomtone", "drone", "loop",
                "background", "crowd", "city", "dungeon"
            },
            ["Nature"] = new[]
            {
                "nature", "wind", "rain", "storm", "water", "river",
                "ocean", "forest", "tree", "grass", "fireplace"
            },
            ["Machines"] = new[]
            {
                "machine", "engine", "motor", "mechanical", "gear",
                "door", "gate", "lever", "elevator"
            },
            ["Music FX"] = new[]
            {
                "stinger", "jingle", "fanfare", "transition", "intro",
                "outro", "musicfx", "music_fx"
            },
        };

    private const int PageSize = 200;

    private readonly string _gameRoot;
    private readonly string _importRoot;
    private readonly ComboBox _libraryKind = new();
    private readonly ComboBox _targetKind = new();
    private readonly Label _destination = new();
    private readonly string _favoritesPath;

    private readonly ListBox _categoryList = new();
    private readonly ListView _soundList = new();
    private readonly TextBox _search = new();
    private readonly ComboBox _sort = new();
    private readonly Button _favoriteButton = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _category = new();
    private readonly Label _details = new();
    private readonly Label _status = new();
    private readonly Label _pageLabel = new();
    private readonly Button _previousPageButton = new();
    private readonly Button _nextPageButton = new();
    private readonly Button _importZipButton = new();
    private readonly Button _importFilesButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _progressLabel = new();
    private readonly Button _playButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _importSelectedButton = new();
    private readonly Button _importFilteredButton = new();

    private readonly List<SoundAsset> _assets = new();
    private readonly List<SoundAsset> _visibleAssets = new();
    private readonly HashSet<string> _favoriteAssetKeys =
        new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _libraryCancellation;
    private CancellationTokenSource? _selectionCancellation;
    private SoundAsset? _selectedAsset;
    private int _page;

    public FrmSoundImport()
    {
        _gameRoot = ResolveGameRoot();
        _importRoot = Path.Combine(_gameRoot, "soundimport");

        _favoritesPath = Path.Combine(
            _gameRoot,
            ".soundimport-cache",
            "favorites.txt"
        );

        Text = "Sound / Voice Sound Import";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1080, 700);
        Size = new Size(1380, 860);
        BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        ForeColor = System.Drawing.Color.Gainsboro;

        Directory.CreateDirectory(_importRoot);
        foreach (var kind in Enum.GetValues<SoundImportKind>())
            Directory.CreateDirectory(SoundImporter.GameFolder(_gameRoot, kind));
        LoadFavorites();
        foreach (var categoryName in Categories)
        {
            foreach (var kind in Enum.GetValues<SoundImportKind>())
                Directory.CreateDirectory(Path.Combine(SoundImporter.LibraryFolder(_importRoot, kind), categoryName));
        }

        BuildInterface();

        Shown += async (_, _) => await ReloadLibraryAsync();
        FormClosed += (_, _) =>
        {
            StopPlayback();

            _operationCancellation?.Cancel();
            _libraryCancellation?.Cancel();
            _selectionCancellation?.Cancel();

            _operationCancellation?.Dispose();
            _libraryCancellation?.Dispose();
            _selectionCancellation?.Dispose();

            _operationCancellation = null;
            _libraryCancellation = null;
            _selectionCancellation = null;
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
            .FirstOrDefault(path =>
                Directory.Exists(Path.Combine(path, "resources")))
            ?? Environment.CurrentDirectory;
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        Controls.Add(root);

        BuildTypeBar(root);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 10, 10, 6),
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };

        ConfigureAccentButton(_importZipButton, "IMPORT ZIP", 126);
        _importZipButton.Click += async (_, _) => await ImportZipAsync();

        ConfigureDarkButton(_importFilesButton, "IMPORT AUDIO FILES", 155);
        _importFilesButton.Click += async (_, _) => await ImportFilesAsync();

        var refresh = CreateDarkButton("REFRESH", 105);
        refresh.Click += async (_, _) => await ReloadLibraryAsync();

        var open = CreateDarkButton("OPEN SOUNDIMPORT", 165);
        open.Click += (_, _) => OpenFolder(_importRoot);

        ConfigureDarkButton(_cancelButton, "CANCEL", 92);
        _cancelButton.Enabled = false;
        _cancelButton.Click += (_, _) =>
        {
            _operationCancellation?.Cancel();
            _cancelButton.Enabled = false;
            _cancelButton.Text = "CANCELLING...";
            _status.Text = "Cancelling after the current sound...";
        };

        _progressBar.Width = 220;
        _progressBar.Height = 22;
        _progressBar.Margin = new Padding(12, 5, 0, 0);
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Visible = false;

        _progressLabel.AutoSize = false;
        _progressLabel.Width = 250;
        _progressLabel.Height = 32;
        _progressLabel.Margin = new Padding(8, 0, 0, 0);
        _progressLabel.ForeColor = System.Drawing.Color.Silver;
        _progressLabel.TextAlign = ContentAlignment.MiddleLeft;
        _progressLabel.Visible = false;

        toolbar.Controls.Add(_importZipButton);
        toolbar.Controls.Add(_importFilesButton);
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(open);
        toolbar.Controls.Add(_cancelButton);
        toolbar.Controls.Add(_progressBar);
        toolbar.Controls.Add(_progressLabel);
        root.Controls.Add(toolbar, 0, 1);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10),
            Margin = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        root.Controls.Add(body, 0, 2);

        var categoryPanel = CreateSection("SUBCATEGORIES");
        StyleListBox(_categoryList);
        _categoryList.Dock = DockStyle.Fill;
        _categoryList.SelectedIndexChanged += (_, _) =>
        {
            _page = 0;
            PopulateSoundList();
        };
        categoryPanel.Controls.Add(_categoryList, 0, 1);
        body.Controls.Add(categoryPanel, 0, 0);

        var libraryPanel = CreateSection("SOUND LIBRARY");
        var libraryHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(38, 32, 34),
        };
        libraryHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        libraryHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        libraryHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        libraryHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

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

        _search.Dock = DockStyle.Fill;
        _search.Margin = new Padding(0, 0, 6, 7);
        _search.PlaceholderText = "Search sounds...";
        StyleTextBox(_search);
        _search.TextChanged += (_, _) =>
        {
            _page = 0;
            PopulateSoundList();
        };

        _sort.Dock = DockStyle.Fill;
        _sort.Margin = new Padding(0, 0, 6, 7);
        _sort.DropDownStyle = ComboBoxStyle.DropDownList;
        _sort.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _sort.ForeColor = System.Drawing.Color.White;
        _sort.Items.AddRange(
            new object[]
            {
                "Name A-Z",
                "Newest First",
                "Oldest First",
                "Favorites First",
            }
        );
        _sort.SelectedIndex = 0;
        _sort.SelectedIndexChanged += (_, _) =>
        {
            _page = 0;
            PopulateSoundList();
        };

        _favoriteButton.Dock = DockStyle.Fill;
        _favoriteButton.Margin = new Padding(0, 0, 0, 7);
        _favoriteButton.Text = "FAVORITE";
        ConfigureDarkButtonStyle(_favoriteButton);
        _favoriteButton.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            8,
            FontStyle.Bold
        );
        _favoriteButton.Enabled = false;
        _favoriteButton.Click += (_, _) => ToggleFavorite();

        filterBar.Controls.Add(_search, 0, 0);
        filterBar.Controls.Add(_sort, 1, 0);
        filterBar.Controls.Add(_favoriteButton, 2, 0);

        _soundList.Dock = DockStyle.Fill;
        _soundList.View = View.Details;
        _soundList.FullRowSelect = true;
        _soundList.MultiSelect = false;
        _soundList.HideSelection = false;
        _soundList.BorderStyle = BorderStyle.None;
        _soundList.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        _soundList.ForeColor = System.Drawing.Color.Gainsboro;
        _soundList.Columns.Add("Name", 245);
        _soundList.Columns.Add("Category", 100);
        _soundList.Columns.Add("Type", 100);
        _soundList.Columns.Add("Duration", 80);
        _soundList.Columns.Add("Size", 80);
        _soundList.SelectedIndexChanged += async (_, _) =>
            await SelectSoundAsync();

        var pager = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0),
            BackColor = System.Drawing.Color.FromArgb(38, 32, 34),
        };

        ConfigureDarkButton(_previousPageButton, "<", 42);
        _previousPageButton.Click += (_, _) =>
        {
            if (_page <= 0)
            {
                return;
            }

            _page--;
            PopulateSoundList();
        };

        _pageLabel.AutoSize = false;
        _pageLabel.Size = new Size(290, 28);
        _pageLabel.ForeColor = System.Drawing.Color.Silver;
        _pageLabel.TextAlign = ContentAlignment.MiddleCenter;

        ConfigureDarkButton(_nextPageButton, ">", 42);
        _nextPageButton.Click += (_, _) =>
        {
            _page++;
            PopulateSoundList();
        };

        pager.Controls.Add(_previousPageButton);
        pager.Controls.Add(_pageLabel);
        pager.Controls.Add(_nextPageButton);

        libraryHost.Controls.Add(filterBar, 0, 0);
        libraryHost.Controls.Add(_soundList, 0, 1);
        libraryHost.Controls.Add(pager, 0, 2);
        libraryPanel.Controls.Add(libraryHost, 0, 1);
        body.Controls.Add(libraryPanel, 1, 0);

        var previewPanel = CreateSection("PREVIEW / DETAILS");
        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            BackColor = System.Drawing.Color.FromArgb(32, 28, 29),
        };

        _details.AutoSize = false;
        _details.Dock = DockStyle.Top;
        _details.Height = 210;
        _details.ForeColor = System.Drawing.Color.Gainsboro;
        _details.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            10
        );

        _playButton.Text = "PLAY";
        _playButton.Location = new System.Drawing.Point(14, 230);
        _playButton.Size = new Size(118, 36);
        ConfigureDarkButtonStyle(_playButton);
        _playButton.Click += (_, _) => PlaySelected();

        _stopButton.Text = "STOP";
        _stopButton.Location = new System.Drawing.Point(142, 230);
        _stopButton.Size = new Size(118, 36);
        ConfigureDarkButtonStyle(_stopButton);
        _stopButton.Click += (_, _) => StopPlayback();

        var openSource = CreateDarkButton("OPEN FILE LOCATION", 190);
        openSource.Location = new System.Drawing.Point(14, 280);
        openSource.Click += (_, _) =>
        {
            if (_selectedAsset != null)
            {
                OpenFolder(Path.GetDirectoryName(_selectedAsset.FilePath));
            }
        };

        previewHost.Controls.Add(_details);
        previewHost.Controls.Add(_playButton);
        previewHost.Controls.Add(_stopButton);
        previewHost.Controls.Add(openSource);
        previewPanel.Controls.Add(previewHost, 0, 1);
        body.Controls.Add(previewPanel, 2, 0);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };
        root.Controls.Add(footer, 0, 3);

        AddFooterLabel(footer, "Name:", 14, 17);
        _name.Location = new System.Drawing.Point(62, 11);
        _name.Size = new Size(300, 28);
        StyleTextBox(_name);
        footer.Controls.Add(_name);

        AddFooterLabel(footer, "Category:", 380, 17);
        _category.Location = new System.Drawing.Point(446, 11);
        _category.Size = new Size(155, 28);
        _category.DropDownStyle = ComboBoxStyle.DropDown;
        _category.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _category.ForeColor = System.Drawing.Color.White;
        _category.Items.AddRange(Categories);
        footer.Controls.Add(_category);

        ConfigureAccentButton(
            _importSelectedButton,
            "IMPORT SELECTED TO GAME",
            235
        );
        _importSelectedButton.Location = new System.Drawing.Point(620, 9);
        _importSelectedButton.Click += async (_, _) =>
            await ImportSelectedToGameAsync();
        footer.Controls.Add(_importSelectedButton);

        ConfigureDarkButton(
            _importFilteredButton,
            "IMPORT ALL FILTERED TO GAME",
            245
        );
        _importFilteredButton.Location = new System.Drawing.Point(865, 9);
        _importFilteredButton.Click += async (_, _) =>
            await ImportFilteredToGameAsync();
        footer.Controls.Add(_importFilteredButton);

        var openGame = CreateDarkButton("OPEN DESTINATION", 180);
        openGame.Location = new System.Drawing.Point(1120, 9);
        openGame.Click += (_, _) => OpenFolder(SoundImporter.GameFolder(_gameRoot, TargetKind));
        footer.Controls.Add(openGame);

        _status.AutoSize = false;
        _status.Location = new System.Drawing.Point(14, 57);
        _status.Size = new Size(1320, 48);
        _status.ForeColor = System.Drawing.Color.Silver;
        footer.Controls.Add(_status);
    }

    private async Task ReloadLibraryAsync()
    {
        _libraryCancellation?.Cancel();
        _libraryCancellation?.Dispose();
        _libraryCancellation = new CancellationTokenSource();
        var cancellationToken = _libraryCancellation.Token;

        var selectedCategory = _categoryList.SelectedItem?.ToString();
        _status.Text =
            "Indexing sound library in the background. WAV files are not decoded at startup.";

        var progress = new Progress<int>(
            count =>
            {
                if (!IsDisposed && !Disposing)
                {
                    _status.Text =
                        $"Indexing sound library... {count:N0} WAV file(s) found.";
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

                foreach (var categoryName in _assets
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
                             ))
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
                var index = _categoryList.FindStringExact(selectedCategory);
                if (index >= 0)
                {
                    _categoryList.SelectedIndex = index;
                }
            }

            if (_categoryList.SelectedIndex < 0 &&
                _categoryList.Items.Count > 0)
            {
                _categoryList.SelectedIndex = 0;
            }

            _page = 0;
            PopulateSoundList();
            _status.Text =
                $"Indexed {_assets.Count:N0} staged sound(s). " +
                $"{PageSize:N0} are displayed per page.";
        }
        catch (OperationCanceledException)
        {
            // A newer refresh replaced this request.
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                MessageBox.Show(
                    this,
                    "Unable to index sound library: " + ex.Message,
                    "Sounds Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

    private List<SoundAsset> BuildAssetIndex(
        IProgress<int> progress,
        CancellationToken cancellationToken
    )
    {
        var assets = new List<SoundAsset>();
        var count = 0;

        foreach (var file in Directory.EnumerateFiles(
                     _importRoot,
                     "*.wav",
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

                assets.Add(
                    new SoundAsset
                    {
                        FilePath = file,
                        FileName = fileName,
                        SuggestedName = FriendlyName(stem),
                        Category = categoryName,
                        Kind = SoundImporter.LibraryKind(_importRoot, file),
                        FileSize = new FileInfo(file).Length,
                        LastWriteTimeUtc = File.GetLastWriteTimeUtc(file),
                    }
                );

                count++;
                if (count % 500 == 0)
                {
                    progress.Report(count);
                }
            }
            catch
            {
                // Ignore inaccessible entries and continue indexing.
            }
        }

        progress.Report(count);
        return assets;
    }

    private IEnumerable<SoundAsset> GetFilteredAssets()
    {
        IEnumerable<SoundAsset> query = _assets;
        if (_libraryKind.SelectedIndex > 0)
        {
            var kind = (SoundImportKind)(_libraryKind.SelectedIndex - 1);
            query = query.Where(asset => asset.Kind == kind);
        }

        var categoryName = _categoryList.SelectedItem?.ToString();
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

        var search = _search.Text.Trim();
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

        var sortMode = _sort.SelectedItem?.ToString() ?? "Name A-Z";

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
            return query
                .OrderByDescending(asset => asset.LastWriteTimeUtc)
                .ThenBy(
                    asset => asset.SuggestedName,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        if (string.Equals(
                sortMode,
                "Oldest First",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return query
                .OrderBy(asset => asset.LastWriteTimeUtc)
                .ThenBy(
                    asset => asset.SuggestedName,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        if (string.Equals(
                sortMode,
                "Favorites First",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return query
                .OrderByDescending(IsFavorite)
                .ThenByDescending(asset => asset.LastWriteTimeUtc)
                .ThenBy(
                    asset => asset.SuggestedName,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        return query.OrderBy(
            asset => asset.SuggestedName,
            StringComparer.OrdinalIgnoreCase
        );
    }

    private void PopulateSoundList(bool preservePosition = false)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        var selectedAsset = preservePosition ? _selectedAsset : null;
        var previousIndex = preservePosition && _soundList.SelectedIndices.Count > 0
            ? _soundList.SelectedIndices[0]
            : 0;
        var topIndex = preservePosition ? _soundList.TopItem?.Index ?? 0 : 0;

        var filtered = GetFilteredAssets().ToArray();
        var pageCount = Math.Max(
            1,
            (int)Math.Ceiling(filtered.Length / (double)PageSize)
        );

        _page = Math.Clamp(_page, 0, pageCount - 1);

        _visibleAssets.Clear();
        _visibleAssets.AddRange(
            filtered.Skip(_page * PageSize).Take(PageSize)
        );

        _soundList.BeginUpdate();
        try
        {
            _soundList.Items.Clear();

            foreach (var asset in _visibleAssets)
            {
                var favoritePrefix = IsFavorite(asset)
                    ? "* "
                    : string.Empty;
                var item = new ListViewItem(
                    favoritePrefix + asset.SuggestedName
                )
                {
                    Tag = asset,
                };
                item.SubItems.Add(asset.Category);
                item.SubItems.Add(SoundImporter.DisplayName(asset.Kind));
                item.SubItems.Add(
                    asset.MetadataLoaded
                        ? FormatDuration(asset.Duration)
                        : "-"
                );
                item.SubItems.Add(FormatSize(asset.FileSize));
                _soundList.Items.Add(item);
            }
        }
        finally
        {
            _soundList.EndUpdate();
        }

        _previousPageButton.Enabled = _page > 0;
        _nextPageButton.Enabled = _page + 1 < pageCount;
        _pageLabel.Text =
            $"Page {_page + 1:N0}/{pageCount:N0} - " +
            $"{filtered.Length:N0} sound(s)";

        if (_soundList.Items.Count > 0)
        {
            var index = preservePosition
                ? _visibleAssets.FindIndex(asset => ReferenceEquals(asset, selectedAsset))
                : 0;
            if (index < 0)
            {
                index = previousIndex;
            }

            index = Math.Clamp(index, 0, _soundList.Items.Count - 1);
            _soundList.Items[index].Selected = true;

            if (preservePosition)
            {
                _soundList.TopItem = _soundList.Items[
                    Math.Clamp(topIndex, 0, _soundList.Items.Count - 1)
                ];
            }
        }
        else
        {
            _selectedAsset = null;
            _details.Text = string.Empty;
            UpdateFavoriteButton();
        }
    }

    private async Task SelectSoundAsync()
    {
        if (_soundList.SelectedItems.Count == 0)
        {
            return;
        }

        var asset = _soundList.SelectedItems[0].Tag as SoundAsset;
        if (asset == null)
        {
            return;
        }

        StopPlayback();

        _selectionCancellation?.Cancel();
        _selectionCancellation?.Dispose();
        _selectionCancellation = new CancellationTokenSource();
        var cancellationToken = _selectionCancellation.Token;

        _selectedAsset = asset;
        _targetKind.SelectedIndex = (int)asset.Kind;
        UpdateFavoriteButton();
        _name.Text = asset.SuggestedName;
        _category.Text = asset.Category;
        _details.Text = $"Loading {asset.FileName}...";

        try
        {
            if (!asset.MetadataLoaded)
            {
                var metadata = await Task.Run(
                    () => ReadWaveInfo(asset.FilePath, cancellationToken),
                    cancellationToken
                );

                cancellationToken.ThrowIfCancellationRequested();

                asset.Duration = metadata.Duration;
                asset.SampleRate = metadata.SampleRate;
                asset.Channels = metadata.Channels;
                asset.BitsPerSample = metadata.BitsPerSample;
                asset.MetadataLoaded = true;
            }

            if (!ReferenceEquals(_selectedAsset, asset))
            {
                return;
            }

            _details.Text =
                $"File: {asset.FileName}\r\n" +
                $"Type: {SoundImporter.DisplayName(asset.Kind)}\r\n" +
                $"Category: {asset.Category}\r\n" +
                $"Duration: {FormatDuration(asset.Duration)}\r\n" +
                $"Sample rate: {asset.SampleRate:N0} Hz\r\n" +
                $"Channels: {asset.Channels}\r\n" +
                $"Bit depth: {asset.BitsPerSample}-bit\r\n" +
                $"Size: {FormatSize(asset.FileSize)}\r\n" +
                $"Added/updated: {asset.LastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm}\r\n\r\n" +
                "PLAY previews the original WAV without importing it.";

            if (_soundList.SelectedItems.Count > 0)
            {
                _soundList.SelectedItems[0].SubItems[3].Text =
                    FormatDuration(asset.Duration);
            }
        }
        catch (OperationCanceledException)
        {
            // Selection changed.
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_selectedAsset, asset))
            {
                _details.Text =
                    $"{asset.FileName}\r\n\r\n" +
                    $"Unable to read WAV metadata: {ex.Message}";
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

    private bool IsFavorite(SoundAsset asset) =>
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
            _status.Text = "Unable to save sound favorites: " + ex.Message;
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

        PopulateSoundList(preservePosition: true);
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

    private async Task ImportZipAsync()
    {
        if (!ChooseImportKind(out var kind)) return;
        using var dialog = new OpenFileDialog
        {
            Filter = "ZIP archives (*.zip)|*.zip",
            Title = "Import sound ZIP",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await RunOperationAsync(
            (progress, cancellationToken) =>
                ProcessZipImport(
                    dialog.FileName,
                    progress,
                    cancellationToken,
                    kind
                ),
            $"Importing {SoundImporter.DisplayName(kind)} ZIP..."
        );
    }

    private async Task ImportFilesAsync()
    {
        if (!ChooseImportKind(out var kind)) return;
        using var dialog = new OpenFileDialog
        {
            Filter = SoundImporter.FileFilter,
            Title = "Import audio files (automatic WAV conversion)",
            CheckFileExists = true,
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var files = dialog.FileNames;
        await RunOperationAsync(
            (progress, cancellationToken) =>
                ProcessFileImport(
                    files,
                    progress,
                    cancellationToken,
                    kind
                ),
            $"Importing {files.Length:N0} audio file(s) as WAV..."
        );
    }

    private async Task RunOperationAsync(
        Func<IProgress<SoundImportProgress>, CancellationToken, SoundImportResult>
            operation,
        string startingText
    )
    {
        if (_operationCancellation != null)
        {
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        var cancellationToken = _operationCancellation.Token;
        var stopwatch = Stopwatch.StartNew();

        SetOperationUi(true, startingText);

        var progress = new Progress<SoundImportProgress>(
            value =>
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                var percent = value.Total <= 0
                    ? 0
                    : Math.Clamp(
                        (int)Math.Round(
                            value.Completed * 100d / value.Total
                        ),
                        0,
                        100
                    );

                _progressBar.Value = percent;
                _progressLabel.Text =
                    $"{percent}% | {value.Completed:N0}/{value.Total:N0}";

                _status.Text =
                    $"{value.Stage}" +
                    (string.IsNullOrWhiteSpace(value.CurrentItem)
                        ? string.Empty
                        : $" | {value.CurrentItem}");
            }
        );

        try
        {
            var result = await Task.Run(
                () => operation(progress, cancellationToken),
                cancellationToken
            );

            if (IsDisposed || Disposing)
            {
                return;
            }

            await ReloadLibraryAsync();

            _progressBar.Value = result.Cancelled ? _progressBar.Value : 100;
            _progressLabel.Text = result.Cancelled
                ? "Cancelled"
                : $"100% | {stopwatch.Elapsed:mm\\:ss}";

            _status.Text = result.Cancelled
                ? $"Operation cancelled. {result.Imported:N0} sound(s) completed; " +
                  $"{result.Skipped:N0} skipped; " +
                  $"{result.RenamedDuplicates:N0} duplicate name(s) safely renamed."
                : $"Operation complete. {result.Imported:N0} sound(s) imported; " +
                  $"{result.Skipped:N0} skipped; " +
                  $"{result.RenamedDuplicates:N0} duplicate name(s) prefixed; {result.Converted:N0} converted to WAV.";
            if (result.Errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, result.Errors), "Audio import - files skipped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
            {
                _status.Text = "Operation cancelled.";
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Sounds Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        finally
        {
            stopwatch.Stop();

            _operationCancellation?.Dispose();
            _operationCancellation = null;

            if (!IsDisposed && !Disposing)
            {
                SetOperationUi(false, string.Empty);
            }
        }
    }

    private SoundImportResult ProcessZipImport(
        string zipPath,
        IProgress<SoundImportProgress> progress,
        CancellationToken cancellationToken,
        SoundImportKind kind
    )
    {
        var result = new SoundImportResult();

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries
                .Where(entry =>
                    entry.Length > 0 &&
                    SoundImporter.Supports(entry.Name))
                .ToArray();

            for (var i = 0; i < entries.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = entries[i];

                progress.Report(
                    new SoundImportProgress
                    {
                        Completed = i,
                        Total = entries.Length,
                        Stage = "Extracting / converting sounds to WAV",
                        CurrentItem = entry.FullName,
                    }
                );

                try
                {
                    var sourceName =
                        Path.GetFileNameWithoutExtension(entry.Name);
                    var folderHint = GetZipDirectory(entry.FullName)
                        .Replace('/', ' ')
                        .Replace('\\', ' ');
                    var categoryName = ClassifySound(
                        $"{sourceName} {folderHint}"
                    );
                    var categoryDirectory =
                        Path.Combine(SoundImporter.LibraryFolder(_importRoot, kind), categoryName);
                    Directory.CreateDirectory(categoryDirectory);

                    var fileName = Path.ChangeExtension(Path.GetFileName(entry.Name), ".wav");
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var requestedDestination =
                        Path.Combine(categoryDirectory, fileName);
                    var temporary = Path.Combine(Path.GetTempPath(), $"CR2026-sound-{Guid.NewGuid():N}{Path.GetExtension(entry.Name)}");
                    string destination;
                    try
                    {
                        using (var input = entry.Open())
                        using (var output = new FileStream(temporary, FileMode.CreateNew))
                            CopyStreamWithCancellation(input, output, cancellationToken);
                        destination = SoundImporter.Import(temporary, requestedDestination, cancellationToken);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    if (!string.Equals(destination, requestedDestination, StringComparison.OrdinalIgnoreCase)) result.RenamedDuplicates++;
                    if (!entry.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) result.Converted++;

                    result.Imported++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    result.Skipped++;
                    if (result.Errors.Count < 20) result.Errors.Add($"{entry.FullName}: {exception.Message}");
                }
            }

            progress.Report(
                new SoundImportProgress
                {
                    Completed = entries.Length,
                    Total = entries.Length,
                    Stage = "ZIP import complete",
                }
            );
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        return result;
    }

    private SoundImportResult ProcessFileImport(
        IReadOnlyList<string> files,
        IProgress<SoundImportProgress> progress,
        CancellationToken cancellationToken,
        SoundImportKind kind
    )
    {
        var result = new SoundImportResult();

        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = files[i];

                progress.Report(
                    new SoundImportProgress
                    {
                        Completed = i,
                        Total = files.Count,
                        Stage = "Importing / converting sounds to WAV",
                        CurrentItem = Path.GetFileName(file),
                    }
                );

                try
                {
                    var sourceName = Path.GetFileNameWithoutExtension(file);
                    var categoryName = ClassifySound(sourceName);
                    var categoryDirectory =
                        Path.Combine(SoundImporter.LibraryFolder(_importRoot, kind), categoryName);
                    Directory.CreateDirectory(categoryDirectory);

                    var requestedDestination = Path.Combine(
                        categoryDirectory,
                        Path.ChangeExtension(Path.GetFileName(file), ".wav")
                    );
                    var destination = SoundImporter.Import(file, requestedDestination, cancellationToken);
                    if (!string.Equals(destination, requestedDestination, StringComparison.OrdinalIgnoreCase)) result.RenamedDuplicates++;
                    if (!file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) result.Converted++;

                    result.Imported++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    result.Skipped++;
                    if (result.Errors.Count < 20) result.Errors.Add($"{Path.GetFileName(file)}: {exception.Message}");
                }
            }

            progress.Report(
                new SoundImportProgress
                {
                    Completed = files.Count,
                    Total = files.Count,
                    Stage = "File import complete",
                }
            );
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        return result;
    }

    private async Task ImportSelectedToGameAsync()
    {
        if (_selectedAsset == null ||
            !File.Exists(_selectedAsset.FilePath))
        {
            MessageBox.Show(
                this,
                "Select a sound first.",
                "Sounds Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var desiredName = MakeFileName(
            _name.Text,
            _selectedAsset.FileName
        );

        await ImportAssetsToGameAsync(
            new[] { (_selectedAsset, desiredName) }
        );
    }

    private async Task ImportFilteredToGameAsync()
    {
        var assets = GetFilteredAssets().ToArray();
        if (assets.Length == 0)
        {
            MessageBox.Show(
                this,
                "No sounds match the current filter.",
                "Sounds Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Import {assets.Length:N0} filtered sound(s) as {SoundImporter.DisplayName(TargetKind)} into resources/{SoundImporter.Folder(TargetKind)}?\n\n" +
            "All sounds are imported as PCM WAV. Duplicate filenames receive a numeric prefix; existing files are preserved.",
            "Import Sounds To Game",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await ImportAssetsToGameAsync(
            assets.Select(asset => (asset, asset.FileName)).ToArray()
        );
    }

    private async Task ImportAssetsToGameAsync(
        IReadOnlyList<(SoundAsset Asset, string FileName)> assets
    )
    {
        if (_operationCancellation != null)
        {
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        var cancellationToken = _operationCancellation.Token;

        var kind = TargetKind;
        var destinationRoot = SoundImporter.GameFolder(_gameRoot, kind);
        SetOperationUi(true, $"Importing {SoundImporter.DisplayName(kind)} to game...");

        var imported = 0;
        var skipped = 0;
        var renamedDuplicates = 0;
        var errors = new List<string>();

        try
        {
            await Task.Run(
                () =>
                {
                    for (var i = 0; i < assets.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var (asset, fileName) = assets[i];
                        var requestedDestination =
                            Path.Combine(destinationRoot, fileName);
                        try
                        {
                            var destination = SoundImporter.Import(asset.FilePath, requestedDestination, cancellationToken);
                            if (!string.Equals(destination, requestedDestination, StringComparison.OrdinalIgnoreCase)) renamedDuplicates++;
                            imported++;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            skipped++;
                            if (errors.Count < 20) errors.Add($"{asset.FileName}: {exception.Message}");
                        }

                        var completed = i + 1;
                        BeginInvoke(
                            () =>
                            {
                                if (IsDisposed || Disposing)
                                {
                                    return;
                                }

                                var percent = Math.Clamp(
                                    (int)Math.Round(
                                        completed * 100d / assets.Count
                                    ),
                                    0,
                                    100
                                );
                                _progressBar.Value = percent;
                                _progressLabel.Text =
                                    $"{percent}% | {completed:N0}/{assets.Count:N0}";
                                _status.Text =
                                    $"Copying to resources/{SoundImporter.Folder(kind)} | {asset.FileName}";
                            }
                        );
                    }
                },
                cancellationToken
            );

            GameContentManager.LoadSounds();

            _progressBar.Value = 100;
            _progressLabel.Text = "100%";
            _status.Text =
                $"{SoundImporter.DisplayName(kind)} refreshed: {imported:N0} imported, {skipped:N0} skipped, " +
                $"{renamedDuplicates:N0} duplicate name(s) prefixed.";
            if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Audio import - files skipped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            _status.Text =
                $"Game import cancelled. {imported:N0} sound(s) were already copied.";
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            SetOperationUi(false, string.Empty);
        }
    }

    private void SetOperationUi(bool running, string status)
    {
        _importZipButton.Enabled = !running;
        _importFilesButton.Enabled = !running;
        _importSelectedButton.Enabled = !running;
        _importFilteredButton.Enabled = !running;
        _targetKind.Enabled = _libraryKind.Enabled = !running;
        _cancelButton.Enabled = running;
        _cancelButton.Text = "CANCEL";
        _progressBar.Visible = running || _progressBar.Value > 0;
        _progressLabel.Visible = running || _progressBar.Value > 0;

        if (running)
        {
            _progressBar.Value = 0;
            _progressLabel.Text = "0%";
            _status.Text = status;
        }
    }

    private void PlaySelected()
    {
        if (_selectedAsset == null ||
            !File.Exists(_selectedAsset.FilePath))
        {
            return;
        }

        StopPlayback();
        _ = PlaySound(
            _selectedAsset.FilePath,
            IntPtr.Zero,
            SoundFlags.Async |
            SoundFlags.FileName |
            SoundFlags.NoDefault
        );
    }

    private static void StopPlayback()
    {
        _ = PlaySound(
            null,
            IntPtr.Zero,
            SoundFlags.Purge
        );
    }

    private static (
        TimeSpan Duration,
        int SampleRate,
        short Channels,
        short BitsPerSample
    ) ReadWaveInfo(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException("Not a RIFF WAV file.");
        }

        _ = reader.ReadUInt32();

        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException("Not a WAVE file.");
        }

        int sampleRate = 0;
        short channels = 0;
        short bitsPerSample = 0;
        int byteRate = 0;
        long dataLength = 0;

        while (stream.Position + 8 <= stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();
            var chunkStart = stream.Position;

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                _ = reader.ReadUInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                byteRate = reader.ReadInt32();
                _ = reader.ReadUInt16();
                bitsPerSample = reader.ReadInt16();
            }
            else if (chunkId == "data")
            {
                dataLength = chunkSize;
            }

            var next = chunkStart + chunkSize;
            if ((chunkSize & 1) != 0)
            {
                next++;
            }

            stream.Position = Math.Min(next, stream.Length);

            if (sampleRate > 0 && byteRate > 0 && dataLength > 0)
            {
                break;
            }
        }

        if (sampleRate <= 0 || byteRate <= 0 || dataLength <= 0)
        {
            throw new InvalidDataException(
                "WAV metadata could not be read."
            );
        }

        return (
            TimeSpan.FromSeconds(dataLength / (double)byteRate),
            sampleRate,
            channels,
            bitsPerSample
        );
    }

    private static string ClassifySound(string value)
    {
        foreach (var pair in CategoryKeywords)
        {
            if (pair.Value.Any(keyword =>
                    value.Contains(
                        keyword,
                        StringComparison.OrdinalIgnoreCase
                    )))
            {
                return pair.Key;
            }
        }

        return "Misc";
    }

    private static string FriendlyName(string stem)
    {
        var value = stem
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        while (value.Contains("  ", StringComparison.Ordinal))
        {
            value = value.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return "Sound";
        }

        return string.Join(
            " ",
            value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(word =>
                    word.Length == 1
                        ? word.ToUpperInvariant()
                        : char.ToUpperInvariant(word[0]) + word[1..])
        );
    }

    private static string MakeFileName(
        string requestedName,
        string fallbackFileName
    )
    {
        var stem = requestedName.Trim();
        if (string.IsNullOrWhiteSpace(stem))
        {
            return fallbackFileName;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalid, '_');
        }

        stem = stem.Trim('_', '.', ' ');
        if (string.IsNullOrWhiteSpace(stem))
        {
            return fallbackFileName;
        }

        return stem.EndsWith(
            ".wav",
            StringComparison.OrdinalIgnoreCase
        )
            ? stem
            : stem + ".wav";
    }

    private static string GetZipDirectory(string fullName)
    {
        var normalized = fullName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }

    private static void CopyStreamWithCancellation(
        Stream input,
        Stream output,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[128 * 1024];
        int read;

        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"hh\:mm\:ss");
        }

        return duration.ToString(@"mm\:ss");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024L)
        {
            return $"{bytes / (1024d * 1024d):0.0} MB";
        }

        if (bytes >= 1024L)
        {
            return $"{bytes / 1024d:0.0} KB";
        }

        return $"{bytes:N0} B";
    }

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                }
            );
        }
        catch
        {
            // Explorer access is optional.
        }
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

        panel.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.FromArgb(247, 69, 96),
                Font = new Font(
                    SystemFonts.MessageBoxFont.FontFamily,
                    11,
                    FontStyle.Bold
                ),
                TextAlign = ContentAlignment.MiddleLeft,
            },
            0,
            0
        );

        return panel;
    }

    private static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        listBox.ForeColor = System.Drawing.Color.Gainsboro;
        listBox.BorderStyle = BorderStyle.None;
        listBox.IntegralHeight = false;
        listBox.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            10
        );
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        textBox.ForeColor = System.Drawing.Color.White;
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void AddFooterLabel(
        Control parent,
        string text,
        int x,
        int y
    )
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

    private static Button CreateDarkButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 34),
        };
        ConfigureDarkButtonStyle(button);
        return button;
    }

    private static void ConfigureDarkButton(
        Button button,
        string text,
        int width
    )
    {
        button.Text = text;
        button.Size = new Size(width, 34);
        ConfigureDarkButtonStyle(button);
    }

    private static void ConfigureDarkButtonStyle(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        button.ForeColor = System.Drawing.Color.Gainsboro;
        button.FlatAppearance.BorderColor =
            System.Drawing.Color.FromArgb(90, 78, 81);
        button.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            9,
            FontStyle.Bold
        );
        button.Cursor = Cursors.Hand;
    }

    private static void ConfigureAccentButton(
        Button button,
        string text,
        int width
    )
    {
        button.Text = text;
        button.Size = new Size(width, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = System.Drawing.Color.FromArgb(247, 69, 96);
        button.ForeColor = System.Drawing.Color.White;
        button.FlatAppearance.BorderColor = button.BackColor;
        button.Font = new Font(
            SystemFonts.MessageBoxFont.FontFamily,
            9,
            FontStyle.Bold
        );
        button.Cursor = Cursors.Hand;
    }

    [Flags]
    private enum SoundFlags : uint
    {
        Async = 0x0001,
        NoDefault = 0x0002,
        FileName = 0x00020000,
        Purge = 0x0040,
    }

    [DllImport(
        "winmm.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    private static extern bool PlaySound(
        string? pszSound,
        IntPtr hmod,
        SoundFlags fdwSound
    );
}
