using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.LogiCoins;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmLogiCoinShopConfiguration : DarkForm
{
    private sealed record ItemChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record VariableChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record OfferChoice(LogiCoinShopOffer Offer, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly LogiCoinShopConfiguration _working;
    private readonly List<LogiCoinShopOffer> _offers;
    private readonly CheckBox _enabled = new() { Text = "Enable LogiCoin Shop", AutoSize = true };
    private readonly TextBox _shopUrl = new() { Width = 420 };
    private readonly ComboBox _premiumVariable = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ListBox _offerList = new() { Dock = DockStyle.Fill };

    public FrmLogiCoinShopConfiguration()
    {
        Text = "LogiCoin Shop";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 900;
        Height = 680;
        MinimizeBox = false;
        MaximizeBox = false;

        _working = LogiCoinShopConfiguration.FromJson(LogiCoinShopConfiguration.Instance.ToJson());
        _offers = (_working.Offers ?? []).Select(CloneOffer).ToList();

        _enabled.Checked = _working.Enabled;
        _shopUrl.Text = _working.ShopUrl;
        FillPremiumVariables();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };
        settings.Controls.Add(_enabled);
        settings.Controls.Add(new Label
        {
            Text = "Buy more LogiCoins URL",
            AutoSize = true,
            Margin = new Padding(20, 7, 4, 0),
        });
        settings.Controls.Add(_shopUrl);

        var premium = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };
        premium.Controls.Add(new Label
        {
            Text = "Premium expiry User Variable",
            AutoSize = true,
            Margin = new Padding(3, 7, 8, 0),
        });
        premium.Controls.Add(_premiumVariable);
        premium.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = "Premium stores a Unix timestamp in this INTEGER User Variable. In an Event, compare this variable > System Time to test Premium access.",
            Margin = new Padding(8, 7, 3, 0),
        });

        var offerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        offerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        offerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        offerPanel.Controls.Add(_offerList, 0, 0);

        var offerButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };
        var add = new Button { Text = "Add offer", AutoSize = true };
        var edit = new Button { Text = "Edit selected", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        add.Click += (_, _) => AddOffer();
        edit.Click += (_, _) => EditOffer();
        remove.Click += (_, _) => RemoveOffer();
        offerButtons.Controls.Add(add);
        offerButtons.Controls.Add(edit);
        offerButtons.Controls.Add(remove);
        offerPanel.Controls.Add(offerButtons, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var cancel = new Button { Text = "Cancel", Width = 110 };
        var save = new Button { Text = "Save", Width = 110 };
        cancel.Click += (_, _) => Close();
        save.Click += (_, _) => SaveConfiguration();
        footer.Controls.Add(cancel);
        footer.Controls.Add(save);

        root.Controls.Add(settings, 0, 0);
        root.Controls.Add(premium, 0, 1);
        root.Controls.Add(offerPanel, 0, 2);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);

        RefreshOffers();
    }

    private void FillPremiumVariables()
    {
        _premiumVariable.Items.Clear();
        _premiumVariable.Items.Add(new VariableChoice(Guid.Empty, "(Premium disabled / no variable)"));

        foreach (var variable in UserVariableDescriptor.Lookup.Values
                     .OfType<UserVariableDescriptor>()
                     .Where(variable => variable.DataType == VariableDataType.Integer)
                     .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase))
        {
            _premiumVariable.Items.Add(new VariableChoice(variable.Id, variable.Name));
        }

        var target = Math.Max(
            0,
            Enumerable.Range(0, _premiumVariable.Items.Count)
                .FirstOrDefault(index =>
                    _premiumVariable.Items[index] is VariableChoice choice &&
                    choice.Id == _working.PremiumUntilUserVariableId
                )
        );
        _premiumVariable.SelectedIndex = target;
    }

    private void AddOffer()
    {
        using var dialog = new LogiCoinOfferDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
        {
            return;
        }

        _offers.Add(dialog.Result);
        RefreshOffers();
    }

    private void EditOffer()
    {
        if (_offerList.SelectedItem is not OfferChoice choice)
        {
            return;
        }

        using var dialog = new LogiCoinOfferDialog(choice.Offer);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
        {
            return;
        }

        var index = _offers.FindIndex(offer => offer.Id == choice.Offer.Id);
        if (index >= 0)
        {
            _offers[index] = dialog.Result;
        }

        RefreshOffers();
    }

    private void RemoveOffer()
    {
        if (_offerList.SelectedItem is not OfferChoice choice)
        {
            return;
        }

        _offers.RemoveAll(offer => offer.Id == choice.Offer.Id);
        RefreshOffers();
    }

    private void RefreshOffers()
    {
        _offerList.Items.Clear();
        var now = DateTimeOffset.UtcNow;

        foreach (var offer in _offers
                     .OrderBy(offer => offer.SortOrder)
                     .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase))
        {
            var price = offer.EffectivePrice(now);
            var priceText = price == offer.PriceLogiCoins
                ? $"{price:N0} LC"
                : $"{offer.PriceLogiCoins:N0} -> {price:N0} LC (-{offer.DiscountPercent}%)";
            var availability = offer.Enabled ? "Enabled" : "Disabled";
            var type = offer.Type switch
            {
                LogiCoinOfferType.Item => "Item",
                LogiCoinOfferType.Bundle => $"Bundle ({offer.Bundle.Length})",
                LogiCoinOfferType.Premium => $"Premium {offer.PremiumDays} days",
                _ => offer.Type.ToString(),
            };

            _offerList.Items.Add(
                new OfferChoice(
                    offer,
                    $"[{availability}] {offer.Name} | {type} | {priceText}"
                )
            );
        }
    }

    private void SaveConfiguration()
    {
        if (!Uri.TryCreate(_shopUrl.Text.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
        {
            MessageBox.Show(this, "Enter a valid Shop URL.", "LogiCoin Shop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _working.Enabled = _enabled.Checked;
        _working.ShopUrl = _shopUrl.Text.Trim();
        _working.PremiumUntilUserVariableId =
            _premiumVariable.SelectedItem is VariableChoice variable ? variable.Id : Guid.Empty;
        _working.Offers = _offers.ToArray();

        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(this, "The LogiCoin Shop configuration is invalid.", "LogiCoin Shop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (_working.Offers.Any(offer => offer.Type == LogiCoinOfferType.Premium) &&
            _working.PremiumUntilUserVariableId == Guid.Empty)
        {
            MessageBox.Show(this, "Choose an INTEGER User Variable before adding Premium offers.", "LogiCoin Shop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        PacketSender.SendSaveLogiCoinShopConfiguration(_working.ToJson());
        LogiCoinShopConfiguration.Load(_working.ToJson());
        Close();
    }

    private static LogiCoinShopOffer CloneOffer(LogiCoinShopOffer source) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Type = source.Type,
            PriceLogiCoins = source.PriceLogiCoins,
            ItemId = source.ItemId,
            ItemQuantity = source.ItemQuantity,
            Bundle = (source.Bundle ?? []).Select(entry => new LogiCoinBundleItem(entry.ItemId, entry.Quantity)).ToArray(),
            PremiumDays = source.PremiumDays,
            Enabled = source.Enabled,
            SortOrder = source.SortOrder,
            DiscountPercent = source.DiscountPercent,
            PromotionStartsAtUtc = source.PromotionStartsAtUtc,
            PromotionEndsAtUtc = source.PromotionEndsAtUtc,
        };

    private sealed class LogiCoinOfferDialog : DarkForm
    {
        private sealed record BundleChoice(LogiCoinBundleItem Entry, string Text)
        {
            public override string ToString() => Text;
        }

        private readonly TextBox _name = new() { Width = 300 };
        private readonly TextBox _description = new() { Width = 480, Multiline = true, Height = 70 };
        private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly NumericUpDown _price = new() { Minimum = 0, Maximum = 1_000_000_000, Width = 140, ThousandsSeparator = true };
        private readonly NumericUpDown _sortOrder = new() { Minimum = -1_000_000, Maximum = 1_000_000, Width = 100 };
        private readonly CheckBox _enabled = new() { Text = "Enabled", AutoSize = true };
        private readonly NumericUpDown _discount = new() { Minimum = 0, Maximum = 100, Width = 80 };
        private readonly DateTimePicker _promoStart = new() { Width = 170, ShowCheckBox = true, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm" };
        private readonly DateTimePicker _promoEnd = new() { Width = 170, ShowCheckBox = true, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm" };

        private readonly Panel _specificPanel = new() { Dock = DockStyle.Fill };
        private readonly ComboBox _item = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        private readonly NumericUpDown _itemQuantity = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 120, ThousandsSeparator = true };
        private readonly ListBox _bundleList = new() { Width = 520, Height = 150 };
        private readonly ComboBox _bundleItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        private readonly NumericUpDown _bundleQuantity = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 120, ThousandsSeparator = true };
        private readonly NumericUpDown _premiumDays = new() { Minimum = 1, Maximum = 3650, Value = 30, Width = 100 };

        private readonly List<LogiCoinBundleItem> _bundle = [];
        private readonly Guid _id;

        public LogiCoinShopOffer? Result { get; private set; }

        public LogiCoinOfferDialog(LogiCoinShopOffer? source = null)
        {
            Text = source == null ? "Add LogiCoin Offer" : "Edit LogiCoin Offer";
            StartPosition = FormStartPosition.CenterParent;
            Width = 720;
            Height = 630;
            MinimizeBox = false;
            MaximizeBox = false;

            _id = source?.Id ?? Guid.NewGuid();
            _type.Items.AddRange(Enum.GetValues<LogiCoinOfferType>().Cast<object>().ToArray());
            FillItems(_item);
            FillItems(_bundleItem);

            if (source != null)
            {
                _name.Text = source.Name;
                _description.Text = source.Description;
                _type.SelectedItem = source.Type;
                _price.Value = source.PriceLogiCoins;
                _sortOrder.Value = source.SortOrder;
                _enabled.Checked = source.Enabled;
                _discount.Value = source.DiscountPercent;
                _itemQuantity.Value = Math.Clamp(source.ItemQuantity, 1, 1_000_000_000);
                _premiumDays.Value = Math.Clamp(source.PremiumDays, 1, 3650);
                SelectItem(_item, source.ItemId);

                foreach (var entry in source.Bundle ?? [])
                {
                    _bundle.Add(new LogiCoinBundleItem(entry.ItemId, entry.Quantity));
                }

                if (source.PromotionStartsAtUtc is { } starts)
                {
                    _promoStart.Checked = true;
                    _promoStart.Value = starts.LocalDateTime;
                }
                else
                {
                    _promoStart.Checked = false;
                }

                if (source.PromotionEndsAtUtc is { } ends)
                {
                    _promoEnd.Checked = true;
                    _promoEnd.Value = ends.LocalDateTime;
                }
                else
                {
                    _promoEnd.Checked = false;
                }
            }
            else
            {
                _type.SelectedItem = LogiCoinOfferType.Item;
                _enabled.Checked = true;
                _promoStart.Checked = false;
                _promoEnd.Checked = false;
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(10),
                AutoScroll = true,
            };

            root.Controls.Add(Row("Name", _name), 0, 0);
            root.Controls.Add(Row("Description", _description), 0, 1);
            root.Controls.Add(Row("Type", _type, "Price (LogiCoins)", _price, "Sort", _sortOrder, _enabled), 0, 2);
            root.Controls.Add(Row("Discount %", _discount, "Starts (local)", _promoStart, "Ends (local)", _promoEnd), 0, 3);
            root.Controls.Add(new Label
            {
                Text = "Promotions are active only between the optional start/end dates. A 0% discount disables the promotion.",
                AutoSize = true,
            }, 0, 4);

            _specificPanel.Height = 230;
            root.Controls.Add(_specificPanel, 0, 5);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
            };
            var cancel = new Button { Text = "Cancel", Width = 100 };
            var ok = new Button { Text = "OK", Width = 100 };
            cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
            ok.Click += (_, _) => SaveOffer();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 6);

            Controls.Add(root);
            _type.SelectedIndexChanged += (_, _) => RebuildSpecificEditor();
            RebuildSpecificEditor();
            RefreshBundle();
        }

        private void RebuildSpecificEditor()
        {
            _specificPanel.Controls.Clear();

            if (_type.SelectedItem is not LogiCoinOfferType type)
            {
                return;
            }

            if (type == LogiCoinOfferType.Item)
            {
                var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
                panel.Controls.Add(new Label { Text = "Item", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
                panel.Controls.Add(_item);
                panel.Controls.Add(new Label { Text = "Quantity", AutoSize = true, Margin = new Padding(12, 8, 3, 3) });
                panel.Controls.Add(_itemQuantity);
                _specificPanel.Controls.Add(panel);
                return;
            }

            if (type == LogiCoinOfferType.Premium)
            {
                var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                panel.Controls.Add(new Label { Text = "Premium days", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
                panel.Controls.Add(_premiumDays);
                panel.Controls.Add(new Label
                {
                    Text = "Premium time stacks on top of remaining Premium time.",
                    AutoSize = true,
                    Margin = new Padding(12, 8, 3, 3),
                });
                _specificPanel.Controls.Add(panel);
                return;
            }

            var bundleRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            bundleRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bundleRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _bundleList.Dock = DockStyle.Fill;
            bundleRoot.Controls.Add(_bundleList, 0, 0);

            var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            controls.Controls.Add(_bundleItem);
            controls.Controls.Add(_bundleQuantity);
            var add = new Button { Text = "Add item", AutoSize = true };
            var remove = new Button { Text = "Remove selected", AutoSize = true };
            add.Click += (_, _) =>
            {
                if (_bundleItem.SelectedItem is not ItemChoice item || item.Id == Guid.Empty)
                {
                    return;
                }

                _bundle.Add(new LogiCoinBundleItem(item.Id, (int)_bundleQuantity.Value));
                RefreshBundle();
            };
            remove.Click += (_, _) =>
            {
                if (_bundleList.SelectedItem is BundleChoice choice)
                {
                    var index = _bundle.FindIndex(entry => entry == choice.Entry);
                    if (index >= 0)
                    {
                        _bundle.RemoveAt(index);
                    }

                    RefreshBundle();
                }
            };
            controls.Controls.Add(add);
            controls.Controls.Add(remove);
            bundleRoot.Controls.Add(controls, 0, 1);
            _specificPanel.Controls.Add(bundleRoot);
        }

        private void RefreshBundle()
        {
            _bundleList.Items.Clear();
            foreach (var entry in _bundle)
            {
                _bundleList.Items.Add(
                    new BundleChoice(
                        entry,
                        $"{entry.Quantity:N0} x {ItemDescriptor.GetName(entry.ItemId)}"
                    )
                );
            }
        }

        private void SaveOffer()
        {
            if (_type.SelectedItem is not LogiCoinOfferType type)
            {
                return;
            }

            var itemId = _item.SelectedItem is ItemChoice item ? item.Id : Guid.Empty;
            var result = new LogiCoinShopOffer
            {
                Id = _id,
                Name = _name.Text.Trim(),
                Description = _description.Text.Trim(),
                Type = type,
                PriceLogiCoins = (int)_price.Value,
                ItemId = itemId,
                ItemQuantity = (int)_itemQuantity.Value,
                Bundle = _bundle.ToArray(),
                PremiumDays = (int)_premiumDays.Value,
                Enabled = _enabled.Checked,
                SortOrder = (int)_sortOrder.Value,
                DiscountPercent = (int)_discount.Value,
                PromotionStartsAtUtc = _promoStart.Checked
                    ? new DateTimeOffset(_promoStart.Value.ToUniversalTime(), TimeSpan.Zero)
                    : null,
                PromotionEndsAtUtc = _promoEnd.Checked
                    ? new DateTimeOffset(_promoEnd.Value.ToUniversalTime(), TimeSpan.Zero)
                    : null,
            };

            if (!result.IsStructurallyValid)
            {
                MessageBox.Show(this, "This offer is incomplete or invalid.", "LogiCoin Shop", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Result = result;
            DialogResult = DialogResult.OK;
        }

        private static FlowLayoutPanel Row(params object[] parts)
        {
            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
            };

            foreach (var part in parts)
            {
                if (part is string label)
                {
                    row.Controls.Add(new Label
                    {
                        Text = label,
                        AutoSize = true,
                        Margin = new Padding(8, 8, 3, 3),
                    });
                }
                else if (part is Control control)
                {
                    row.Controls.Add(control);
                }
            }

            return row;
        }

        private static void FillItems(ComboBox picker)
        {
            picker.Items.Clear();
            picker.Items.Add(new ItemChoice(Guid.Empty, "Choose item..."));

            foreach (var item in ItemDescriptor.Lookup.Values
                         .OfType<ItemDescriptor>()
                         .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                picker.Items.Add(
                    new ItemChoice(
                        item.Id,
                        string.IsNullOrWhiteSpace(item.Folder)
                            ? item.Name
                            : $"[{item.Folder}] / {item.Name}"
                    )
                );
            }

            picker.SelectedIndex = 0;
        }

        private static void SelectItem(ComboBox picker, Guid itemId)
        {
            for (var index = 0; index < picker.Items.Count; ++index)
            {
                if (picker.Items[index] is ItemChoice choice && choice.Id == itemId)
                {
                    picker.SelectedIndex = index;
                    return;
                }
            }
        }
    }
}
