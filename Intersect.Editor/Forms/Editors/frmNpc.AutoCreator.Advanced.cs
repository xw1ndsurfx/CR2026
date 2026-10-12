using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
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
        public CheckBox Drops { get; } = new() { Text = "Auto loot (Aurons, Auragems + safe consumables)", Checked = true };
        public CheckBox KeepDrops { get; } = new() { Text = "Keep existing drops", Checked = true };
        public CheckBox IndividualLoot { get; } = new() { Text = "Loot for all contributors on Boss", Checked = true };
        public CheckBox Hostility { get; } = new() { Text = "Configure NPC vs NPC hostility (opt-in)", Checked = false };
        public CheckBox Sight { get; } = new() { Text = "Auto aggressive sight range", Checked = true };
        public CheckBox DeathEvents { get; } = new() { Text = "Default [MONSTER] - BasicOne death events", Checked = true };
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
        // Mode 1 is a fixed attack interval IN MILLISECONDS, not a %.
        // Setting 10 or 20 previously caused 50-ms-clamped attacks.
        public int SpeedModifier(int role) => 1;
        public int SpeedValue(int role) => role switch
        {
            0 => 1050, // Warrior
            1 => 850,  // Archer
            2 => 1400, // Mage: most of its damage comes from spells
            3 => 1600, // Tank: deliberately slower
            4 => 700,  // Assassin: quick auto-attacks
            _ => 1100
        };
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
            options.KeepDrops, options.IndividualLoot, options.Hostility, options.KeepHostility,
            options.Sight, options.DeathEvents
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

        options.LootItem.Location = new System.Drawing.Point(20, 933);
        options.LootItem.Size = new Size(215, 25);
        options.HostileNpc.Location = new System.Drawing.Point(257, 933);
        options.HostileNpc.Size = new Size(215, 25);
        dialog.Controls.Add(new Label
        {
            Text = "Loot pool (safe consumables):", ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(20, 912), Size = new Size(215, 20)
        });
        dialog.Controls.Add(new Label
        {
            Text = "Hostile target (NPC vs NPC):", ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(257, 912), Size = new Size(215, 20)
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
            Text = "Aurons ~40-90%; Auragems ~0.03-0.15% (if named items exist). " +
                   "Check BasicOne event rewards to avoid paying currencies twice.",
            ForeColor = System.Drawing.Color.Khaki,
            Location = new System.Drawing.Point(20, 970),
            Size = new Size(450, 62)
        });
        dialog.AutoScrollMinSize = new Size(500, 1115);
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

        if (options.Sight.Checked)
        {
            // Aggressive without an appropriate sight radius makes an NPC feel idle.
            AssignNpcField(nudSightRange,
                Math.Min(20, (role switch { 1 => 9, 2 => 8, 3 => 6, 4 => 10, _ => 7 }) +
                    (difficulty switch { 2 => 1, 3 => 2, _ => 0 })));
        }

        if (options.DeathEvents.Checked)
        {
            // Never overwrite an authored quest/boss event.
            var basicMonsterEvent = EventDescriptor.Lookup.Values
                .OfType<EventDescriptor>()
                .FirstOrDefault(ev => ev.CommonEvent &&
                    ev.Name.Contains("BasicOne", StringComparison.OrdinalIgnoreCase) &&
                    ev.Name.Contains("MONSTER", StringComparison.OrdinalIgnoreCase));
            if (basicMonsterEvent != null)
            {
                if (mEditorItem.OnDeathEventId == Guid.Empty)
                    cmbOnDeathEventKiller.SelectedIndex =
                        EventDescriptor.ListIndex(basicMonsterEvent.Id) + 1;
                if (mEditorItem.OnDeathPartyEventId == Guid.Empty)
                    cmbOnDeathEventParty.SelectedIndex =
                        EventDescriptor.ListIndex(basicMonsterEvent.Id) + 1;
            }
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
            // Currency names originate in the game's item database, not the source
            // code. Never substitute an unrelated item when a name is absent.
            var itemRecords = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                .Where(item => item.ItemType is ItemType.Currency or ItemType.Consumable)
                .ToArray();
            ItemDescriptor? FindCurrency(params string[] names) =>
                itemRecords.FirstOrDefault(item => names.Contains(
                    item.Name.Trim(), StringComparer.OrdinalIgnoreCase));

            if (!options.KeepDrops.Checked)
            {
                mEditorItem.Drops.Clear();
            }

            // Auron/Aureon loot: common. Auragem loot: exceptionally rare.
            // These are percentages as displayed in the NPC editor, not ratios.
            void AddCurrencyDrop(ItemDescriptor? item, double chance, int maxAmount)
            {
                if (item == null || mEditorItem.Drops.Any(drop => drop.ItemId == item.Id))
                {
                    return;
                }
                mEditorItem.Drops.Add(new Drop
                {
                    ItemId = item.Id, MinQuantity = 1,
                    MaxQuantity = Math.Max(1, maxAmount),
                    Chance = chance
                });
            }
            var aurons = FindCurrency("Aurons", "Auron", "Aureons", "Aureon");
            var auragems = FindCurrency("Auragems", "Auragem", "Aureagems", "Aureagem");
            AddCurrencyDrop(aurons, difficulty switch
            {
                0 => 40d, 2 => 75d, 3 => 90d, _ => 60d
            }, 1 + Math.Max(1, level) / 10);
            AddCurrencyDrop(auragems, difficulty switch
            {
                2 => 0.07d, 3 => 0.15d, _ => 0.03d
            }, 1);

            var chosenId = options.LootIds[Math.Max(0, options.LootItem.SelectedIndex)];
            var eligible = chosenId != Guid.Empty
                ? new[] { ItemDescriptor.Get(chosenId) }.OfType<ItemDescriptor>().ToArray()
                : ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                    .Where(item => item.ItemType == ItemType.Consumable &&
                        item.CanTrade && item.EventId == Guid.Empty &&
                        item.UsageRequirements.Lists.All(list => list.Conditions.Count == 0) &&
                        item.Price <= Math.Max(50, level * 20) &&
                        item.Rarity <= difficulty + 1)
                    .OrderBy(item => Math.Abs(item.Price - level * 10))
                    .ThenBy(item => item.Name)
                    .Take(difficulty switch { 3 => 3, 2 => 2, _ => 1 })
                    .ToArray();

            if (eligible.Length > 0)
            {
                for (var index = 0; index < eligible.Length; index++)
                {
                    var item = eligible[index];
                    if (mEditorItem.Drops.Any(drop => drop.ItemId == item.Id))
                    {
                        continue;
                    }

                    var tierChance = difficulty switch
                    {
                        0 => 5d, 2 => 35d, 3 => 65d, _ => 15d
                    };
                    mEditorItem.Drops.Add(new Drop
                    {
                        ItemId = item.Id, MinQuantity = 1,
                        MaxQuantity = difficulty == 3 && index == 0 ? 3 : 1,
                        Chance = Math.Clamp(tierChance /
                            (1d + Math.Max(0, item.Rarity) * 0.5d + index), 0.1d, 100d)
                    });
                }
            }
            UpdateDropValues();
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
