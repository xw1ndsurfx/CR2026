using System;
using System.Drawing;
using System.Windows.Forms;
using DarkUI.Controls;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmNpc
{
    // This generator only adjusts the base combat numbers. It intentionally leaves
    // spell lists, drops, events, pets and invasion-specific behavior unchanged.
    private void BuildNpcAutoCreator()
    {
        var button = new DarkButton
        {
            Text = "Auto Create NPC",
            Size = new Size(186, 31),
            Location = new Point(btnSave.Left - 198, btnSave.Top),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        button.Click += (_, _) => ShowNpcAutoCreator();
        Controls.Add(button);
        button.BringToFront();
    }

    private void ShowNpcAutoCreator()
    {
        if (mEditorItem == null)
        {
            MessageBox.Show(this, "Select an NPC first.", "NPC Auto Creator");
            return;
        }

        using var dialog = new Form
        {
            Text = "NPC Auto Creator",
            Size = new Size(440, 450),
            MinimumSize = new Size(440, 450),
            MaximumSize = new Size(440, 450),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.Gainsboro
        };

        static Label MakeLabel(string text, int y) => new()
        {
            Text = text, Location = new Point(20, y),
            Size = new Size(150, 25), ForeColor = Color.Gainsboro
        };

        var name = new TextBox { Location = new Point(176, 20), Width = 220, Text = txtName.Text };
        var level = new NumericUpDown
        {
            Location = new Point(176, 58), Width = 220,
            Minimum = 1, Maximum = nudLevel.Maximum, Value = nudLevel.Value
        };
        var role = new ComboBox
        {
            Location = new Point(176, 96), Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        role.Items.AddRange(new object[] { "Warrior / Melee", "Archer / Ranged", "Mage / Magic", "Tank", "Assassin" });
        role.SelectedIndex = 0;
        var difficulty = new ComboBox
        {
            Location = new Point(176, 134), Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        difficulty.Items.AddRange(new object[] { "Weak", "Normal", "Elite", "Boss" });
        difficulty.SelectedIndex = 1;
        var preview = new Label
        {
            Location = new Point(20, 184), Size = new Size(376, 132),
            ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10)
        };
        var warning = new Label
        {
            Text = "Experimental presets: review combat results before using them in production. Existing drops, spells and events are preserved.",
            Location = new Point(20, 328), Size = new Size(376, 42),
            ForeColor = Color.Khaki
        };
        var apply = new Button
        {
            Text = "Apply to selected NPC", Location = new Point(196, 375),
            Size = new Size(200, 30), DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Cancel", Location = new Point(20, 375),
            Size = new Size(110, 30), DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange(new Control[]
        {
            MakeLabel("NPC Name", 22), name,
            MakeLabel("Level", 60), level,
            MakeLabel("Combat Class", 98), role,
            MakeLabel("Difficulty", 136), difficulty,
            preview, warning, apply, cancel
        });
        dialog.AcceptButton = apply;
        dialog.CancelButton = cancel;

        (int hp, int mana, int attack, int magic, int defense, int resist, int damage, int exp) Calculate()
        {
            var lvl = (double)level.Value;
            var multiplier = difficulty.SelectedIndex switch
            {
                0 => 0.65,
                2 => 1.8,
                3 => 4.0,
                _ => 1.0
            };
            var hpWeight = role.SelectedIndex switch
            {
                3 => 1.8, 4 => 0.7, 1 or 2 => 0.8, _ => 1.0
            };
            var damageWeight = role.SelectedIndex switch
            {
                3 => 0.7, 4 => 1.35, _ => 1.0
            };
            int Scale(double value) => (int)Math.Clamp(Math.Round(value), 0, 100000000);
            return (
                Scale((30 + 12 * lvl + lvl * lvl * 1.5) * multiplier * hpWeight),
                role.SelectedIndex == 2 ? Scale((20 + 7 * lvl) * multiplier) : 0,
                role.SelectedIndex == 2 ? 0 : Scale((3 + 2.2 * lvl) * multiplier),
                role.SelectedIndex == 2 ? Scale((3 + 2.2 * lvl) * multiplier) : 0,
                Scale((2 + lvl * 1.1) * multiplier * (role.SelectedIndex == 3 ? 1.5 : 1.0)),
                Scale((2 + lvl * 1.1) * multiplier * (role.SelectedIndex == 2 ? 1.4 : 1.0)),
                Scale((3 + 1.8 * lvl) * multiplier * damageWeight),
                Scale((5 + 3 * lvl + lvl * lvl * 0.5) * multiplier)
            );
        }

        void RefreshPreview()
        {
            var s = Calculate();
            preview.Text =
                $"HP: {s.hp:N0}    Mana: {s.mana:N0}\n" +
                $"Attack: {s.attack:N0}    Magic: {s.magic:N0}\n" +
                $"Defense: {s.defense:N0}    Magic Resist: {s.resist:N0}\n" +
                $"Base Damage: {s.damage:N0}    EXP: {s.exp:N0}\n" +
                "Preview only. No changes until Apply.";
        }
        level.ValueChanged += (_, _) => RefreshPreview();
        role.SelectedIndexChanged += (_, _) => RefreshPreview();
        difficulty.SelectedIndexChanged += (_, _) => RefreshPreview();
        RefreshPreview();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var stats = Calculate();
        static void Assign(NumericUpDown field, int value)
        {
            field.Value = Math.Clamp((decimal)value, field.Minimum, field.Maximum);
        }

        txtName.Text = name.Text;
        Assign(nudLevel, (int)level.Value);
        Assign(nudHp, stats.hp);
        Assign(nudMana, stats.mana);
        Assign(nudStr, stats.attack);
        Assign(nudMag, stats.magic);
        Assign(nudDef, stats.defense);
        Assign(nudMR, stats.resist);
        Assign(nudDamage, stats.damage);
        Assign(nudExp, stats.exp);
        _chkIsBoss.Checked = difficulty.SelectedIndex == 3;
        chkAggressive.Checked = true;
        MessageBox.Show(this,
            "Preset applied. Review the values and click Save to persist the NPC. Combat formulas have not yet been calibrated.",
            "NPC Auto Creator");
    }
}
