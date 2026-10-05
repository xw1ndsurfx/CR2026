using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Content;
using Intersect.Editor.Networking;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.RoyalStylist;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmRoyalStylistConfiguration : DarkForm
{
    private sealed record ItemChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record StyleChoice(string File, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly RoyalStylistConfiguration _working;
    private readonly List<RoyalStylistDefinition> _stylists;
    private RoyalStylistDefinition? _selected;

    private readonly ListBox _list =
        new() { Dock = DockStyle.Fill };

    private readonly DarkTextBox _name =
        new() { Dock = DockStyle.Fill };

    private readonly DarkTextBox _description =
        new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 70,
        };

    private readonly CheckBox _premiumRequired =
        new()
        {
            Text = "Premium membership required",
            AutoSize = true,
            Checked = true,
        };

    private readonly ComboBox _currency =
        new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

    private readonly DarkNumericUpDown _price =
        new()
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1_000_000_000,
            ThousandsSeparator = true,
        };

    private readonly DarkNumericUpDown _sort =
        new()
        {
            Dock = DockStyle.Fill,
            Minimum = -100000,
            Maximum = 100000,
        };

    private readonly CheckedListBox _hair =
        CreateStyleList();
    private readonly CheckedListBox _shirts =
        CreateStyleList();
    private readonly CheckedListBox _pants =
        CreateStyleList();
    private readonly CheckedListBox _boots =
        CreateStyleList();

    private readonly CheckBox _allowNoHair =
        new() { Text = "Allow None", AutoSize = true, Checked = true };
    private readonly CheckBox _allowNoShirt =
        new() { Text = "Allow None", AutoSize = true };
    private readonly CheckBox _allowNoPants =
        new() { Text = "Allow None", AutoSize = true };
    private readonly CheckBox _allowNoBoots =
        new() { Text = "Allow None", AutoSize = true };

    public FrmRoyalStylistConfiguration()
    {
        Text = "Royal Stylists";
        Width = 1120;
        Height = 760;
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;

        _working = RoyalStylistConfiguration.FromJson(
            RoyalStylistConfiguration.Instance.ToJson()
        );
        _stylists = _working.Stylists.ToList();

        PopulateCurrencies();
        PopulateStyles();

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };

        var cancel = new DarkButton
        {
            Text = "Cancel",
            Width = 105,
            Height = 30,
        };
        var save = new DarkButton
        {
            Text = "Save",
            Width = 105,
            Height = 30,
        };

        cancel.Click += (_, _) => Close();
        save.Click += (_, _) => Save();
        footer.Controls.Add(cancel);
        footer.Controls.Add(save);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 300)
        );
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100)
        );

        root.Controls.Add(BuildList(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);

        Controls.Add(root);
        Controls.Add(footer);

        _list.SelectedIndexChanged += (_, _) =>
        {
            CommitSelected();
            _selected =
                _list.SelectedItem as RoyalStylistDefinition;
            LoadSelected();
        };

        RefreshList();
    }

    private static CheckedListBox CreateStyleList() =>
        new()
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };

    private Control BuildList()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 10, 0),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
        };

        var add = new DarkButton
        {
            Text = "+ New",
            Width = 82,
            Height = 30,
        };
        var duplicate = new DarkButton
        {
            Text = "Duplicate",
            Width = 92,
            Height = 30,
        };
        var remove = new DarkButton
        {
            Text = "Delete",
            Width = 82,
            Height = 30,
        };

        add.Click += (_, _) =>
        {
            CommitSelected();
            var stylist = new RoyalStylistDefinition
            {
                CurrencyItemId = FindDefaultCurrencyId(),
            };
            _stylists.Add(stylist);
            RefreshList(stylist.Id);
        };

        duplicate.Click += (_, _) =>
        {
            if (_selected == null)
            {
                return;
            }

            CommitSelected();
            var copy = RoyalStylistConfiguration.FromJson(
                new RoyalStylistConfiguration
                {
                    Stylists = [_selected],
                }.ToJson()
            ).Stylists[0];

            copy.Id = Guid.NewGuid();
            copy.Name += " Copy";
            _stylists.Add(copy);
            RefreshList(copy.Id);
        };

        remove.Click += (_, _) =>
        {
            if (_selected == null)
            {
                return;
            }

            if (MessageBox.Show(
                    this,
                    $"Delete Royal Stylist '{_selected.Name}'?",
                    "Royal Stylists",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                ) != DialogResult.Yes)
            {
                return;
            }

            _stylists.RemoveAll(
                stylist => stylist.Id == _selected.Id
            );
            _selected = null;
            RefreshList();
        };

        buttons.Controls.Add(add);
        buttons.Controls.Add(duplicate);
        buttons.Controls.Add(remove);
        panel.Controls.Add(_list);
        panel.Controls.Add(buttons);
        return panel;
    }

    private Control BuildEditor()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
        };
        table.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 190)
        );
        table.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100)
        );

        AddRow(table, "Name", _name, 38);
        AddRow(table, "Description", _description, 78);
        AddRow(table, "Access", _premiumRequired, 38);
        AddRow(table, "Currency", _currency, 38);
        AddRow(table, "Price", _price, 38);
        AddRow(table, "Sort order", _sort, 38);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(
            CreateStyleTab("Hair", _hair, _allowNoHair)
        );
        tabs.TabPages.Add(
            CreateStyleTab("Shirts", _shirts, _allowNoShirt)
        );
        tabs.TabPages.Add(
            CreateStyleTab("Pants", _pants, _allowNoPants)
        );
        tabs.TabPages.Add(
            CreateStyleTab("Boots", _boots, _allowNoBoots)
        );
        AddRow(table, "Styles offered", tabs, 390);

        return table;
    }

    private static TabPage CreateStyleTab(
        string title,
        CheckedListBox list,
        CheckBox allowNone
    )
    {
        var page = new TabPage(title);
        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
        };
        allowNone.Location = new System.Drawing.Point(8, 8);
        top.Controls.Add(allowNone);
        page.Controls.Add(list);
        page.Controls.Add(top);
        return page;
    }

    private static void AddRow(
        TableLayoutPanel table,
        string label,
        Control control,
        int height
    )
    {
        var row = table.RowCount++;
        table.RowStyles.Add(
            new RowStyle(SizeType.Absolute, height)
        );
        table.Controls.Add(
            new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            },
            0,
            row
        );
        control.Margin = new Padding(4);
        table.Controls.Add(control, 1, row);
    }

    private static Guid FindDefaultCurrencyId()
    {
        var compatible = MiniGameCurrency.CompatibleItems(
                ItemDescriptor.Lookup.Values
                    .OfType<ItemDescriptor>())
            .ToArray();

        var aureons = compatible.FirstOrDefault(item =>
            string.Equals(
                MiniGameCurrency.DisplayName(item),
                "Aureons",
                StringComparison.OrdinalIgnoreCase
            ));

        return aureons?.Id ??
               compatible.FirstOrDefault()?.Id ??
               Guid.Empty;
    }

    private void PopulateCurrencies()
    {
        _currency.Items.Clear();
        _currency.Items.Add(
            new ItemChoice(Guid.Empty, "(None / Free)")
        );

        foreach (var item in MiniGameCurrency.CompatibleItems(
                     ItemDescriptor.Lookup.Values
                         .OfType<ItemDescriptor>()))
        {
            _currency.Items.Add(
                new ItemChoice(
                    item.Id,
                    MiniGameCurrency.DisplayName(item)
                )
            );
        }

        _currency.SelectedIndex = 0;

        var defaultCurrencyId = FindDefaultCurrencyId();
        if (defaultCurrencyId != Guid.Empty)
        {
            SelectCurrency(defaultCurrencyId);
        }
    }

    private void PopulateStyles()
    {
        var paperdolls =
            GameContentManager.GetTextureNames(
                GameContentManager.TextureType.Paperdoll
            ) ?? [];

        FillStyles(
            _hair,
            paperdolls,
            CharacterAppearance.HairPrefix
        );
        FillStyles(
            _shirts,
            paperdolls,
            CharacterAppearance.ShirtPrefix
        );
        FillStyles(
            _pants,
            paperdolls,
            CharacterAppearance.PantsPrefix
        );
        FillStyles(
            _boots,
            paperdolls,
            CharacterAppearance.BootsPrefix
        );
    }

    private static void FillStyles(
        CheckedListBox list,
        IEnumerable<string> paperdolls,
        string prefix
    )
    {
        list.Items.Clear();

        foreach (var file in paperdolls
                     .Where(file =>
                         CharacterAppearance.IsCatalogStyle(
                             file,
                             prefix
                         ))
                     .OrderBy(
                         file => file,
                         StringComparer.OrdinalIgnoreCase
                     ))
        {
            list.Items.Add(
                new StyleChoice(file, FriendlyStyle(file, prefix))
            );
        }
    }

    private static string FriendlyStyle(
        string file,
        string prefix
    )
    {
        var value = Path.GetFileNameWithoutExtension(file);
        if (value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            value = value[prefix.Length..];
        }

        value = value.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return file;
        }

        return string.Join(
            " ",
            value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(part =>
                    char.ToUpperInvariant(part[0]) + part[1..])
        );
    }

    private void RefreshList(Guid? selectId = null)
    {
        var id = selectId ?? _selected?.Id;

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var stylist in _stylists
                     .OrderBy(stylist => stylist.SortOrder)
                     .ThenBy(stylist => stylist.Name))
        {
            _list.Items.Add(stylist);
        }

        _list.DisplayMember =
            nameof(RoyalStylistDefinition.Name);
        _list.EndUpdate();

        if (id.HasValue)
        {
            for (var i = 0; i < _list.Items.Count; i++)
            {
                if (_list.Items[i] is RoyalStylistDefinition stylist &&
                    stylist.Id == id)
                {
                    _list.SelectedIndex = i;
                    return;
                }
            }
        }

        _list.SelectedIndex =
            _list.Items.Count > 0 ? 0 : -1;

        if (_list.SelectedIndex < 0)
        {
            LoadSelected();
        }
    }

    private void LoadSelected()
    {
        var enabled = _selected != null;
        foreach (var control in new Control[]
                 {
                     _name,
                     _description,
                     _premiumRequired,
                     _currency,
                     _price,
                     _sort,
                     _hair,
                     _shirts,
                     _pants,
                     _boots,
                     _allowNoHair,
                     _allowNoShirt,
                     _allowNoPants,
                     _allowNoBoots,
                 })
        {
            control.Enabled = enabled;
        }

        if (_selected == null)
        {
            return;
        }

        _name.Text = _selected.Name;
        _description.Text = _selected.Description;
        _premiumRequired.Checked = _selected.PremiumRequired;
        _price.Value = Math.Clamp(
            _selected.Price,
            (int)_price.Minimum,
            (int)_price.Maximum
        );
        _sort.Value = Math.Clamp(
            _selected.SortOrder,
            (int)_sort.Minimum,
            (int)_sort.Maximum
        );

        _allowNoHair.Checked = _selected.AllowNoHair;
        _allowNoShirt.Checked = _selected.AllowNoShirt;
        _allowNoPants.Checked = _selected.AllowNoPants;
        _allowNoBoots.Checked = _selected.AllowNoBoots;

        SelectCurrency(_selected.CurrencyItemId);
        SetChecked(_hair, _selected.HairStyles);
        SetChecked(_shirts, _selected.ShirtStyles);
        SetChecked(_pants, _selected.PantsStyles);
        SetChecked(_boots, _selected.BootsStyles);
    }

    private void SelectCurrency(Guid id)
    {
        for (var i = 0; i < _currency.Items.Count; i++)
        {
            if (_currency.Items[i] is ItemChoice choice &&
                choice.Id == id)
            {
                _currency.SelectedIndex = i;
                return;
            }
        }

        _currency.SelectedIndex = 0;
    }

    private static void SetChecked(
        CheckedListBox list,
        IEnumerable<string> selected
    )
    {
        var lookup = selected.ToHashSet(
            StringComparer.OrdinalIgnoreCase
        );

        for (var i = 0; i < list.Items.Count; i++)
        {
            var choice = (StyleChoice)list.Items[i]!;
            list.SetItemChecked(i, lookup.Contains(choice.File));
        }
    }

    private static string[] CheckedStyles(CheckedListBox list) =>
        list.CheckedItems
            .Cast<StyleChoice>()
            .Select(choice => choice.File)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void CommitSelected()
    {
        if (_selected == null)
        {
            return;
        }

        _selected.Name = string.IsNullOrWhiteSpace(_name.Text)
            ? "Royal Stylist"
            : _name.Text.Trim();

        _selected.Description =
            _description.Text?.Trim() ?? string.Empty;
        _selected.PremiumRequired = _premiumRequired.Checked;
        _selected.CurrencyItemId =
            (_currency.SelectedItem as ItemChoice)?.Id ??
            Guid.Empty;
        _selected.Price = (int)_price.Value;
        _selected.SortOrder = (int)_sort.Value;

        _selected.AllowNoHair = _allowNoHair.Checked;
        _selected.AllowNoShirt = _allowNoShirt.Checked;
        _selected.AllowNoPants = _allowNoPants.Checked;
        _selected.AllowNoBoots = _allowNoBoots.Checked;

        _selected.HairStyles = CheckedStyles(_hair);
        _selected.ShirtStyles = CheckedStyles(_shirts);
        _selected.PantsStyles = CheckedStyles(_pants);
        _selected.BootsStyles = CheckedStyles(_boots);
    }

    private void Save()
    {
        CommitSelected();
        _working.Stylists = _stylists.ToArray();

        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(
                this,
                "The Royal Stylist configuration is invalid. " +
                "A paid stylist must have a currency item.",
                "Royal Stylists",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        PacketSender.SendSaveRoyalStylistConfiguration(
            _working.ToJson()
        );
        RoyalStylistConfiguration.Load(_working.ToJson());
        Close();
    }
}
