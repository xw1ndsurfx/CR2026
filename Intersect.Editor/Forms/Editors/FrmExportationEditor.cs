using System.Globalization;
using DarkUI.Forms;
using Intersect.Editor.General;
using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Professions;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors;

/// <summary>A single, server-validated, form-based Exportation workspace.</summary>
public sealed class FrmExportationEditor : DarkForm
{
    private sealed record Named(Guid Id, string Caption)
    {
        public override string ToString() => Caption;
    }
    private sealed record RouteEntry(ExportationRoute Route)
    {
        public override string ToString() => Route.Name;
    }

    private static readonly DrawingColor Back = DrawingColor.FromArgb(24, 31, 42);
    private static readonly DrawingColor PanelBack = DrawingColor.FromArgb(32, 42, 55);
    private static readonly DrawingColor InputBack = DrawingColor.FromArgb(27, 36, 48);
    private static readonly DrawingColor TextLight = DrawingColor.FromArgb(235, 241, 247);
    private static readonly DrawingColor Accent = DrawingColor.FromArgb(153, 216, 59);
    private static string T(string key) => ExportationText.Get(key, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private ExportationConfiguration _configuration;
    private readonly List<ExportationRoute> _routes;
    private ExportationRoute? _route;
    private bool _loading, _dirty, _pending;
    private readonly System.Windows.Forms.Timer _timeout = new() { Interval = 15000 };
    private readonly ListBox _routeList = new() { Name = "ExportationRoutes", Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _search = new() { Name = "ExportationSearch", PlaceholderText = "Rechercher une route...", Dock = DockStyle.Fill };
    private readonly TabControl _tabs = new() { Name = "ExportationTabs", Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _save = Button("Enregistrer", "ExportationSave");
    private readonly Button _reload = Button("Recharger", "ExportationReload");

    private readonly CheckBox _enabled = new() { Name = "ExportationEnabled", AutoSize = true };
    private readonly ComboBox _profession = Combo("ExportationProfession");
    private readonly TextBox _name = TextBox("ExportationRouteName");
    private readonly ComboBox _sourceMap = Combo("ExportationSourceMap");
    private readonly ComboBox _targetMap = Combo("ExportationDestinationMap");
    private readonly ComboBox _merchant = Combo("ExportationMerchant");
    private readonly ComboBox _recipient = Combo("ExportationRecipient");
    private readonly ComboBox _quest = Combo("ExportationQuest");
    private readonly ComboBox _rewardItem = Combo("ExportationRewardItem");
    private readonly NumericUpDown _rewardQty = Number("ExportationRewardQuantity", 0, 1000000);
    private readonly NumericUpDown _level = Number("ExportationRequiredLevel", 1, 500);
    private readonly NumericUpDown _exp = Number("ExportationRewardExperience", 0, 2000000000);
    private readonly ListView _goods = new() { Name = "ExportationGoods", Dock = DockStyle.Fill,
        View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly ComboBox _goodItem = Combo("ExportationGoodItem");
    private readonly ComboBox _goodGroup = Combo("ExportationGoodGroup");
    private readonly ComboBox _goodSprite = Combo("ExportationGoodSprite");
    private readonly NumericUpDown _goodQty = Number("ExportationGoodQuantity", 1, 10000);
    private readonly Label _professionInfo = new() { AutoSize = true, ForeColor = TextLight, MaximumSize = new Size(670, 0) };
    private readonly NumericUpDown _sampleExp = Number("ExportationSampleExperience", 0, 1000000000000m);
    private readonly ProgressBar _progress = new() { Name = "ExportationExperiencePreview", Maximum = 100, Width = 400, Height = 20 };
    private readonly Label _experienceInfo = new() { AutoSize = true, ForeColor = TextLight };
    private readonly Label _preview = new() { Dock = DockStyle.Fill, ForeColor = TextLight,
        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f), Padding = new Padding(25) };

    private static ComboBox Combo(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat, BackColor = InputBack, ForeColor = TextLight, DropDownWidth = 500,
    };
    private static TextBox TextBox(string name) => new() { Name = name, Dock = DockStyle.Fill,
        BackColor = InputBack, ForeColor = TextLight, BorderStyle = BorderStyle.FixedSingle };
    private static NumericUpDown Number(string name, decimal min, decimal max) => new()
    {
        Name = name, Minimum = min, Maximum = max, Dock = DockStyle.Fill,
        ThousandsSeparator = true, BackColor = InputBack, ForeColor = TextLight,
    };
    private static Button Button(string label, string name = "") => new() { Text = label, Name = name,
        AutoSize = true, Height = 33, FlatStyle = FlatStyle.Flat, BackColor = PanelBack, ForeColor = TextLight };
    private static Label Head(string text) => new() { Text = text, AutoSize = true, ForeColor = Accent,
        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f, FontStyle.Bold),
        Margin = new Padding(7, 11, 7, 11) };
    private static TabPage Page(string text) => new(text) { AutoScroll = true, Padding = new Padding(8),
        BackColor = Back, ForeColor = TextLight };
    private static TableLayoutPanel FormGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
            BackColor = PanelBack, Padding = new Padding(10) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }
    private static void Field(TableLayoutPanel grid, string caption, Control input)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = caption, ForeColor = TextLight, AutoSize = true,
            Anchor = AnchorStyles.Left, Margin = new Padding(5, 11, 9, 8) }, 0, row);
        input.Margin = new Padding(5, 5, 5, 6);
        grid.Controls.Add(input, 1, row);
    }
    private static void Full(TableLayoutPanel grid, Control input)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(input, 0, row);
        grid.SetColumnSpan(input, 2);
    }

    public FrmExportationEditor(string json)
    {
        _configuration = ExportationConfiguration.FromJson(json);
        _routes = _configuration.Routes.ToList();
        Name = "ExportationEditor"; Text = "Exportation Editor";
        Size = new Size(1310, 830); MinimumSize = new Size(880, 590);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Back; ForeColor = TextLight;
        Build();
        Wire();
        LoadConfiguration(json, _routes.FirstOrDefault()?.Id ?? Guid.Empty);
        _status.Text = T("Changes are saved only after server acknowledgement.");
        _timeout.Tick += (_, _) =>
        {
            _timeout.Stop();
            if (!_pending) return;
            Pending(false);
            _status.Text = T("Server did not confirm the save. Your draft is preserved.");
        };
        FormClosing += (_, args) =>
        {
            if (_pending) { args.Cancel = true; return; }
            if (_dirty && MessageBox.Show(this, T("Discard the local draft?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                args.Cancel = true;
        };
        Disposed += (_, _) => { _timeout.Stop(); _timeout.Dispose(); };
    }

    private void Build()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            WrapContents = true, BackColor = PanelBack, Padding = new Padding(12, 8, 12, 8) };
        _save.Text = T("Save to server");
        _reload.Text = T("Reload from server");
        top.Controls.Add(_save); top.Controls.Add(_reload);
        top.Controls.Add(new Label { Text = T("Merchant orders, cargo, sea deliveries and profession EXP"),
            AutoSize = true, ForeColor = TextLight, Margin = new Padding(18, 10, 0, 4) });
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42,
            BackColor = PanelBack, Padding = new Padding(12, 4, 12, 4) };
        bottom.Controls.Add(_status);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 295,
            Panel1MinSize = 200, Panel2MinSize = 480 };
        split.Panel1.Padding = new Padding(8);
        split.Panel2.Padding = new Padding(8);
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        left.Controls.Add(Head(T("Routes")), 0, 0);
        left.Controls.Add(_search, 0, 1);
        _routeList.BackColor = InputBack; _routeList.ForeColor = TextLight;
        left.Controls.Add(_routeList, 0, 2);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        var create = Button(T("New"), "ExportationNew");
        var duplicate = Button(T("Duplicate"));
        var delete = Button(T("Delete"));
        create.Click += (_, _) => CreateRoute();
        duplicate.Click += (_, _) => DuplicateRoute();
        delete.Click += (_, _) => DeleteRoute();
        actions.Controls.Add(create); actions.Controls.Add(duplicate); actions.Controls.Add(delete);
        left.Controls.Add(actions, 0, 3);
        split.Panel1.Controls.Add(left);
        _tabs.TabPages.Add(GeneralTab());
        _tabs.TabPages.Add(GoodsTab());
        _tabs.TabPages.Add(ProfessionTab());
        _tabs.TabPages.Add(AssetsTab());
        _tabs.TabPages.Add(PreviewTab());
        split.Panel2.Controls.Add(_tabs);
        Controls.Add(split); Controls.Add(bottom); Controls.Add(top);
    }

    private TabPage GeneralTab()
    {
        var page = Page(T("General"));
        var grid = FormGrid();
        Full(grid, Head(T("System")));
        _enabled.Text = T("Enable Exportation");
        Field(grid, T("Activity enabled"), _enabled);
        Field(grid, T("Profession used"), _profession);
        Full(grid, Head(T("Route information")));
        Field(grid, T("Route name"), _name);
        Field(grid, T("Source map / island"), _sourceMap);
        Field(grid, T("Merchant event"), _merchant);
        Field(grid, T("Destination map / island"), _targetMap);
        Field(grid, T("Recipient event"), _recipient);
        Field(grid, T("Delivery quest (one task)"), _quest);
        Full(grid, Head(T("Delivery rules")));
        Full(grid, new Label { AutoSize = true, ForeColor = TextLight,
            Text = T("Boat required. Cargo is lost on death, shipwreck or quest abandonment. Return to merchant to retry."),
            MaximumSize = new Size(650, 0), Margin = new Padding(10) });
        Full(grid, Head(T("Rewards (on delivery only)")));
        Field(grid, T("Reward item / currency"), _rewardItem);
        Field(grid, T("Reward quantity"), _rewardQty);
        Field(grid, T("Profession EXP"), _exp);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage GoodsTab()
    {
        var page = Page(T("Goods and crates"));
        page.AutoScroll = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 247));
        _goods.Columns.Add(T("Item"), 250);
        _goods.Columns.Add(T("Quantity"), 90);
        _goods.Columns.Add(T("Crate group"), 145);
        _goods.Columns.Add(T("Sprite"), 130);
        _goods.BackColor = InputBack; _goods.ForeColor = TextLight;
        layout.Controls.Add(_goods, 0, 0);
        var grid = FormGrid();
        Field(grid, T("Item"), _goodItem);
        Field(grid, T("Quantity"), _goodQty);
        Field(grid, T("Crate group"), _goodGroup);
        Field(grid, T("Optional sprite"), _goodSprite);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var add = Button(T("Add / update item"), "ExportationAddGood");
        var remove = Button(T("Remove selected"));
        add.Click += (_, _) => AddOrUpdateGood();
        remove.Click += (_, _) => RemoveGood();
        actions.Controls.Add(add); actions.Controls.Add(remove);
        Full(grid, actions);
        layout.Controls.Add(grid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage ProfessionTab()
    {
        var page = Page(T("Profession and EXP"));
        var grid = FormGrid();
        Full(grid, Head(T("Native profession level and EXP")));
        Full(grid, _professionInfo);
        Field(grid, T("Minimum level for this route"), _level);
        Full(grid, new Label { AutoSize = true, ForeColor = TextLight,
            Text = T("EXP is awarded only when the quest and cargo delivery succeed."), Margin = new Padding(12) });
        Full(grid, Head(T("EXP simulator (not a live character)")));
        Field(grid, T("Simulated total EXP"), _sampleExp);
        Full(grid, _progress); Full(grid, _experienceInfo);
        var actions = new FlowLayoutPanel { AutoSize = true };
        var open = Button(T("Open Profession Editor"));
        var refresh = Button(T("Refresh professions"));
        open.Click += (_, _) =>
        {
            var window = new FrmProfessionConfiguration();
            window.FormClosed += (_, _) => RefreshProfessionChoices();
            window.Show(this);
        };
        refresh.Click += (_, _) => RefreshProfessionChoices();
        actions.Controls.Add(open); actions.Controls.Add(refresh);
        Full(grid, actions);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage AssetsTab()
    {
        var page = Page(T("Graphics and sounds"));
        page.AutoScroll = false;
        page.Controls.Add(new ExportationAssetsPanel { Dock = DockStyle.Fill });
        return page;
    }

    private TabPage PreviewTab()
    {
        var page = Page(T("Preview"));
        page.Controls.Add(_preview);
        return page;
    }

    private void Wire()
    {
        _save.Click += (_, _) => Save();
        _reload.Click += (_, _) => Reload();
        _search.TextChanged += (_, _) => RefreshRoutes(_route?.Id ?? Guid.Empty);
        _routeList.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _route = (_routeList.SelectedItem as RouteEntry)?.Route;
            ShowRoute();
        };
        _enabled.CheckedChanged += (_, _) => { if (_loading) return; _configuration.Enabled = _enabled.Checked; Changed(); };
        _profession.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _configuration.ProfessionId = Id(_profession); Changed(); UpdateProfession();
        };
        _name.TextChanged += (_, _) =>
        {
            if (_loading || _route == null) return;
            _route.Name = _name.Text;
            Changed(); RefreshRoutes(_route.Id);
        };
        _sourceMap.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _route == null) return;
            _route.SourceMapId = Id(_sourceMap);
            _route.SourceNpcId = Guid.Empty;
            FillEvents(_merchant, _route.SourceMapId, Guid.Empty);
            Changed();
        };
        _targetMap.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _route == null) return;
            _route.RecipientMapId = Id(_targetMap);
            _route.RecipientNpcId = Guid.Empty;
            FillEvents(_recipient, _route.RecipientMapId, Guid.Empty);
            Changed();
        };
        _merchant.SelectedIndexChanged += (_, _) => EditRoute(r => r.SourceNpcId = Id(_merchant));
        _recipient.SelectedIndexChanged += (_, _) => EditRoute(r => r.RecipientNpcId = Id(_recipient));
        _quest.SelectedIndexChanged += (_, _) => EditRoute(r => r.QuestId = Id(_quest));
        _rewardItem.SelectedIndexChanged += (_, _) => EditRoute(r => r.RewardItemId = Id(_rewardItem));
        _rewardQty.ValueChanged += (_, _) => EditRoute(r => r.RewardQuantity = (int)_rewardQty.Value);
        _level.ValueChanged += (_, _) => EditRoute(r => r.MinimumProfessionLevel = (int)_level.Value);
        _exp.ValueChanged += (_, _) => EditRoute(r => r.ExperienceReward = (long)_exp.Value);
        _goods.SelectedIndexChanged += (_, _) => SelectGood();
        _sampleExp.ValueChanged += (_, _) => UpdateProfession();
    }

    private void EditRoute(Action<ExportationRoute> action)
    {
        if (_loading || _route == null) return;
        action(_route); Changed();
    }
    private void Changed()
    {
        _dirty = true;
        _status.Text = T("Unsaved changes.");
        ShowSummary();
    }
    private static Guid Id(ComboBox box) => (box.SelectedItem as Named)?.Id ?? Guid.Empty;
    private static void Fill(ComboBox box, IEnumerable<Named> choices, Guid select, string none)
    {
        var values = choices.DistinctBy(c => c.Id)
            .OrderBy(c => c.Caption, StringComparer.CurrentCultureIgnoreCase).ToArray();
        box.BeginUpdate();
        box.Items.Clear();
        box.Items.Add(new Named(Guid.Empty, none));
        foreach (var value in values) box.Items.Add(value);
        if (select != Guid.Empty && values.All(v => v.Id != select))
            box.Items.Add(new Named(select, T("Missing reference") + ": " + select.ToString("D")));
        box.SelectedItem = box.Items.OfType<Named>().FirstOrDefault(v => v.Id == select) ?? box.Items[0];
        box.EndUpdate();
    }

    private void RefreshProfessionChoices()
    {
        var previous = _loading; _loading = true;
        try
        {
            Fill(_profession, ProfessionConfiguration.Instance.Professions
                .Where(p => !NativeProfessions.IsNative(p.Id))
                .Select(p => new Named(p.Id, p.Name)),
                _configuration.ProfessionId, T("Select a profession..."));
        }
        finally { _loading = previous; }
        UpdateProfession();
    }

    private void FillEvents(ComboBox box, Guid map, Guid selected)
    {
        var old = _loading; _loading = true;
        try
        {
            var events = EventDescriptor.Lookup.Values.OfType<EventDescriptor>()
                .Concat(Globals.CurrentMap?.LocalEvents.Values.OfType<EventDescriptor>() ?? []);
            Fill(box, events.Where(x => !x.CommonEvent && !x.Global && x.MapId == map)
                    .Select(x => new Named(x.Id, x.Name)),
                selected, T("Select map event..."));
        }
        finally { _loading = old; }
    }

    private void RefreshRoutes(Guid id)
    {
        var old = _loading; _loading = true;
        try
        {
            _routeList.BeginUpdate(); _routeList.Items.Clear();
            foreach (var r in _routes.Where(x => x.Name.Contains(_search.Text,
                        StringComparison.CurrentCultureIgnoreCase))
                     .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
                _routeList.Items.Add(new RouteEntry(r));
            _routeList.SelectedItem = _routeList.Items.OfType<RouteEntry>().FirstOrDefault(x => x.Route.Id == id);
            _routeList.EndUpdate();
        }
        finally { _loading = old; }
    }

    private void ShowRoute()
    {
        var r = _route; var old = _loading; _loading = true;
        try
        {
            _name.Text = r?.Name ?? "";
            var maps = MapList.OrderedMaps.Where(m => m.MapId != Guid.Empty)
                .Select(m => new Named(m.MapId, m.Name));
            Fill(_sourceMap, maps, r?.SourceMapId ?? Guid.Empty, T("Source map..."));
            Fill(_targetMap, maps, r?.RecipientMapId ?? Guid.Empty, T("Destination map..."));
            FillEvents(_merchant, r?.SourceMapId ?? Guid.Empty, r?.SourceNpcId ?? Guid.Empty);
            FillEvents(_recipient, r?.RecipientMapId ?? Guid.Empty, r?.RecipientNpcId ?? Guid.Empty);
            Fill(_quest, QuestDescriptor.Lookup.Values.OfType<QuestDescriptor>()
                .Where(q => q.Tasks.Count == 1).Select(q => new Named(q.Id, q.Name)),
                r?.QuestId ?? Guid.Empty, T("One-task quest..."));
            Fill(_rewardItem, ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                .Where(item => item.IsStackable && item.MaxInventoryStack > 0)
                .Select(item => new Named(item.Id, item.Name)),
                r?.RewardItemId ?? Guid.Empty, T("No item"));
            _rewardQty.Value = Math.Clamp(r?.RewardQuantity ?? 0, 0, 1000000);
            _exp.Value = Math.Clamp(r?.ExperienceReward ?? 0L, 0L, 2000000000L);
            _level.Value = Math.Clamp(r?.MinimumProfessionLevel ?? 1, 1, 500);
            Fill(_goodItem, ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                .Select(item => new Named(item.Id, item.Name)), Guid.Empty, T("Select item..."));
            _goodGroup.Items.Clear();
            foreach (var group in Enum.GetValues<CargoGroup>()) _goodGroup.Items.Add(T(group.ToString()));
            _goodGroup.SelectedIndex = 0;
            _goodSprite.Items.Clear();
            _goodSprite.Items.Add(new Named(Guid.Empty, T("Default item icon")));
            foreach (var key in ExportationArt.Goods) _goodSprite.Items.Add(new Named(Guid.Empty, key));
            _goodSprite.SelectedIndex = 0;
            foreach (Control c in new Control[]
            { _name, _sourceMap, _targetMap, _merchant, _recipient, _quest, _rewardItem,
              _rewardQty, _exp, _level, _goods, _goodItem, _goodGroup, _goodSprite, _goodQty })
                c.Enabled = r != null;
        }
        finally { _loading = old; }
        RefreshGoods();
        ShowSummary();
    }

    private void CreateRoute()
    {
        if (_routes.Count >= 128) { _status.Text = T("Maximum of 128 routes."); return; }
        var r = new ExportationRoute { Name = T("New route") };
        _routes.Add(r); _route = r;
        _search.Text = ""; RefreshRoutes(r.Id);
        ShowRoute(); Changed(); _tabs.SelectedIndex = 0;
        _name.Focus(); _name.SelectAll();
    }

    private void DuplicateRoute()
    {
        if (_route is not { } r || _routes.Count >= 128) return;
        var clone = new ExportationRoute
        {
            Name = (r.Name.Length > 100 ? r.Name[..100] : r.Name) + " (" + T("Copy") + ")",
            SourceNpcId = r.SourceNpcId, RecipientNpcId = r.RecipientNpcId,
            SourceMapId = r.SourceMapId, RecipientMapId = r.RecipientMapId,
            QuestId = r.QuestId, MinimumProfessionLevel = r.MinimumProfessionLevel,
            RewardItemId = r.RewardItemId, RewardQuantity = r.RewardQuantity,
            ExperienceReward = r.ExperienceReward,
            Lines = r.Lines.Select(item => new CargoLine
                { ItemId = item.ItemId, Group = item.Group, Quantity = item.Quantity, Sprite = item.Sprite }).ToArray(),
        };
        _routes.Add(clone); _route = clone;
        _search.Text = ""; RefreshRoutes(clone.Id); ShowRoute(); Changed();
    }

    private void DeleteRoute()
    {
        if (_route is not { } r) return;
        if (MessageBox.Show(this, T("Deleting a route may interrupt active deliveries. Continue?"),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _routes.Remove(r);
        _route = _routes.FirstOrDefault();
        RefreshRoutes(_route?.Id ?? Guid.Empty); ShowRoute(); Changed();
    }

    private void ShowSummary()
    {
        var r = _route;
        _preview.Text = r == null ? T("Create or select a route.") : string.Join(Environment.NewLine, new[]
        {
            T("Route") + ": " + r.Name,
            T("Source map") + ": " + MapList.OrderedMaps.FirstOrDefault(m => m.MapId == r.SourceMapId)?.Name,
            T("Destination map") + ": " + MapList.OrderedMaps.FirstOrDefault(m => m.MapId == r.RecipientMapId)?.Name,
            T("Goods") + ": " + r.Lines.Length,
            T("Minimum level") + ": " + r.MinimumProfessionLevel,
            T("EXP on delivery") + ": " + r.ExperienceReward.ToString("N0", CultureInfo.CurrentCulture),
            "",
            T("Real sailing is required. Death or shipwreck destroys the cargo."),
            T("This is a configuration preview; test real deliveries in the game."),
        });
    }

    private void UpdateProfession()
    {
        var def = ProfessionConfiguration.Instance.Find(_configuration.ProfessionId);
        if (def == null)
        {
            _professionInfo.Text = T("Choose or create a custom profession in the existing Profession Editor.");
            _experienceInfo.Text = ""; _progress.Value = 0; return;
        }
        var exp = def.ClampExperience((long)_sampleExp.Value);
        var level = def.LevelForExperience(exp);
        var maximum = level >= def.MaximumLevel;
        var totalForLevel = def.ExperienceToReachLevel(level);
        var required = maximum ? 0 : Math.Max(1L, def.ExperienceForNextLevel(level));
        var into = maximum ? 0 : Math.Clamp(exp - totalForLevel, 0, required);
        _progress.Value = maximum ? 100 : (int)Math.Clamp((decimal)into * 100m / required, 0m, 100m);
        _professionInfo.Text = def.Name + " — " + T("Maximum level") + ": " + def.MaximumLevel +
            ", " + T("Base EXP") + ": " + def.BaseExperience.ToString("N0", CultureInfo.CurrentCulture) +
            ", " + T("Growth") + ": " + def.ExperienceGrowth.ToString("P0", CultureInfo.CurrentCulture);
        _experienceInfo.Text = T("Simulation") + " — " + T("Level") + " " + level +
            (maximum ? " (" + T("Maximum level reached") + ")" :
                ": " + into.ToString("N0", CultureInfo.CurrentCulture) +
                " / " + required.ToString("N0", CultureInfo.CurrentCulture) + " EXP");
    }

    private void Pending(bool active)
    {
        _pending = active;
        _tabs.Enabled = _routeList.Enabled = _search.Enabled = !active;
        _save.Enabled = _reload.Enabled = !active;
        if (active) _timeout.Start(); else _timeout.Stop();
    }

    private void Save()
    {
        if (_pending) return;
        _configuration.Routes = _routes.ToArray();
        if (_configuration.Enabled && _configuration.ProfessionId == Guid.Empty)
        {
            _status.Text = T("Choose the Exportation profession before saving."); return;
        }
        if (!_configuration.IsValid)
        {
            _status.Text = T("Complete the route: two maps, two events, a quest and the goods list.");
            return;
        }
        Pending(true);
        try
        {
            Intersect.Editor.Networking.Network.SendPacket(new ExportationRequestPacket
                { Action = ExportationAction.SaveConfiguration, ConfigurationJson = _configuration.ToJson() });
            _status.Text = T("Waiting for the server to confirm.");
        }
        catch (Exception)
        {
            Pending(false); _status.Text = T("Could not contact the server. Your draft was preserved.");
        }
    }

    private void Reload()
    {
        if (_pending) return;
        if (_dirty && MessageBox.Show(this, T("Discard the local draft?"), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            Intersect.Editor.Networking.Network.SendPacket(new ExportationRequestPacket
                { Action = ExportationAction.GetConfiguration, OpenEditor = false });
            _status.Text = T("Waiting for the server to confirm.");
        }
        catch (Exception) { _status.Text = T("Unable to contact the server."); }
    }

    private void LoadConfiguration(string json, Guid selection)
    {
        try
        {
            var next = ExportationConfiguration.FromJson(json);
            _configuration = next;
            _routes.Clear(); _routes.AddRange(next.Routes);
            var old = _loading; _loading = true;
            try
            {
                _enabled.Checked = next.Enabled;
                RefreshProfessionChoices();
                _search.Text = "";
                _route = _routes.FirstOrDefault(r => r.Id == selection) ?? _routes.FirstOrDefault();
                RefreshRoutes(_route?.Id ?? Guid.Empty);
            }
            finally { _loading = old; }
            _dirty = false; ShowRoute();
        }
        catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException)
        {
            _status.Text = T("The server returned invalid data; the local draft was preserved.");
        }
    }

    internal void Response(ExportationStatePacket packet)
    {
        if (IsDisposed || !packet.Editor) return;
        if (_pending) Pending(false);
        if (packet.Saved && !string.IsNullOrWhiteSpace(packet.ConfigurationJson))
        {
            LoadConfiguration(packet.ConfigurationJson, _route?.Id ?? Guid.Empty);
            _status.Text = T("Exportation routes saved.");
        }
        else if (!string.IsNullOrWhiteSpace(packet.ConfigurationJson) && !_dirty)
        {
            LoadConfiguration(packet.ConfigurationJson, _route?.Id ?? Guid.Empty);
            _status.Text = T("Server configuration loaded.");
        }
        else if (!string.IsNullOrWhiteSpace(packet.Message)) _status.Text = packet.Message;
    }

    private void RefreshGoods()
    {
        _goods.BeginUpdate(); _goods.Items.Clear();
        foreach (var line in _route?.Lines ?? [])
        {
            var item = new ListViewItem(ItemDescriptor.GetName(line.ItemId)) { Tag = line };
            item.SubItems.Add(line.Quantity.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(T(line.Group.ToString()));
            item.SubItems.Add(string.IsNullOrEmpty(line.Sprite) ? T("Default") : line.Sprite);
            _goods.Items.Add(item);
        }
        _goods.EndUpdate();
    }

    private void SelectGood()
    {
        if (_loading || _goods.SelectedItems.Count == 0 ||
            _goods.SelectedItems[0].Tag is not CargoLine good) return;
        var old = _loading; _loading = true;
        try
        {
            Fill(_goodItem, ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                .Select(item => new Named(item.Id, item.Name)), good.ItemId, T("Select item..."));
            _goodGroup.SelectedIndex = (int)good.Group;
            _goodQty.Value = good.Quantity;
            _goodSprite.SelectedIndex = Math.Max(0, Array.IndexOf(ExportationArt.Goods, good.Sprite) + 1);
        }
        finally { _loading = old; }
    }

    private void AddOrUpdateGood()
    {
        if (_route is not { } r || Id(_goodItem) == Guid.Empty ||
            _goodGroup.SelectedIndex < 0) return;
        var selected = _goods.SelectedItems.Count > 0 ? _goods.SelectedItems[0].Tag as CargoLine : null;
        if (selected == null && r.Lines.Length >= 48) { _status.Text = T("Maximum 48 goods per route."); return; }
        var id = Id(_goodItem);
        if (r.Lines.Any(x => x.ItemId == id && x != selected))
        { _status.Text = T("This item is already in the order."); return; }
        var good = new CargoLine
        {
            ItemId = id, Quantity = (int)_goodQty.Value,
            Group = (CargoGroup)_goodGroup.SelectedIndex,
            Sprite = _goodSprite.SelectedIndex > 0 ? ExportationArt.Goods[_goodSprite.SelectedIndex - 1] : "",
        };
        if (!good.IsValid) return;
        var lines = r.Lines.ToList();
        if (selected == null) lines.Add(good); else lines[lines.IndexOf(selected)] = good;
        r.Lines = lines.ToArray();
        RefreshGoods(); Changed();
    }

    private void RemoveGood()
    {
        if (_route is not { } r || _goods.SelectedItems.Count == 0 ||
            _goods.SelectedItems[0].Tag is not CargoLine line) return;
        r.Lines = r.Lines.Where(x => x != line).ToArray();
        RefreshGoods(); Changed();
    }
}
