using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using DarkUI.Controls;
using Intersect.GameObjects;
using Intersect.Editor.Forms;
using Intersect.Enums;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmNpc
{
    // Only base combat stats are generated. Special NPC behavior, pet configuration,
    // scripted encounters, spells, drops, events and immunities are not rewritten.
    private void BuildNpcAutoCreator()
    {
        var button = new DarkButton
        {
            Text = "Auto Create NPC",
            Size = new Size(186, 31),
            Location = new System.Drawing.Point(btnSave.Left - 198, btnSave.Top),
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

        if (mEditorItem.IsPet)
        {
            MessageBox.Show(this,
                "Pet stats are managed separately. The NPC generator cannot modify a pet.",
                "NPC Auto Creator", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = "NPC Auto Creator - Combat Simulation",
            Size = new Size(510, 715),
            MinimumSize = new Size(440, 390),
            AutoScroll = true,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = true,
            MinimizeBox = false,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            ForeColor = System.Drawing.Color.Gainsboro
        };

        static Label MakeLabel(string text, int y) => new()
        {
            Text = text, Location = new System.Drawing.Point(20, y),
            Size = new Size(170, 25), ForeColor = System.Drawing.Color.Gainsboro
        };

        var name = new TextBox
        {
            Location = new System.Drawing.Point(202, 20), Width = 270, Text = txtName.Text
        };
        var level = new NumericUpDown
        {
            Location = new System.Drawing.Point(202, 58), Width = 270,
            Minimum = 1, Maximum = Math.Min(nudLevel.Maximum, Math.Max(1, Options.Instance.Player.MaxLevel)),
            Value = Math.Clamp(nudLevel.Value, 1, Math.Min(nudLevel.Maximum, Math.Max(1, Options.Instance.Player.MaxLevel)))
        };
        var role = new ComboBox
        {
            Location = new System.Drawing.Point(202, 96), Width = 270,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        role.Items.AddRange(new object[]
        {
            "Warrior / Melee", "Archer / Ranged", "Mage / Magic", "Tank", "Assassin"
        });
        role.SelectedIndex = 0;
        var difficulty = new ComboBox
        {
            Location = new System.Drawing.Point(202, 134), Width = 270,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        difficulty.Items.AddRange(new object[] { "Weak", "Normal", "Elite", "Boss" });
        difficulty.SelectedIndex = mEditorItem.IsBoss ? 3 : 1;
        var party = new NumericUpDown
        {
            Location = new System.Drawing.Point(202, 172), Width = 270,
            Minimum = 1, Maximum = 5, Value = 1
        };
        var spellPicker = new CheckedListBox
        {
            Location = new System.Drawing.Point(202, 210),
            Size = new Size(270, 108), CheckOnClick = true,
            BackColor = System.Drawing.Color.FromArgb(53, 53, 56),
            ForeColor = System.Drawing.Color.Gainsboro
        };
        var spellIds = new List<Guid>();
        List<Guid> SelectedSpells() => spellPicker.CheckedIndices.Cast<int>()
            .Where(index => index >= 0 && index < spellIds.Count)
            .Select(index => spellIds[index]).Distinct().ToList();

        void RefreshSpells()
        {
            spellPicker.Items.Clear();
            spellIds.Clear();
            var candidates = SpellDescriptor.Lookup.Values
                .OfType<SpellDescriptor>()
                .Where(spell => spell.Combat != null &&
                    spell.SpellType == SpellType.CombatSpell &&
                    !spell.Combat.Friendly &&
                    spell.Combat.VitalDiff.Length > (int)Vital.Health &&
                    spell.Combat.VitalDiff[(int)Vital.Health] > 0)
                .Where(spell => role.SelectedIndex == 1
                    ? spell.Combat.Projectile != null
                    : spell.Combat.CastRange >= 2 || spell.Combat.Projectile != null)
                .OrderBy(spell => spell.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var spell in candidates)
            {
                spellIds.Add(spell.Id);
                spellPicker.Items.Add(spell.Name);
            }

            // Mage offers three distinct offensive spells; archer offers two.
            // Players can check/uncheck any entry before generating the NPC.
            var count = role.SelectedIndex switch { 1 => 2, 2 => 3, _ => 0 };
            var preferred = Enumerable.Range(0, candidates.Length)
                .OrderByDescending(index =>
                {
                    var text = candidates[index].Name.ToLowerInvariant();
                    return role.SelectedIndex == 1
                        ? (text.Contains("arrow") || text.Contains("bow") ||
                           text.Contains("shot") ? 1 : 0)
                        : (text.Contains("magic") || text.Contains("arcane") ||
                           text.Contains("lightning") || text.Contains("fire") ||
                           text.Contains("frost") || text.Contains("ice") ||
                           text.Contains("bolt") ? 1 : 0);
                }).Take(count).ToArray();
            foreach (var index in preferred) spellPicker.SetItemChecked(index, true);
            spellPicker.Enabled = role.SelectedIndex is 1 or 2;
        }
        RefreshSpells();

        var preview = new Label
        {
            Location = new System.Drawing.Point(20, 332), Size = new Size(452, 170),
            ForeColor = System.Drawing.Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10)
        };
        var warning = new Label
        {
            Text = "Calibrated with the Balance Lab's class simulation and median-per-slot equipment. " +
                   "NPC spell DPS is estimated using frequency, mana cost and cooldowns. " +
                   "Drops, events, pets and scripted invasion logic are preserved.",
            Location = new System.Drawing.Point(20, 508), Size = new Size(452, 74),
            ForeColor = System.Drawing.Color.Khaki
        };
        var simulate = new Button
        {
            Text = "Simulate & Calibrate", Location = new System.Drawing.Point(20, 602),
            Size = new Size(193, 32)
        };
        var apply = new Button
        {
            Text = "Apply to selected NPC", Location = new System.Drawing.Point(267, 602),
            Size = new Size(205, 32), DialogResult = DialogResult.None
        };
        var cancel = new Button
        {
            Text = "Cancel", Location = new System.Drawing.Point(20, 640),
            Size = new Size(115, 28), DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange(new Control[]
        {
            MakeLabel("NPC Name", 22), name,
            MakeLabel("Level", 60), level,
            MakeLabel("Combat Class", 98), role,
            MakeLabel("Difficulty", 136), difficulty,
            MakeLabel("Party size (1 = solo)", 174), party,
            MakeLabel("Spells (check several)", 212), spellPicker,
            preview, warning, simulate, apply, cancel
        });
        dialog.CancelButton = cancel;
        dialog.AcceptButton = apply;

        // Initial numbers supply a stable starting point. The actual HP and
        // damage calibration is delegated to the Balance Lab's combat simulator.
        (int hp, int mana, int attack, int magic, int defense, int resist,
            int speed, int damage, int exp) CalculatePreset()
        {
            var lvl = (double)level.Value;
            var multiplier = difficulty.SelectedIndex switch
            {
                0 => 0.65d,
                2 => 1.8d,
                3 => 4.0d,
                _ => 1.0d
            };
            var hpWeight = role.SelectedIndex switch
            {
                3 => 1.8d, 4 => 0.7d, 1 or 2 => 0.8d, _ => 1.0d
            };
            var damageWeight = role.SelectedIndex switch
            {
                3 => 0.7d, 4 => 1.35d, _ => 1.0d
            };
            // Ranged NPCs need sufficient mana for their first few casts.
            // This intentionally does not mutate the SpellDescriptor itself.
            var maxManaCost = SelectedSpells()
                .Select(SpellDescriptor.Get)
                .Where(spell => spell?.VitalCost != null &&
                    spell.VitalCost.Length > (int)Vital.Mana)
                .Select(spell => Math.Max(0L, spell!.VitalCost[(int)Vital.Mana]))
                .DefaultIfEmpty(0L).Max();
            var spellMana = Math.Min((double)nudMana.Maximum, maxManaCost * 4d);
            int Cap(NumericUpDown field, double value) =>
                (int)Math.Clamp(Math.Round(value), (double)field.Minimum,
                    Math.Min((double)field.Maximum, 100_000_000d));
            return (
                Cap(nudHp, (30d + 12d * lvl + lvl * lvl * 1.5d) * multiplier * hpWeight),
                Cap(nudMana, Math.Max(role.SelectedIndex == 2
                    ? (20d + 7d * lvl) * multiplier : 0d, spellMana)),
                role.SelectedIndex == 2 ? 0 : Cap(nudStr, (3d + 2.2d * lvl) * multiplier),
                role.SelectedIndex == 2 ? Cap(nudMag, (3d + 2.2d * lvl) * multiplier) : 0,
                Cap(nudDef, (2d + lvl * 1.1d) * multiplier * (role.SelectedIndex == 3 ? 1.5d : 1d)),
                Cap(nudMR, (2d + lvl * 1.1d) * multiplier * (role.SelectedIndex == 2 ? 1.4d : 1d)),
                Cap(nudSpd, 2d + lvl * 0.8d),
                Cap(nudDamage, (3d + 1.8d * lvl) * multiplier * damageWeight),
                Cap(nudExp, (5d + 3d * lvl + lvl * lvl * 0.5d) * multiplier)
            );
        }

        FrmBalanceLab.NpcAutoBalanceResult? calibration = null;
        (double ttk, double loss) Targets() => difficulty.SelectedIndex switch
        {
            0 => (4d, 10d),
            2 => (18d, 35d),
            3 => (40d, 65d),
            _ => (8d, 20d)
        };

        void RefreshPreview()
        {
            var s = CalculatePreset();
            var hp = calibration?.Calibrated == true ? calibration.Health : s.hp;
            var damage = calibration?.Calibrated == true ? calibration.Damage : s.damage;
            preview.Text =
                $"HP: {hp:N0}    Mana: {s.mana:N0}\n" +
                $"Attack: {s.attack:N0}    Magic: {s.magic:N0}    Speed: {s.speed:N0}\n" +
                $"Defense: {s.defense:N0}    Magic Resist: {s.resist:N0}\n" +
                $"Base Damage: {damage:N0}    EXP: {s.exp:N0}\n" +
                (calibration?.Calibrated == true
                    ? $"Simulated TTK: {calibration.TtkSeconds:0.0}s, HP lost: {calibration.HpLossPercent:0.0}%\n" +
                      $"Based on {calibration.ClassCount} player classes."
                    : "Not calibrated - select Simulate & Calibrate.");
        }

        void InvalidatePreview()
        {
            calibration = null;
            RefreshPreview();
        }

        level.ValueChanged += (_, _) => InvalidatePreview();
        role.SelectedIndexChanged += (_, _) => { RefreshSpells(); InvalidatePreview(); };
        spellPicker.ItemCheck += (_, _) =>
        {
            calibration = null;
            preview.Text = "Spell selection changed. Simulate again before applying.";
        };
        difficulty.SelectedIndexChanged += (_, _) => InvalidatePreview();
        party.ValueChanged += (_, _) => InvalidatePreview();

        bool EnsureCalibrated()
        {
            if (calibration?.Calibrated == true)
            {
                return true;
            }

            var proposed = CalculatePreset();
            var stats = new int[5];
            stats[(int)Stat.Attack] = proposed.attack;
            stats[(int)Stat.AbilityPower] = proposed.magic;
            stats[(int)Stat.Defense] = proposed.defense;
            stats[(int)Stat.MagicResist] = proposed.resist;
            stats[(int)Stat.Speed] = proposed.speed;
            var targets = Targets();

            simulate.Enabled = false;
            dialog.UseWaitCursor = true;
            try
            {
                var selectedIds = SelectedSpells();
                var simulationSpells = new List<Guid>();
                var simulationIntervals = new List<int>();
                for (var slot = 0; slot < mEditorItem.Spells.Count; slot++)
                {
                    if (mEditorItem.Spells[slot] == Guid.Empty) continue;
                    simulationSpells.Add(mEditorItem.Spells[slot]);
                    simulationIntervals.Add(mEditorItem.GetSpellCastIntervalOverrideMs(slot));
                }
                foreach (var spellId in selectedIds.Where(id => !simulationSpells.Contains(id)))
                {
                    var number = selectedIds.IndexOf(spellId);
                    simulationSpells.Add(spellId);
                    simulationIntervals.Add(number switch { 0 => 4000, 1 => 8000, _ => 15000 });
                }
                var frequency = mEditorItem.SpellFrequency;
                calibration = FrmBalanceLab.CalibrateGeneratedNpc(
                    (int)level.Value, (int)party.Value, targets.ttk, targets.loss,
                    proposed.hp, proposed.damage, stats,
                    role.SelectedIndex == 2 ? (int)DamageType.Magic : (int)DamageType.Physical,
                    role.SelectedIndex == 2 ? (int)Stat.AbilityPower : (int)Stat.Attack,
                    100, (long)nudHp.Maximum, (int)nudDamage.Maximum,
                    proposed.mana, simulationSpells, frequency, role.SelectedIndex is 1 or 2,
                    simulationIntervals);
            }
            catch (Exception ex)
            {
                calibration = null;
                MessageBox.Show(dialog, $"Could not simulate combat: {ex.Message}",
                    "NPC Auto Creator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally
            {
                dialog.UseWaitCursor = false;
                simulate.Enabled = true;
            }

            RefreshPreview();
            if (calibration.Calibrated)
            {
                return true;
            }
            MessageBox.Show(dialog, calibration.Message,
                "NPC Auto Creator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        simulate.Click += (_, _) => EnsureCalibrated();
        apply.Click += (_, _) =>
        {
            if (EnsureCalibrated())
            {
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
        };
        RefreshPreview();

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        // The Apply button cannot close the dialog without a valid simulation.
        if (calibration?.Calibrated != true)
        {
            return;
        }

        var result = CalculatePreset();
        static void Assign(NumericUpDown field, decimal value)
        {
            field.Value = Math.Clamp(value, field.Minimum, field.Maximum);
        }

        txtName.Text = name.Text;
        Assign(nudLevel, level.Value);
        Assign(nudHp, calibration!.Health);
        Assign(nudMana, result.mana);
        Assign(nudStr, result.attack);
        Assign(nudMag, result.magic);
        Assign(nudDef, result.defense);
        Assign(nudMR, result.resist);
        Assign(nudSpd, result.speed);
        Assign(nudDamage, calibration.Damage);
        Assign(nudExp, result.exp);
        Assign(nudScaling, 100);
        Assign(nudCritChance, 0);
        Assign(nudCritMultiplier, 1.50m);
        cmbAttackSpeedModifier.SelectedIndex = 0;
        Assign(nudAttackSpeedValue, 0);
        Assign(nudHpRegen, 0);
        Assign(nudMpRegen, 0);
        cmbDamageType.SelectedIndex = role.SelectedIndex == 2
            ? (int)DamageType.Magic : (int)DamageType.Physical;
        cmbScalingStat.SelectedIndex = role.SelectedIndex == 2
            ? (int)Stat.AbilityPower : (int)Stat.Attack;

        // Use existing tested NPC combat movement modes. Merely selecting
        // Ranged/Caster does NOT create attacks or spells; see dialog warning.
        _chkSmartCombatMovement.Checked = true;
        _cmbCombatMovementMode.SelectedIndex = role.SelectedIndex switch
        {
            1 => 2, // Ranged
            2 => 3, // Caster
            4 => 4, // Kite
            _ => 1, // Melee (including Tank)
        };
        Assign(_nudPreferredCombatRange, role.SelectedIndex switch
        {
            1 => 5, 2 => 5, 4 => 3, _ => 1
        });
        _chkIsBoss.Checked = difficulty.SelectedIndex == 3;
        chkAggressive.Checked = true;

        var selectedIds = SelectedSpells();
        for (var index = 0; index < selectedIds.Count; index++)
        {
            var spellId = selectedIds[index];
            // Existing spells and their configured intervals are preserved.
            if (mEditorItem.Spells.Contains(spellId)) continue;
            mEditorItem.EnsureSpellCastIntervalSlots();
            mEditorItem.Spells.Add(spellId);
            var slot = mEditorItem.Spells.Count - 1;
            mEditorItem.SetSpellCastIntervalMs(slot, index switch
            {
                0 => 4000, 1 => 8000, _ => 15000
            });
        }
        RefreshNpcSpellList(Math.Max(0, mEditorItem.Spells.Count - 1));

        MessageBox.Show(this,
            $"NPC profile applied. Simulation: {calibration.TtkSeconds:0.0}s TTK, " +
            $"{calibration.HpLossPercent:0.0}% HP loss. Review and click Save to persist. " +
            "NPC spell damage is estimated. Review combat spells and scripted encounters in-game.",
            "NPC Auto Creator", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
