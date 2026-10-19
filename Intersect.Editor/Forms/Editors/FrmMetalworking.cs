using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Professions;
using Intersect.Network.Packets.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmMetalworking : DarkForm
{
    internal Func<(SaveWorldActivitiesPacket? Packet, string? Error)> PrepareSave { get; }
    private sealed record Choice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }
    public FrmMetalworking(string json, MetalworkKind initial = MetalworkKind.Smelting)
    {
        var draft = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(json) ?? new();
        var recipes = draft.Metalworking.Recipes;
        Text = "Royal Foundry & Forge - Recipes";
        Size = new Size(1120, 760);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 250 };
        var kind = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<MetalworkKind>() };
        kind.BindingContext = BindingContext;
        kind.SelectedItem = initial;
        var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name" };
        sidebar.Controls.Add(list);
        sidebar.Controls.Add(kind);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(12) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        var items = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)).ToList();
        items.Insert(0, new Choice(Guid.Empty, "None"));
        ComboBox ItemCombo(List<Choice> values) => new()
        {
            BindingContext = BindingContext,
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = values.ToList(),
            DisplayMember = "Name",
            ValueMember = "Id"
        };
        var name = new TextBox { Name = "RecipeName", Dock = DockStyle.Fill };
        var enabled = new CheckBox { Name = "RecipeEnabled", Text = "Enabled", Dock = DockStyle.Fill };
        var output = ItemCombo(items); output.Name = "RecipeOutput";
        var quantity = new NumericUpDown { Minimum = 1, Maximum = 100, Dock = DockStyle.Fill };
        var professions = ProfessionConfiguration.Instance.Professions.Select(p => new Choice(p.Id, p.Name)).OrderBy(p => p.Name).ToList();
        professions.Insert(0, new Choice(Guid.Empty, "No profession XP"));
        var profession = ItemCombo(professions);
        var level = new NumericUpDown { Minimum = 1, Maximum = 1000, Dock = DockStyle.Fill };
        var xp = new NumericUpDown { Minimum = 0, Maximum = 5000, Dock = DockStyle.Fill };
        var quality = new NumericUpDown { Minimum = 1, Maximum = 100, Dock = DockStyle.Fill };
        ComboBox Sound()
        {
            var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.Add("");
            if (Directory.Exists("resources/sounds"))
                combo.Items.AddRange(Directory.GetFiles("resources/sounds").Select(Path.GetFileName).OfType<string>().OrderBy(s => s).Cast<object>().ToArray());
            return combo;
        }
        var pump = Sound();
        var strike = Sound();
        var success = Sound();
        void Field(string label, Control control)
        {
            var row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            fields.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            fields.Controls.Add(control, 1, row);
        }
        Field("Recipe name", name);
        Field("Availability", enabled);
        Field("Output item", output);
        Field("Output quantity", quantity);
        Field("Profession (e.g. Blacksmith)", profession);
        Field("Required profession level", level);
        Field("Profession XP", xp);
        Field("Required quality (%)", quality);
        Field("Bellows sound", pump);
        Field("Pour / hammer / quench sound", strike);
        Field("Success sound", success);
        var materials = new DataGridView { Name = "Materials", Dock = DockStyle.Fill, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        materials.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Required item", DataPropertyName = "ItemId", DataSource = items.ToList(), DisplayMember = "Name", ValueMember = "Id" });
        materials.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Quantity", DataPropertyName = "Quantity", FillWeight = 30 });
        materials.DataError += (_, e) => ActivityEditorValidation.ReportGridError(materials, e);
        materials.CellValidated += (_, e) => materials.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
        body.Controls.Add(fields, 0, 0);
        body.Controls.Add(materials, 1, 0);
        var help = new Label { Dock = DockStyle.Fill, Text = "Foundry: heat and three timed pours. Forge: heat, four timed hammer strikes, then quench. Materials are consumed only after a successful result and a saved inventory transaction. Level 1 recipes can teach the selected profession. Use ores as inputs for ingots, then ingots as inputs for weapons.", AutoSize = false };
        body.Controls.Add(help, 0, 1);
        body.SetColumnSpan(help, 2);
        MetalworkRecipe? current = null;
        void Store()
        {
            if (current == null)
                return;
            materials.EndEdit();
            if (materials.DataSource != null)
                BindingContext[materials.DataSource].EndCurrentEdit();
            current.Name = name.Text.Trim();
            current.Enabled = enabled.Checked;
            current.OutputItemId = (output.SelectedItem as Choice)?.Id ?? current.OutputItemId;
            current.OutputQuantity = (int)quantity.Value;
            current.ProfessionId = (profession.SelectedItem as Choice)?.Id ?? current.ProfessionId;
            current.RequiredLevel = (int)level.Value;
            current.ProfessionExperience = (int)xp.Value;
            current.RequiredQuality = (int)quality.Value;
            current.PumpSound = pump.SelectedItem?.ToString() ?? "";
            current.StrikeSound = strike.SelectedItem?.ToString() ?? "";
            current.SuccessSound = success.SelectedItem?.ToString() ?? "";
        }
        void SelectSound(ComboBox combo, string value)
        {
            if (!combo.Items.Contains(value))
                combo.Items.Add(value);
            combo.SelectedItem = value;
        }
        list.SelectedIndexChanged += (_, _) => { Store(); current = list.SelectedItem as MetalworkRecipe; if (current == null) return; name.Text = current.Name; enabled.Checked = current.Enabled; output.SelectedValue = current.OutputItemId; quantity.Value = current.OutputQuantity; profession.SelectedValue = current.ProfessionId; level.Value = current.RequiredLevel; xp.Value = current.ProfessionExperience; quality.Value = current.RequiredQuality; SelectSound(pump, current.PumpSound); SelectSound(strike, current.StrikeSound); SelectSound(success, current.SuccessSound); materials.DataSource = new BindingList<MetalworkIngredient>(current.Ingredients); };
        void Refresh()
        {
            Store();
            current = null;
            list.DataSource = recipes.Where(r => r.Kind == (MetalworkKind)kind.SelectedItem!).ToList();
        }
        kind.SelectedIndexChanged += (_, _) => Refresh();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
        var add = new Button { Text = "Add recipe", Width = 150 };
        add.Click += (_, _) => { Store(); current = null; var recipe = new MetalworkRecipe { Kind = (MetalworkKind)kind.SelectedItem!, ProfessionId = professions.FirstOrDefault(p => p.Name.Contains("smith", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("forgeron", StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty }; recipes.Add(recipe); Refresh(); list.SelectedItem = recipe; };
        var delete = new Button { Text = "Delete recipe", Width = 150 };
        delete.Click += (_, _) => { if (current != null) recipes.Remove(current); current = null; Refresh(); };
        var save = new Button { Name = "SaveRecipes", Text = "Save recipes", Width = 180 };
        PrepareSave = () =>
        {
            if (!ActivityEditorValidation.CommitGrid(materials)) return (null, Intersect.Editor.Localization.EditorSafetyText.Get("Correct the highlighted table cell before saving."));
            Store();
            var error = ActivityEditorValidation.Metalworking(draft.Metalworking);
            return error != null ? (null, error) : (new SaveWorldActivitiesPacket { Section = WorldActivitiesSection.Metalworking, Json = JsonConvert.SerializeObject(draft) }, null);
        };
        var saveSession = new ActivitySaveSession(this);
        save.Click += (_, _) =>
        {
            var result = PrepareSave();
            if (result.Error != null) { MessageBox.Show(this, result.Error); return; }
            saveSession.Send(result.Packet!);
        };
        buttons.Controls.AddRange([add, delete, save]);
        Controls.Add(body);
        Controls.Add(sidebar);
        Controls.Add(buttons);
        Refresh();
        MiniGameEditorTheme.Apply(this);
    }
}
