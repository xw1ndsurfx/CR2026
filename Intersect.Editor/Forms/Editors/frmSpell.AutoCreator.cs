using System;
using System.Linq;
using System.Windows.Forms;
using DarkUI.Controls;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmSpell
{
    private readonly record struct SpellAutoEstimate(
        int HealthEffect, int ManaCost, int CastTime, int Cooldown, int Range,
        int Radius, int Duration, int Tick, int Scaling, int CritChance,
        decimal CritMultiplier, int StatBonus, long ReferenceHealth,
        int ReferenceCount, string Description);

    private void ShowSpellAutoCreator()
    {
        if (mEditorItem == null)
        {
            MessageBox.Show(this, "Select or create a spell first.", "Spell Auto Creator");
            return;
        }

        if (mEditorItem.SpellType != SpellType.CombatSpell &&
            MessageBox.Show(this,
                "This is a special spell (Warp/Dash/Event). Generating a combat " +
                "profile will change its type. Continue?",
                "Spell Auto Creator", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        using var wizard = new Form
        {
            Text = "Spell Auto Creator - Preview & Balance",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = new System.Drawing.Size(420, 370),
            Size = new System.Drawing.Size(555, 685),
            AutoScroll = true,
            AutoScrollMinSize = new System.Drawing.Size(530, 850),
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            ForeColor = System.Drawing.Color.Gainsboro,
            MaximizeBox = true
        };

        Label Field(string text, int y) => new()
        {
            Text = text,
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(20, y),
            Size = new System.Drawing.Size(186, 26)
        };

        var name = new TextBox
        {
            Location = new System.Drawing.Point(209, 19),
            Size = new System.Drawing.Size(285, 25), Text = txtName.Text
        };
        var level = new NumericUpDown
        {
            Location = new System.Drawing.Point(209, 59),
            Size = new System.Drawing.Size(285, 25),
            Minimum = 1, Maximum = Math.Max(1, Options.Instance.Player.MaxLevel),
            Value = 1
        };
        var template = new ComboBox
        {
            Location = new System.Drawing.Point(209, 99),
            Size = new System.Drawing.Size(285, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        template.Items.AddRange(new object[]
        {
            "Physical Strike", "Magic Bolt", "Area Damage",
            "Healing", "Damage over Time", "Stat Buff", "Crowd Control"
        });
        template.SelectedIndex = mEditorItem.Combat.Friendly ? 3 :
            mEditorItem.Combat.HoTDoT ? 4 :
            mEditorItem.Combat.DamageType == (int)DamageType.Physical ? 0 : 1;

        var tier = new ComboBox
        {
            Location = new System.Drawing.Point(209, 139),
            Size = new System.Drawing.Size(285, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        tier.Items.AddRange(new object[] { "Basic", "Standard", "Advanced", "Boss Ability" });
        tier.SelectedIndex = 1;

        var targeting = new ComboBox
        {
            Location = new System.Drawing.Point(209, 179),
            Size = new System.Drawing.Size(285, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        targeting.Items.AddRange(new object[] { "Single Target", "Area of Effect", "Self" });
        targeting.SelectedIndex = 0;

        var effect = new ComboBox
        {
            Location = new System.Drawing.Point(209, 219),
            Size = new System.Drawing.Size(285, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        effect.Items.AddRange(new object[] { "None", "Stun", "Snare", "Silence", "Blind" });
        effect.SelectedIndex = 0;

        var buffStat = new ComboBox
        {
            Location = new System.Drawing.Point(209, 259),
            Size = new System.Drawing.Size(285, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        buffStat.Items.AddRange(new object[]
        {
            "Attack", "Ability Power", "Defense", "Magic Resist", "Speed"
        });
        buffStat.SelectedIndex = 2;

        var keepArt = new CheckBox
        {
            Text = "Keep existing icon, cast sprite and animations",
            ForeColor = System.Drawing.Color.Gainsboro,
            Checked = true,
            Location = new System.Drawing.Point(20, 303),
            Size = new System.Drawing.Size(470, 26)
        };
        var keepRequirements = new Label
        {
            Text = "Casting requirements and special settings are preserved.",
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(20, 334),
            Size = new System.Drawing.Size(470, 26)
        };
        var description = new Label
        {
            Text = "Based on real NPC HP close to the chosen level when available. " +
                   "Damage is a starting value before armor, resistances and caster scaling.",
            ForeColor = System.Drawing.Color.Khaki,
            Location = new System.Drawing.Point(20, 377),
            Size = new System.Drawing.Size(475, 51)
        };
        var preview = new Label
        {
            Location = new System.Drawing.Point(20, 438),
            Size = new System.Drawing.Size(475, 218),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12),
            ForeColor = System.Drawing.Color.Gainsboro,
        };
        var apply = new Button
        {
            Text = "Apply to spell",
            Location = new System.Drawing.Point(310, 682),
            Size = new System.Drawing.Size(184, 36),
            DialogResult = DialogResult.None
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(20, 682),
            Size = new System.Drawing.Size(125, 36),
            DialogResult = DialogResult.Cancel
        };
        wizard.Controls.AddRange(new Control[]
        {
            Field("Spell Name", 21), name,
            Field("Target Level", 61), level,
            Field("Spell Role", 101), template,
            Field("Power Tier", 141), tier,
            Field("Targeting", 181), targeting,
            Field("Extra Effect", 221), effect,
            Field("Buff Stat", 261), buffStat,
            keepArt, keepRequirements, description, preview, apply, cancel
        });
        wizard.CancelButton = cancel;

        SpellAutoEstimate Calculate()
        {
            var spellLevel = (int)level.Value;
            var referenceNpcs = NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
                .Where(npc => !npc.IsPet && !npc.IsBoss &&
                    npc.Level > 0 && npc.MaxVitals[(int)Vital.Health] > 0)
                .OrderBy(npc => Math.Abs(npc.Level - spellLevel))
                .ThenBy(npc => npc.Id)
                .Take(25)
                .ToArray();
            // Use nearby level samples only. Do not assume a high-level NPC is
            // representative of a level 1 monster if the data set is sparse.
            var close = referenceNpcs.Where(npc =>
                Math.Abs(npc.Level - spellLevel) <= Math.Max(2, spellLevel / 10))
                .Select(npc => npc.MaxVitals[(int)Vital.Health])
                .OrderBy(value => value).ToArray();
            var referenceHp = close.Length == 0
                ? (long)Math.Clamp(30d + 12d * spellLevel +
                    1.5d * spellLevel * spellLevel, 30d, 10_000_000d)
                : close[close.Length / 2];

            var power = tier.SelectedIndex switch
            {
                0 => 0.65d, 2 => 1.5d, 3 => 2.2d, _ => 1d
            };
            var aoe = targeting.SelectedIndex == 1;
            var spellRole = template.SelectedIndex;
            var fraction = spellRole switch
            {
                2 => 0.11d, 3 => 0.18d, 4 => 0.055d,
                5 => 0d, 6 => 0.075d, _ => 0.17d
            };
            if (aoe) fraction *= 0.7d;
            var impact = (int)Math.Clamp(Math.Round(referenceHp * fraction * power),
                spellRole == 5 ? 0d : 1d, 1_000_000_000d);
            var manaCost = (int)Math.Clamp(Math.Round(
                (8d + spellLevel * 0.65d) * power *
                (aoe ? 1.5d : 1d) *
                (spellRole == 3 ? 1.2d : 1d)),
                0d, (double)nudMpCost.Maximum);
            var cast = spellRole switch { 0 => 180, 2 => 950, 3 => 500,
                4 => 700, 5 => 650, 6 => 550, _ => 420 };
            var cooldown = spellRole switch
            {
                0 => 1300, 2 => 6000, 3 => 3500, 4 => 5500,
                5 => 12000, 6 => 10000, _ => 2200
            };
            var duration = spellRole switch { 4 => 9000, 5 => 14000, 6 => 2000, _ => 0 };
            var range = targeting.SelectedIndex == 2 ? 0 :
                spellRole == 0 ? 1 : Math.Clamp(3 + spellLevel / 22, 3, 7);
            var radius = aoe ? (tier.SelectedIndex >= 2 ? 3 : 2) : 1;
            var scaling = spellRole == 5 ? 0 : spellRole switch
            {
                0 => 65, 2 => 40, 4 => 45, 6 => 35, _ => 55
            };
            var bonus = spellRole == 5 ? Math.Max(1, (int)Math.Round(3 +
                spellLevel * power * .45d)) : 0;
            return new SpellAutoEstimate(impact, manaCost, cast, cooldown,
                range, radius, duration, spellRole == 4 ? 3000 : 0,
                scaling, Math.Clamp(3 + tier.SelectedIndex * 3, 0, 25),
                tier.SelectedIndex == 3 ? 1.75m : 1.5m,
                bonus, referenceHp, close.Length,
                close.Length > 0 ? "Nearby NPC health median" :
                    "Fallback health curve (no NPC samples near this level)");
        }

        void RefreshPreview()
        {
            var p = Calculate();
            effect.Enabled = template.SelectedIndex == 6;
            buffStat.Enabled = template.SelectedIndex == 5;
            var percentage = p.ReferenceHealth > 0
                ? p.HealthEffect * 100d / p.ReferenceHealth : 0d;
            preview.Text =
                $"Reference NPC HP: {p.ReferenceHealth:N0}  ({p.ReferenceCount} samples)\n" +
                $"Base HP effect: {p.HealthEffect:N0}  (~{percentage:0.0}% of HP)\n" +
                $"Mana cost: {p.ManaCost:N0}  | Cast: {p.CastTime:N0} ms\n" +
                $"Cooldown: {p.Cooldown:N0} ms  | Range: {p.Range}\n" +
                $"Radius: {p.Radius}  | Duration: {p.Duration:N0} ms\n" +
                $"Scaling: {p.Scaling}%  | Crit: {p.CritChance}%\n" +
                (template.SelectedIndex == 5 ? $"Stat boost: +{p.StatBonus}\n" : "") +
                $"{p.Description}\nNo changes until Apply.";
        }

        void RefreshTemplate()
        {
            var role = template.SelectedIndex;
            targeting.SelectedIndex = role == 2 ? 1 :
                role is 3 or 5 ? 2 : 0;
            effect.SelectedIndex = role == 6 ? 1 : 0;
            RefreshPreview();
        }
        template.SelectedIndexChanged += (_, _) => RefreshTemplate();
        tier.SelectedIndexChanged += (_, _) => RefreshPreview();
        level.ValueChanged += (_, _) => RefreshPreview();
        targeting.SelectedIndexChanged += (_, _) => RefreshPreview();
        effect.SelectedIndexChanged += (_, _) => RefreshPreview();
        buffStat.SelectedIndexChanged += (_, _) => RefreshPreview();

        apply.Click += (_, _) =>
        {
            var role = template.SelectedIndex;
            var p = Calculate();

            static void Set(NumericUpDown target, decimal value) =>
                target.Value = Math.Clamp(value, target.Minimum, target.Maximum);

            // Existing requirements, scripts and assets are preserved by default.
            cmbType.SelectedIndex = (int)SpellType.CombatSpell;
            txtName.Text = name.Text;
            chkFriendly.Checked = role is 3 or 5;
            cmbDamageType.SelectedIndex = role == 0
                ? (int)DamageType.Physical : (int)DamageType.Magic;
            cmbScalingStat.SelectedIndex = role == 0
                ? (int)Stat.Attack : (int)Stat.AbilityPower;
            Set(nudHPDamage, p.HealthEffect);
            Set(nudMPDamage, 0);
            Set(nudScaling, p.Scaling);
            Set(nudCritChance, role == 5 ? 0 : p.CritChance);
            Set(nudCritMultiplier, p.CritMultiplier);
            Set(nudHPCost, 0);
            Set(nudMpCost, p.ManaCost);
            Set(nudCastDuration, p.CastTime);
            Set(nudCooldownDuration, p.Cooldown);
            cmbTargetType.SelectedIndex = targeting.SelectedIndex switch
            {
                1 => (int)SpellTargetType.AoE,
                2 => (int)SpellTargetType.Self,
                _ => (int)SpellTargetType.Single
            };
            Set(nudCastRange, p.Range);
            Set(nudHitRadius, p.Radius);
            chkHOTDOT.Checked = role == 4;
            Set(nudBuffDuration, p.Duration);
            Set(nudTick, p.Tick);

            if (role == 6 && Enum.TryParse<SpellEffect>(
                    effect.Text, true, out var extraEffect) &&
                (int)extraEffect >= 0 && (int)extraEffect < cmbExtraEffect.Items.Count)
            {
                cmbExtraEffect.SelectedIndex = (int)extraEffect;
            }
            else
                cmbExtraEffect.SelectedIndex = 0;

            var statFields = new[]
            {
                nudStr, nudMag, nudDef, nudMR, nudSpd
            };
            var percentageFields = new[]
            {
                nudStrPercentage, nudMagPercentage, nudDefPercentage,
                nudMRPercentage, nudSpdPercentage
            };
            for (var index = 0; index < statFields.Length; index++)
            {
                Set(statFields[index], role == 5 && index == buffStat.SelectedIndex
                    ? p.StatBonus : 0);
                Set(percentageFields[index], 0);
            }
            if (!keepArt.Checked)
                SuggestSpellArtwork(role, name.Text);

            // Do not clear mEditorItem.CastingRequirements or modify Warp/Dash
            // data, which can still be edited independently.
            UpdateEditor();
            MessageBox.Show(this,
                "Spell profile applied. Review the generated fields, animation, " +
                "effect and level benchmark, then click Save. Actual damage and " +
                "mana efficiency must be tested with real characters.",
                "Spell Auto Creator", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            wizard.DialogResult = DialogResult.OK;
            wizard.Close();
        };
        RefreshTemplate();
        wizard.ShowDialog(this);
    }

    private void SuggestSpellArtwork(int role, string proposedName)
    {
        var keyword = role switch
        {
            0 => "slash", 1 => "magic", 2 => "blast", 3 => "heal",
            4 => "poison", 5 => "buff", 6 => "stun", _ => "spell"
        };
        var name = (proposedName ?? "").Trim();
        var animations = AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>()
            .ToArray();
        var cast = animations.FirstOrDefault(animation =>
            animation.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
            animation.Name.Contains("cast", StringComparison.OrdinalIgnoreCase))
            ?? animations.FirstOrDefault(animation =>
                animation.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                animation.Name.Contains("cast", StringComparison.OrdinalIgnoreCase));
        var hit = animations.FirstOrDefault(animation =>
            animation.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
            !animation.Name.Contains("cast", StringComparison.OrdinalIgnoreCase))
            ?? animations.FirstOrDefault(animation =>
                animation.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (cast != null)
        {
            var index = AnimationDescriptor.ListIndex(cast.Id) + 1;
            if (index > 0 && index < cmbCastAnimation.Items.Count)
                cmbCastAnimation.SelectedIndex = index;
        }
        if (hit != null)
        {
            var index = AnimationDescriptor.ListIndex(hit.Id) + 1;
            if (index > 0 && index < cmbHitAnimation.Items.Count)
                cmbHitAnimation.SelectedIndex = index;
        }
        // Icons from sprites are chosen only when the library contains a
        // relevant name, never silently replacing a good custom icon.
        for (var i = 1; i < cmbSprite.Items.Count; i++)
        {
            if (cmbSprite.Items[i]?.ToString()?.Contains(keyword,
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                cmbSprite.SelectedIndex = i;
                break;
            }
        }
    }
}
