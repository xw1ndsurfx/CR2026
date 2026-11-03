using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

internal sealed class FrmShopPromotion : Form
{
    private readonly CheckBox _enabled = new() { AutoSize = true, Checked = true };
    private readonly NumericUpDown _discount = new() { Minimum = 1, Maximum = 99, Value = 20, Width = 120 };
    private readonly DateTimePicker _start = DateInput();
    private readonly DateTimePicker _end = DateInput();
    private readonly Label _preview = new() { AutoSize = true, Padding = new Padding(0, 10, 0, 0) };
    private readonly int _price;
    internal ShopPromotion? Result { get; private set; }
    internal static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private static DateTimePicker DateInput() => new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss" };

    internal FrmShopPromotion(string name, int price, ShopPromotion? current)
    {
        _price = price; Text = T("Special offer") + " · " + name;
        Icon = Intersect.Editor.Core.Program.Icon;
        ClientSize = new Size(650, 370); MinimumSize = new Size(620, 370); MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        static DateTime DisplayDate(DateTime value) => value < DateTimePicker.MinimumDateTime ? DateTimePicker.MinimumDateTime :
            value > DateTimePicker.MaximumDateTime ? DateTimePicker.MaximumDateTime : value;
        _start.Value = current == null ? DateTime.Now : DisplayDate(current.StartsAtUtc.LocalDateTime);
        _end.Value = current == null ? DateTime.Now.AddDays(7) : DisplayDate(current.EndsAtUtc.LocalDateTime);
        if (current != null) { _enabled.Checked = current.Enabled; _discount.Value = Math.Clamp(current.DiscountPercent, 1, 99); }
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 6 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 4; index++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _enabled.Text = T("Enable promotion"); fields.Controls.Add(_enabled, 0, 0); fields.SetColumnSpan(_enabled, 2);
        void Field(string title, Control input, int row)
        { fields.Controls.Add(new Label { Text = T(title), AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, row); fields.Controls.Add(input, 1, row); }
        Field("Reduction (%)", _discount, 1); Field("Starts at", _start, 2); Field("Ends at", _end, 3);
        var timezone = new Label { AutoSize = true, MaximumSize = new Size(590, 0),
            Text = string.Format(T("Dates use your computer's local time ({0})."), TimeZoneInfo.Local.DisplayName) };
        fields.Controls.Add(timezone, 0, 4); fields.SetColumnSpan(timezone, 2);
        fields.Controls.Add(_preview, 0, 5); fields.SetColumnSpan(_preview, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8) };
        var cancel = new LogiklikButton { Text = T("Cancel"), DialogResult = DialogResult.Cancel, Width = 120, Height = 32 };
        var save = new LogiklikButton { Text = T("Save"), Width = 120, Height = 32 };
        var remove = new LogiklikButton { Text = T("Remove promotion"), Width = 180, Height = 32, Enabled = current != null };
        footer.Controls.AddRange([cancel, save, remove]); Controls.Add(fields); Controls.Add(footer);
        AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            if (!TryCreate(out var result)) { MessageBox.Show(this, T("The end date must be after the start date. Check daylight-saving time too.")); return; }
            Result = result; DialogResult = DialogResult.OK;
        };
        remove.Click += (_, _) => { Result = null; DialogResult = DialogResult.OK; };
        _discount.ValueChanged += (_, _) => UpdatePreview(); _enabled.CheckedChanged += (_, _) => UpdatePreview();
        _start.ValueChanged += (_, _) => UpdatePreview(); _end.ValueChanged += (_, _) => UpdatePreview();
        EditorTheme.Apply(this); UpdatePreview();
    }

    private bool TryCreate(out ShopPromotion result)
    {
        var start = DateTime.SpecifyKind(_start.Value, DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(_end.Value, DateTimeKind.Unspecified);
        result = new ShopPromotion();
        if (TimeZoneInfo.Local.IsInvalidTime(start) || TimeZoneInfo.Local.IsInvalidTime(end)) return false;
        result = new ShopPromotion { Enabled = _enabled.Checked, DiscountPercent = (int)_discount.Value,
            StartsAtUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start)), EndsAtUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(end)) };
        return result.IsValid;
    }

    private void UpdatePreview()
    {
        var valid = TryCreate(out var draft);
        var status = !valid ? T("Invalid dates") : !draft.Enabled ? T("Disabled") :
            draft.IsActive(DateTimeOffset.UtcNow) ? T("Active") : DateTimeOffset.UtcNow < draft.StartsAtUtc ? T("Scheduled") : T("Expired");
        _preview.Text = string.Format(T("Promotion price: {0} → {1}"), _price,
            ShopPromotion.Price(_price, (int)_discount.Value)) + "\n" + status;
    }
}
