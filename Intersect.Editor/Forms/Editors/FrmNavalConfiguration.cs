using System.Globalization;
using Intersect.Framework.Core.GameObjects.Animations;
using System.Reflection;
using DarkUI.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmNavalConfiguration : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private HerboniaPatrol? _copiedPatrol;
    private readonly NavalConfiguration _navigation;
    private readonly HarbourCatalog _catalogue;
    private readonly HashSet<string> _publishedModels;
    private Action? _updateOfferRemove;
    private string _navigationVersion, _catalogueVersion;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Multiline = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 45 };
    private readonly Button _save = new() { AutoSize = true };
    private readonly bool[] _dirty = new bool[2];
    private readonly System.Windows.Forms.Timer _timeout = new() { Interval = 10000 };
    private byte _pending;
    private Guid _pendingRequest;
    internal static string T(string source) => NavalSetupText.Get(source, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public FrmNavalConfiguration(NavalConfigurationPacket packet)
    {
        _navigation = JsonConvert.DeserializeObject<NavalConfiguration>(packet.NavigationJson) ?? new();
        _catalogue = JsonConvert.DeserializeObject<HarbourCatalog>(packet.CatalogueJson) ?? new();
        _publishedModels = _catalogue.Offers.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        _navigationVersion = packet.NavigationVersion; _catalogueVersion = packet.CatalogueVersion;
        Text = T("Naval System Editor"); Size = new(1050, 780); MinimumSize = new(850, 620);
        StartPosition = FormStartPosition.CenterParent;
        var general = Page("Navigation and harbour");
        general.Controls.Add(Fields(_navigation, 0, ["CareersVersion", "Ships", "SourceJson", "AdditionalData", "Presentation", "Sailing", "Herbonia", "Gunnery", "Ammunition", "Repair", "Skills"]));
        AddListPage("Test ships", _navigation.Ships.ToList(), 0, () => new ShipDefinition
            { Graphics = "Ship_CorpsRoyaux_002.png", DamagedGraphics = "Ship_CorpsRoyaux_005.png" },
            ships => _navigation.Ships = ships.ToArray());
        AddListPage("Ship shop and repairs", _catalogue.Offers.ToList(), 1,
            () => new HarbourOffer { ForSale = false, Id = "ship-" + Guid.NewGuid().ToString("N")[..12],
                Ship = new() { Graphics = "Ship_CorpsRoyaux_002.png", DamagedGraphics = "Ship_CorpsRoyaux_005.png" } },
            offers => _catalogue.Offers = offers.ToArray());
        Page("Sounds, icons and effects").Controls.Add(Fields(_navigation.Presentation,0,[]));
        Page("Sailing progression").Controls.Add(Fields(_navigation.Sailing,0,[]));
        AddListPage("Herbonia AI patrols",_navigation.Herbonia.ToList(),0,()=>new HerboniaPatrol(),patrols=>_navigation.Herbonia=patrols.ToArray());
        Page("Crew cannons and gunnery").Controls.Add(Fields(_navigation.Gunnery,0,[]));
        AddListPage("Cannon ammunition",_navigation.Ammunition.ToList(),0,()=>CannonAmmunition.Defaults().FirstOrDefault(a=>!_navigation.Ammunition.Any(old=>old.Kind==a.Kind))??new CannonAmmunition(),ammo=>_navigation.Ammunition=ammo.ToArray());
        Page("Ship repair activities").Controls.Add(Fields(_navigation.Repair,0,[]));
        AddListPage("Sailing skills",_navigation.Skills.ToList(),0,()=>NavalSkillDefinition.Defaults().FirstOrDefault(a=>!_navigation.Skills.Any(old=>old.Kind==a.Kind))??new NavalSkillDefinition(),skills=>_navigation.Skills=skills.ToArray());
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft };
        _save.Text = T("Save current section");
        var close = new Button { Text = T("Close"), AutoSize = true };
        footer.Controls.AddRange([close, _save]);
        var help = new Label { Dock = DockStyle.Bottom, Height = 52,
            Text = T("Use Q docks for your fleet. In an NPC event, use Open ship shop and repairs."), Padding = new(8) };
        Controls.Add(_tabs); Controls.Add(help); Controls.Add(_status); Controls.Add(footer);
        _save.Click += (_, _) => SaveSection(); close.Click += (_, _) => Close();
        _tabs.SelectedIndexChanged += (_, _) => SetStatus();
        _timeout.Tick += (_, _) => { _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true;
            _status.Text = T("No reply yet. Changes are kept; retry or reload the editor."); };
        FormClosing += (_, e) =>
        {
            if (_pending != 0) { e.Cancel = true; return; }
            if (_dirty.Any(d => d) && MessageBox.Show(this, T("Discard unsaved changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                e.Cancel = true;
        };
        FormClosed += (_, _) => { _timeout.Dispose(); _navalSoundPreview?.Stop(); _navalSoundPreview?.Dispose(); };
        MiniGameEditorTheme.Apply(this); SetStatus();
    }

    private TabPage Page(string title)
    {
        var tab = new TabPage(T(title)); _tabs.TabPages.Add(tab); return tab;
    }
    private void Changed(int section) { _dirty[section] = true; SetStatus(); }
    private void SetStatus() => _status.Text = _dirty.Any(d => d) ? T("Unsaved changes") : T("Settings received from the server.");

    private void AddListPage<TItem>(string title, List<TItem> items, int section, Func<TItem> create, Action<List<TItem>> store)
        where TItem : class
    {
        var tab = Page(title);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1000, 620), SplitterDistance = 275 };
        var list = new ListBox { Dock = DockStyle.Fill };
        list.Format += (_, e) => e.Value = e.ListItem is HarbourOffer offer ? offer.Name :
            e.ListItem is NavalSkillDefinition skill ? T(NavalSkillDefinition.Caption(skill.Kind)) :
            e.ListItem is CannonAmmunition ammunition ? T(CannonAmmunition.Caption(ammunition.Kind)) :
            e.ListItem is HerboniaPatrol patrol ? $"{patrol.Ship.Name} ({patrol.Ship.X}, {patrol.Ship.Y})" :
            e.ListItem is ShipDefinition ship ? $"{MapList.OrderedMaps.FirstOrDefault(m => m.MapId == ship.MapId)?.Name ?? ship.MapId.ToString()} ({ship.X}, {ship.Y})" : "";
        list.FormattingEnabled = true;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
        var add = new Button { Text = T("Add"), AutoSize = true };
        var remove = new Button { Text = T("Remove"), AutoSize = true };
        buttons.Controls.AddRange([add, remove]); split.Panel1.Controls.Add(list); split.Panel1.Controls.Add(buttons);
        var limit=typeof(TItem)==typeof(NavalSkillDefinition)?5:typeof(TItem)==typeof(CannonAmmunition)?8:typeof(TItem)==typeof(ShipDefinition)?64:32;
        void Populate(TItem? selected)
        {
            add.Enabled=items.Count<limit;
            list.Items.Clear(); list.Items.AddRange(items.Cast<object>().ToArray()); list.SelectedItem = selected;
        }
        void UpdateRemove() => remove.Enabled = list.SelectedItem is TItem selected &&
            (selected is not HarbourOffer offer || !_publishedModels.Contains(offer.Id));
        if (section == 1) _updateOfferRemove = UpdateRemove;
        list.SelectedIndexChanged += (_, _) =>
        {
            UpdateRemove();
            foreach (Control control in split.Panel2.Controls.Cast<Control>().ToArray()) control.Dispose();
            if (list.SelectedItem is not TItem item) return;
            var fields = Fields(item, section, item is HarbourOffer or HerboniaPatrol ? ["Ship", "Loot", "AdditionalData"] : ["AdditionalData"]);
            if (item is HarbourOffer offer)
            {
                var model = Fields(offer.Ship, section, ["Id", "MapId", "X", "Y", "DockX", "DockY", "SinkingGraphics", "AdditionalData"]);
                model.Dock = DockStyle.Top; model.AutoSize = true; model.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                var group = new GroupBox { Text = T("Ship model and movement"), Dock = DockStyle.Top, AutoSize = true, Padding = new(6, 24, 6, 6) };
                group.Controls.Add(model); fields.Controls.Add(group, 0, fields.RowCount++); fields.SetColumnSpan(group, 2);
            }
            if(item is HerboniaPatrol patrol)
            {
                var loot=LootFields(patrol,section);fields.Controls.Add(loot,0,fields.RowCount++);fields.SetColumnSpan(loot,2);
                var model=Fields(patrol.Ship,section,["AdditionalData","DockX","DockY"]);
                model.Dock=DockStyle.Top;model.AutoSize=true;model.AutoSizeMode=AutoSizeMode.GrowAndShrink;
                var group=new GroupBox {Text=T("Ship model and movement"),Dock=DockStyle.Top,AutoSize=true,Padding=new(6,24,6,6)};
                group.Controls.Add(model);fields.Controls.Add(group,0,fields.RowCount++);fields.SetColumnSpan(group,2);
            }
            split.Panel2.Controls.Add(fields); MiniGameEditorTheme.Apply(split.Panel2);
        };
        add.Click += (_, _) =>
        {
            if (items.Count >= limit) return;
            var item = create(); items.Add(item); store(items); Changed(section); Populate(item);
        };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not TItem item) return;
            if (item is HarbourOffer offer && _publishedModels.Contains(offer.Id))
            { _status.Text = T("Keep existing model ids. Disable For sale to retire a model without losing fleets."); return; }
            items.Remove(item); store(items); Changed(section); Populate(items.FirstOrDefault());
        };
        if (typeof(TItem)==typeof(HerboniaPatrol))
        {
            buttons.Height=76;
            var copy=new Button {Text=T("Copy patrol"), AutoSize=true};
            var paste=new Button {Text=T("Paste patrol"), AutoSize=true,Enabled=false};
            buttons.Controls.AddRange([copy,paste]);
            copy.Click+=(_,_)=>
            {
                if (list.SelectedItem is HerboniaPatrol selected)
                {
                    _copiedPatrol=JsonConvert.DeserializeObject<HerboniaPatrol>(JsonConvert.SerializeObject(selected));
                    paste.Enabled=_copiedPatrol!=null;
                }
            };
            paste.Click+=(_,_)=>
            {
                if (_copiedPatrol==null || list.SelectedItem is not HerboniaPatrol selected) return;
                var replacement=JsonConvert.DeserializeObject<HerboniaPatrol>(JsonConvert.SerializeObject(_copiedPatrol));
                if (replacement==null) return;
                replacement.Ship.Id=selected.Ship.Id;
                var index=items.IndexOf((TItem)(object)selected);
                items[index]=(TItem)(object)replacement;
                store(items);Changed(section);Populate(items[index]);
            };
        }
        tab.Controls.Add(split); Populate(items.FirstOrDefault());
        // An empty catalogue must still be enableable before adding the first offer.
        if (section == 1)
        {
            var enabled = new CheckBox { Text = T("Enable shop"), Dock = DockStyle.Top, Checked = _catalogue.Enabled, Height = 32 };
            enabled.CheckedChanged += (_, _) => { _catalogue.Enabled = enabled.Checked; Changed(1); };
            tab.Controls.Add(enabled); enabled.SendToBack();
        }
    }

    private Control LootFields(HerboniaPatrol patrol,int section)
    {
        var group=new GroupBox {Text=T("Naval loot table"),Dock=DockStyle.Top,Height=340,Padding=new(6,20,6,6)};
        var list=new ListBox {Dock=DockStyle.Left,Width=180,FormattingEnabled=true};
        var detail=new Panel {Dock=DockStyle.Fill};
        var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=36};
        var add=new Button {Text=T("Add"),AutoSize=true};var remove=new Button {Text=T("Remove"),AutoSize=true};buttons.Controls.AddRange([add,remove]);
        list.Format+=(_,e)=>e.Value=e.ListItem is NavalLootEntry entry ? ItemDescriptor.Get(entry.ItemId)?.Name??T("Automatic / not selected") : "";
        void Populate(object selected) {list.Items.Clear();list.Items.AddRange(patrol.Loot);list.SelectedItem=selected;}
        list.SelectedIndexChanged+=(_,_)=>
        {
            foreach(Control child in detail.Controls.Cast<Control>().ToArray()) child.Dispose();
            if(list.SelectedItem is NavalLootEntry entry) detail.Controls.Add(Fields(entry,section,[]));
            MiniGameEditorTheme.Apply(detail);
        };
        add.Click+=(_,_)=>{if(patrol.Loot.Length>=32) return;var entry=new NavalLootEntry();patrol.Loot=[..patrol.Loot,entry];Changed(section);Populate(entry);};
        remove.Click+=(_,_)=>{if(list.SelectedItem is not NavalLootEntry entry) return;patrol.Loot=patrol.Loot.Where(e=>e!=entry).ToArray();Changed(section);Populate(patrol.Loot.FirstOrDefault());};
        group.Controls.Add(detail);group.Controls.Add(list);group.Controls.Add(buttons);Populate(patrol.Loot.FirstOrDefault());return group;
    }

    private TableLayoutPanel Fields(object target, int section, string[] skip)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new(12), AutoSize = false };
        panel.ColumnStyles.Add(new(SizeType.Absolute, 230)); panel.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (var property in target.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite && !skip.Contains(p.Name)))
        {
            var row = panel.RowCount++; var value = property.GetValue(target); Control field;
            void Set(object? changed) { property.SetValue(target, changed); Changed(section); }
            if ((target is NavalPresentation || target is CannonAmmunition && property.Name is "Icon" or "ProjectileGraphic" or "WaterGraphic" or "HullGraphic" || target is NavalSkillDefinition && property.Name=="Icon") && property.PropertyType==typeof(string))
                field=AssetField(property.Name,value?.ToString()??"",changed=>Set(changed));
            else if(property.PropertyType==typeof(NavalSkillKind))
            {
                var box=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
                box.Items.AddRange(Enum.GetValues<NavalSkillKind>().Select(k=>T(NavalSkillDefinition.Caption(k))).ToArray());box.SelectedIndex=(int)(NavalSkillKind)value!;
                box.SelectedIndexChanged+=(_,_)=>Set((NavalSkillKind)box.SelectedIndex);field=box;
            }
            else if(property.PropertyType==typeof(CannonAmmoKind))
            {
                var box=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
                box.Items.AddRange(Enum.GetValues<CannonAmmoKind>().Select(k=>T(CannonAmmunition.Caption(k))).ToArray());box.SelectedIndex=(int)(CannonAmmoKind)value!;
                box.SelectedIndexChanged+=(_,_)=>Set((CannonAmmoKind)box.SelectedIndex);field=box;
            }
            else if(property.PropertyType==typeof(NavalCargoKind))
            {
                var box=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
                box.Items.AddRange(Enum.GetValues<NavalCargoKind>().Select(k=>T(NavalCargo.Caption(k))).ToArray());box.SelectedIndex=(int)(NavalCargoKind)value!;
                box.SelectedIndexChanged+=(_,_)=>Set((NavalCargoKind)box.SelectedIndex);field=box;
            }
            else if (property.PropertyType == typeof(bool))
            {
                var box = new CheckBox { Checked = (bool)value!, Dock = DockStyle.Fill };
                box.CheckedChanged += (_, _) => Set(box.Checked); field = box;
            }
            else if (property.PropertyType == typeof(Guid) && property.Name != "Id")
            {
                var choices = new List<Choice> { new(Guid.Empty, T("Automatic / not selected")) };
                if(property.Name=="ImpactAnimationId") choices.AddRange(AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a=>a.Name).Select(a=>new Choice(a.Id,a.Name)));
                else if(property.Name=="ItemId") choices.AddRange(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().OrderBy(i=>i.Name).Select(i=>new Choice(i.Id,i.Name)));
                else if (property.Name == "MapId") choices.AddRange(MapList.OrderedMaps.Select(m => new Choice(m.MapId, m.Name)));
                else choices.AddRange(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Currency).OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)));
                var id = (Guid)value!; if (!choices.Any(c => c.Id == id)) choices.Add(new(id, id.ToString()));
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                box.Items.AddRange(choices.ToArray()); box.SelectedItem = choices.First(c => c.Id == id);
                box.SelectedIndexChanged += (_, _) => Set(((Choice)box.SelectedItem!).Id); field = box;
            }
            else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
            {
                var number = new NumericUpDown { Minimum = property.Name == "Heading" ? -360 : 0, Maximum = int.MaxValue, DecimalPlaces = property.PropertyType == typeof(double) ? 3 : 0,
                    Increment = property.PropertyType == typeof(double) ? .1m : 1, Dock = DockStyle.Fill };
                number.Value = Math.Clamp(Convert.ToDecimal(value, CultureInfo.InvariantCulture), number.Minimum, number.Maximum);
                number.ValueChanged += (_, _) => { if (property.PropertyType == typeof(double)) Set((double)number.Value); else Set((int)number.Value); }; field = number;
            }
            else if (property.Name is "Graphics" or "DamagedGraphics" or "SinkingGraphics" or "IdleGraphics")
            {
                var box = new ComboBox { Dock = DockStyle.Fill, Text = value?.ToString() ?? "" };
                var directory = Path.Combine("resources", "ocean");
                if (Directory.Exists(directory))
                    box.Items.AddRange(Directory.GetFiles(directory, "*.naval.json").Select(Path.GetFileName)
                        .Select(n => n![..^11]).Where(n => n.EndsWith("_sink.png", StringComparison.OrdinalIgnoreCase) ==
                            (property.Name == "SinkingGraphics")).OrderBy(n => n).Cast<object>().ToArray());
                box.TextChanged += (_, _) => Set(box.Text); field = box;
            }
            else if (property.Name == "Currency")
            {
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                box.Items.AddRange(["Aureons", "Auragems"]); box.SelectedItem = value;
                box.SelectedIndexChanged += (_, _) => Set(box.SelectedItem?.ToString()); field = box;
            }
            else
            {
                var text = new TextBox { Text = value?.ToString() ?? "", Dock = DockStyle.Fill, ReadOnly = property.Name == "Id" };
                text.TextChanged += (_, _) => Set(text.Text); field = text;
            }
            field.Name = property.Name;
            panel.RowStyles.Add(new(SizeType.Absolute, (target is NavalPresentation or CannonAmmunition or NavalSkillDefinition) && (property.Name.EndsWith("Icon") || property.Name=="CrewCannonGraphic")?78:35));
            panel.Controls.Add(new Label { Text = T(target is HerboniaPatrol && property.Name=="Enabled" ? "Enable patrol" : target is GunnerySettings && property.Name=="Enabled"?"Enable crew cannons":target is GunnerySettings && property.Name=="MaximumLevel"?"Maximum Gunnery level":property.Name), Dock = DockStyle.Top, Height = 35, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            panel.Controls.Add(field, 1, row);
        }
        panel.RowStyles.Add(new(SizeType.Percent,100));panel.RowCount++;
        return panel;
    }

    internal NavalConfigurationRequestPacket PrepareSave()
    {
        var section = _tabs.SelectedIndex == 2 ? (byte)2 : (byte)1;
        if (section == 1) _navigation.Validate(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
        else _catalogue.Validate();
        return new() { Section = section, ExpectedVersion = section == 1 ? _navigationVersion : _catalogueVersion,
            ConfigurationJson = JsonConvert.SerializeObject(section == 1 ? (object)_navigation : _catalogue) };
    }
    private void SaveSection()
    {
        try
        {
            ValidateChildren(); var request = PrepareSave();
            _pending = request.Section; _pendingRequest = request.RequestId; _save.Enabled = false; _tabs.Enabled = false; _timeout.Start();
            Intersect.Editor.Networking.Network.SendPacket(request); _status.Text = T("Saving...");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or JsonException)
        { _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true; _status.Text = T(e.Message); }
        catch
        {
            _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true;
            _status.Text = T("The naval configuration could not be read or saved. Existing data was preserved.");
        }
    }
    public void AcceptResponse(NavalConfigurationPacket packet)
    {
        if (_pendingRequest == Guid.Empty || packet.RequestId != _pendingRequest) return;
        _timeout.Stop(); _pending = 0; _pendingRequest = Guid.Empty; _save.Enabled = true; _tabs.Enabled = true;
        if (packet.Error.Length > 0) { _status.Text = T(packet.Error); return; }
        if (packet.SavedSection is 1 or 2) _dirty[packet.SavedSection - 1] = false;
        if (packet.SavedSection == 1) _navigationVersion = packet.NavigationVersion;
        if (packet.SavedSection == 2)
        {
            _catalogueVersion = packet.CatalogueVersion;
            _publishedModels.UnionWith(_catalogue.Offers.Select(o => o.Id));
            _updateOfferRemove?.Invoke();
        }
        _status.Text = T("Saved on the server.");
    }
}
