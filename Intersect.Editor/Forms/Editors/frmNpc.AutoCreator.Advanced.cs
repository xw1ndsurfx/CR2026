using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmNpc
{
    private sealed class NpcAutoAdvancedOptions
    {
        public CheckBox Combat { get; } = new() { Text = "Auto combat / crit / attack speed", Checked = true };
        public CheckBox Regen { get; } = new() { Text = "Auto HP & MP regeneration", Checked = true };
        public CheckBox Immunities { get; } = new() { Text = "Auto tenacity & immunities", Checked = true };
        public CheckBox Drops { get; } = new() { Text = "Generate a safe basic loot drop (opt-in)", Checked = false };
        public CheckBox KeepDrops { get; } = new() { Text = "Keep existing drops", Checked = true };
        public CheckBox IndividualLoot { get; } = new() { Text = "Loot for all contributors on Boss", Checked = true };
        public CheckBox Hostility { get; } = new() { Text = "Configure NPC vs NPC hostility (opt-in)", Checked = false };
        public CheckBox KeepHostility { get; } = new() { Text = "Keep existing hostility list", Checked = true };
        public ComboBox LootItem { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        public ComboBox HostileNpc { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        public Action? Changed { get; set; }
        public List<Guid> LootIds { get; } = [];
        public List<Guid> HostileIds { get; } = [];

        public int CritChance(int role, int difficulty)
        {
            var baseValue = role switch { 1 => 12, 2 => 6, 3 => 2, 4 => 20, _ => 7 };
            return Math.Clamp(baseValue + (difficulty switch
            {
                0 => -3, 2 => 3, 3 => 6, _ => 0
            }), 0, 60);
        }

        public decimal CritMultiplier(int role) => role switch
        {
            1 => 1.65m, 3 => 1.35m, 4 => 1.90m, _ => 1.50m
        };

        public int Scaling(int role) => role switch
        {
            1 => 105, 2 => 115, 3 => 85, 4 => 125, _ => 100
        };

        // The existing index 1 speed mode allows a value on the NPC Editor.
        // We only enable it for faster attack archetypes.
        public int SpeedModifier(int role) => role is 1 or 4 ? 1 : 0;
        public int SpeedValue(int role) => role switch { 1 => 10, 4 => 20, _ => 0 };
    }

    private NpcAutoAdvancedOptions BuildNpcAutoAdvancedOptions(Form dialog)
    {
        var options = new NpcAutoAdvancedOptions();
        var heading = new Label
        {
            Text = "Advanced Auto Settings",
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(20, 594),
            Size = new Size(450, 22)
        };
        dialog.Controls.Add(heading);
        var toggles = new[]
        {
            options.Combat, options.Regen, options.Immunities, options.Drops,
            options.KeepDrops, options.IndividualLoot, options.Hostility, options.KeepHostility
        };
        for (var i = 0; i < toggles.Length; i++)
        {
            var box = toggles[i];
            box.ForeColor = System.Drawing.Color.Gainsboro;
            box.AutoSize = false;
            box.Size = new Size(450, 24);
            box.Location = new System.Drawing.Point(22, 620 + i * 27);
            box.CheckedChanged += (_, _) => options.Changed?.Invoke();
            dialog.Controls.Add(box);
        }

        options.LootItem.Location = new System.Drawing.Point(20, 875);
        options.LootItem.Size = new Size(215, 25);
        options.HostileNpc.Location = new System.Drawing.Point(257, 875);
        options.HostileNpc.Size = new Size(215, 25);
        dialog.Controls.Add(new Label
        {
            Text = "Loot pool (safe consumables):", ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(20, 854), Size = new Size(215, 20)
        });
        dialog.Controls.Add(new Label
        {
            Text = "Hostile target (NPC vs NPC):", ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(257, 854), Size = new Size(215, 20)
        });

        options.LootItem.Items.Add("Auto-select safe consumable");
        options.LootIds.Add(Guid.Empty);
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                     .Where(item => item.ItemType == ItemType.Consumable &&
                         item.CanTrade && item.EventId == Guid.Empty &&
                         item.UsageRequirements.Lists.All(list => list.Conditions.Count == 0))
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            options.LootItem.Items.Add(item.Name);
            options.LootIds.Add(item.Id);
        }
        options.LootItem.SelectedIndex = 0;
        options.LootItem.SelectedIndexChanged += (_, _) => options.Changed?.Invoke();

        options.HostileNpc.Items.Add("None / Neutral");
        options.HostileIds.Add(Guid.Empty);
        foreach (var npc in NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
                     .Where(npc => npc.Id != mEditorItem?.Id)
                     .OrderBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase))
        {
            options.HostileNpc.Items.Add(npc.Name);
            options.HostileIds.Add(npc.Id);
        }
        options.HostileNpc.SelectedIndex = 0;
        options.HostileNpc.SelectedIndexChanged += (_, _) => options.Changed?.Invoke();
        dialog.Controls.Add(options.LootItem);
        dialog.Controls.Add(options.HostileNpc);

        dialog.Controls.Add(new Label
        {
            Text = "Drop generation is OFF by default. No currencies, equipment, " +
                   "quest-locked or event items are auto-selected. Hostility stays OFF unless enabled.",
            ForeColor = System.Drawing.Color.Khaki,
            Location = new System.Drawing.Point(20, 916),
            Size = new Size(450, 62)
        });
        dialog.AutoScrollMinSize = new Size(500, 1060);
        return options;
    }

    private void ApplyNpcAutoAdvancedOptions(NpcAutoAdvancedOptions options, int role,
        int difficulty, int level)
    {
        if (options.Combat.Checked)
        {
            AssignNpcField(nudCritChance, options.CritChance(role, difficulty));
            AssignNpcField(nudCritMultiplier, options.CritMultiplier(role));
            AssignNpcField(nudScaling, options.Scaling(role));
            var speedMode = options.SpeedModifier(role);
            cmbAttackSpeedModifier.SelectedIndex = Math.Min(speedMode,
                Math.Max(0, cmbAttackSpeedModifier.Items.Count - 1));
            AssignNpcField(nudAttackSpeedValue, options.SpeedValue(role));

            // Only choose an animation if a matching one exists; never wipe a
            // manually selected animation when the art library lacks a match.
            var keywords = role switch
            {
                1 => new[] { "shoot", "arrow", "bow" },
                2 => new[] { "cast", "magic", "spell" },
                4 => new[] { "stab", "slash", "attack" },
                _ => new[] { "slash", "swing", "attack" }
            };
            var animation = AnimationDescriptor.Lookup.Values
                .OfType<AnimationDescriptor>()
                .FirstOrDefault(candidate => keywords.Any(keyword =>
                    candidate.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            if (animation != null)
            {
                var index = AnimationDescriptor.ListIndex(animation.Id) + 1;
                if (index >= 0 && index < cmbAttackAnimation.Items.Count)
                    cmbAttackAnimation.SelectedIndex = index;
            }
        }

        if (options.Regen.Checked)
        {
            var hpRegen = difficulty switch { 2 => 1, 3 => 2, _ => 0 };
            if (role == 3) hpRegen++;
            var manaRegen = role == 2
                ? difficulty switch { 2 => 3, 3 => 4, _ => 2 }
                : 0;
            AssignNpcField(nudHpRegen, hpRegen);
            AssignNpcField(nudMpRegen, manaRegen);
        }

        if (options.Immunities.Checked)
        {
            AssignNpcField(nudTenacity, difficulty switch
            {
                2 => role == 3 ? 15 : 10,
                3 => role == 3 ? 35 : 25,
                _ => 0
            });
            // Add appropriate immunities instead of removing authored boss
            // mechanics. A normal NPC receives no automatic immunities.
            if (difficulty >= 2)
            {
                chkKnockback.Checked = role == 3 || difficulty == 3 ||
                    chkKnockback.Checked;
            }
            if (difficulty == 3)
            {
                chkStun.Checked = true;
                if (role == 2) chkSilence.Checked = true;
                if (role == 3) chkSnare.Checked = true;
            }
        }

        if (options.Drops.Checked)
        {
            var chosenId = options.LootIds[Math.Max(0, options.LootItem.SelectedIndex)];
            if (chosenId == Guid.Empty)
            {
                var eligible = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                    .Where(item => item.ItemType == ItemType.Consumable &&
                        item.CanTrade && item.EventId == Guid.Empty &&
                        item.UsageRequirements.Lists.All(list => list.Conditions.Count == 0) &&
                        item.Price <= Math.Max(50, level * 20) &&
                        item.Rarity <= difficulty + 1)
                    .OrderBy(item => Math.Abs(item.Price - level * 10))
                    .ThenBy(item => item.Name)
                    .ToArray();
                chosenId = eligible.FirstOrDefault()?.Id ?? Guid.Empty;
            }

            if (chosenId != Guid.Empty)
            {
                if (!options.KeepDrops.Checked) mEditorItem.Drops.Clear();
                if (!mEditorItem.Drops.Any(drop => drop.ItemId == chosenId))
                {
                    mEditorItem.Drops.Add(new Drop
                    {
                        ItemId = chosenId, MinQuantity = 1,
                        MaxQuantity = difficulty == 3 ? 3 : 1,
                        Chance = difficulty switch
                        {
                            0 => 5d, 2 => 35d, 3 => 65d, _ => 15d
                        }
                    });
                }
                UpdateDropValues();
            }
            if (difficulty == 3 && options.IndividualLoot.Checked)
                chkIndividualLoot.Checked = true;
        }

        if (options.Hostility.Checked)
        {
            var hostileId = options.HostileIds[Math.Max(0, options.HostileNpc.SelectedIndex)];
            if (!options.KeepHostility.Checked) mEditorItem.AggroList.Clear();
            if (hostileId != Guid.Empty && !mEditorItem.AggroList.Contains(hostileId))
                mEditorItem.AggroList.Add(hostileId);
            chkEnabled.Checked = mEditorItem.AggroList.Count > 0;
            chkAttackAllies.Checked = false;
            lstAggro.Items.Clear();
            foreach (var id in mEditorItem.AggroList)
                lstAggro.Items.Add(NPCDescriptor.GetName(id));
        }
    }

    private static void AssignNpcField(NumericUpDown field, decimal value)
    {
        field.Value = Math.Clamp(value, field.Minimum, field.Maximum);
    }
}
