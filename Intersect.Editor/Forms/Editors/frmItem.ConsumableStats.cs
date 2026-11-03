using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmItem
{
    private static string ConsumableText(string english, string french, string spanish) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "fr" => french,
            "es" => spanish,
            _ => english,
        };

    private void InitializeConsumableStatEditor()
    {
        var editButton = new Button
        {
            Name = "btnConsumableStatEffects",
            Text = ConsumableText("Timed stat effects...", "Effets de stats temporaires...", "Efectos temporales de estadísticas..."),
            Location = new Point(11, 130),
            Size = new Size(231, 27),
            BackColor = Color.FromArgb(63, 63, 67),
            ForeColor = Color.Gainsboro,
            FlatStyle = FlatStyle.Flat,
        };
        editButton.FlatAppearance.BorderColor = Color.FromArgb(96, 96, 99);
        editButton.Click += (_, _) => EditConsumableStatEffects();
        grpConsumable.Controls.Add(editButton);
        grpConsumable.Height = Math.Max(grpConsumable.Height, 171);
    }

    private void EditConsumableStatEffects()
    {
        if (mEditorItem == null || mEditorItem.ItemType != ItemType.Consumable)
        {
            return;
        }

        using var dialog = new Form
        {
            Text = ConsumableText("Timed consumable modifiers", "Modificateurs temporaires", "Modificadores temporales"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(486, 344),
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.Gainsboro,
        };
        var header = new TableLayoutPanel
        {
            Location = new Point(14, 15),
            Size = new Size(458, 238),
            ColumnCount = 3,
            RowCount = 6,
            BackColor = dialog.BackColor,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28.5f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28.5f));
        for (var row = 0; row < 6; row++) header.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));

        static Label Heading(string label) => new()
        {
            Text = label, AutoSize = true, ForeColor = Color.Gainsboro,
            Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft,
        };

        header.Controls.Add(Heading(ConsumableText("Stat", "Statistique", "Estadística")), 0, 0);
        header.Controls.Add(Heading(ConsumableText("Flat (+/-)", "Fixe (+/-)", "Fijo (+/-)")), 1, 0);
        header.Controls.Add(Heading("% (+/-)"), 2, 0);

        var statNames = new[]
        {
            ConsumableText("Attack", "Attaque", "Ataque"),
            ConsumableText("Ability power", "Puissance", "Poder"),
            ConsumableText("Defense", "Défense", "Defensa"),
            ConsumableText("Magic resistance", "Résistance magique", "Resistencia mágica"),
            ConsumableText("Speed", "Vitesse", "Velocidad"),
        };
        var flatInputs = new NumericUpDown[statNames.Length];
        var percentageInputs = new NumericUpDown[statNames.Length];
        for (var i = 0; i < statNames.Length; i++)
        {
            header.Controls.Add(Heading(statNames[i]), 0, i + 1);
            flatInputs[i] = new NumericUpDown
            {
                Minimum = -1000, Maximum = 1000,
                Value = Math.Clamp(mEditorItem.StatsGiven[i], -1000, 1000),
                Width = 105, BackColor = Color.FromArgb(65, 65, 69),
                ForeColor = Color.White,
            };
            percentageInputs[i] = new NumericUpDown
            {
                Minimum = -80, Maximum = 200,
                Value = Math.Clamp(mEditorItem.PercentageStatsGiven[i], -80, 200),
                Width = 105, BackColor = Color.FromArgb(65, 65, 69),
                ForeColor = Color.White,
            };
            header.Controls.Add(flatInputs[i], 1, i + 1);
            header.Controls.Add(percentageInputs[i], 2, i + 1);
        }

        var note = new Label
        {
            Location = new Point(17, 260),
            Size = new Size(452, 39),
            Text = ConsumableText(
                "Duration follows item cooldown (ms); 0 = 30s default, or Grog duration.",
                "Durée = cooldown de l'objet (ms); 0 = 30 s, ou durée du Grog.",
                "Duración = espera del objeto (ms); 0 = 30 s, o duración del Grog."),
            ForeColor = Color.Silver,
        };
        var save = new Button
        {
            Text = ConsumableText("Apply", "Appliquer", "Aplicar"),
            DialogResult = DialogResult.OK, Location = new Point(292, 308),
            Size = new Size(86, 29),
        };
        var cancel = new Button
        {
            Text = ConsumableText("Cancel", "Annuler", "Cancelar"),
            DialogResult = DialogResult.Cancel, Location = new Point(387, 308),
            Size = new Size(86, 29),
        };
        dialog.Controls.Add(header);
        dialog.Controls.Add(note);
        dialog.Controls.Add(save);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        for (var i = 0; i < statNames.Length; i++)
        {
            mEditorItem.StatsGiven[i] = (int)flatInputs[i].Value;
            mEditorItem.PercentageStatsGiven[i] = (int)percentageInputs[i].Value;
        }
    }
}
