using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmItem
{
    private const string GeneratedSummaryMarker = "[Item Auto Creator]";
    private const string AutoLevelListName = "Auto Creator - Minimum Level";
    private readonly record struct GeneratedItemPreview(
        int Price, int Damage, int Crit, int AttackSpeedMs,
        int MainStat, int SecondaryStat, int Hp, int Mana,
        int HpRegen, int MpRegen, int BlockChance, int BlockAmount,
        int BlockAbsorption, int ConsumableValue, int BagSlots,
        int Scaling, string Summary);

    private void ShowItemAutoCreator()
    {
        if (mEditorItem == null)
        {
            MessageBox.Show(this, "Select an item or create a new one first.",
                "Item Auto Creator", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = "Item Auto Creator - Equipment & General Items",
            FormBorderStyle = FormBorderStyle.Sizable,
            StartPosition = FormStartPosition.CenterParent,
            Size = new System.Drawing.Size(585, 690),
            MinimumSize = new System.Drawing.Size(445, 390),
            AutoScroll = true,
            AutoScrollMinSize = new System.Drawing.Size(558, 1000),
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            ForeColor = System.Drawing.Color.Gainsboro,
            MaximizeBox = true
        };

        Label Label(string text, int top) => new()
        {
            Text = text,
            Location = new System.Drawing.Point(18, top),
            Size = new System.Drawing.Size(210, 24),
            ForeColor = System.Drawing.Color.Gainsboro
        };
        var name = new TextBox
        {
            Location = new System.Drawing.Point(242, 18), Width = 290,
            Text = txtName.Text
        };
        var type = new ComboBox
        {
            Location = new System.Drawing.Point(242, 59), Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        var supportedTypes = new[]
        {
            ItemType.Equipment, ItemType.Consumable, ItemType.Bag,
            ItemType.Currency, ItemType.Spell, ItemType.Event
        };
        foreach (var itemType in supportedTypes)
            type.Items.Add(itemType.ToString());
        type.SelectedIndex = Math.Max(0, Array.IndexOf(supportedTypes, mEditorItem.ItemType));

        var slot = new ComboBox
        {
            Location = new System.Drawing.Point(242, 101), Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var choice in cmbEquipmentSlot.Items)
            slot.Items.Add(choice);
        if (slot.Items.Count > 0)
            slot.SelectedIndex = Math.Clamp(mEditorItem.EquipmentSlot, 0, slot.Items.Count - 1);

        var level = new NumericUpDown
        {
            Location = new System.Drawing.Point(242, 142), Width = 290,
            Minimum = 1, Maximum = Math.Max(1, Options.Instance.Player.MaxLevel),
            Value = Math.Clamp(GetCurrentItemMinimumLevel(), 1,
                Math.Max(1, Options.Instance.Player.MaxLevel))
        };
        var rarity = new ComboBox
        {
            Location = new System.Drawing.Point(242, 183), Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var choice in cmbRarity.Items) rarity.Items.Add(choice);
        if (rarity.Items.Count == 0) rarity.Items.Add("Common");
        rarity.SelectedIndex = Math.Clamp(mEditorItem.Rarity, 0, rarity.Items.Count - 1);

        var build = new ComboBox
        {
            Location = new System.Drawing.Point(242, 224), Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        build.Items.AddRange(new object[]
        {
            "Balanced", "Offensive", "Defensive", "Magic", "Support"
        });
        build.SelectedIndex = 0;

        CheckBox Option(string text, int y, bool selected) => new()
        {
            Text = text, Checked = selected,
            Location = new System.Drawing.Point(18, y),
            Size = new System.Drawing.Size(515, 26),
            ForeColor = System.Drawing.Color.Gainsboro
        };
        var autoStats = Option("Generate combat / stats / vital bonuses", 271, true);
        var autoRegen = Option("Generate regen, shield defense and attack speed", 301, true);
        var autoPrice = Option("Generate price using level and rarity", 331, true);
        var autoDescription = Option("Write level, rarity, stats and value in description", 361, true);
        var applyLevel = Option("Set a real minimum-level usage requirement", 391, true);
        var replaceRequirements = Option(
            "Replace existing usage requirements (otherwise preserve)", 421, false);
        var preserveArt = Option("Keep existing icons, animations and paperdolls", 451, true);

        var warning = new Label
        {
            Location = new System.Drawing.Point(18, 491),
            Size = new System.Drawing.Size(515, 79),
            ForeColor = System.Drawing.Color.Khaki,
            Text = "Level and rarity scaling are starter presets, not battle-tested economy values. " +
                   "Special Spell/Event items keep their linked spell or event. " +
                   "An existing custom requirement will NOT be overwritten unless enabled."
        };
        var preview = new Label
        {
            Location = new System.Drawing.Point(18, 578),
            Size = new System.Drawing.Size(515, 286),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12),
            ForeColor = System.Drawing.Color.Gainsboro
        };
        var apply = new Button
        {
            Text = "Apply to selected item",
            Location = new System.Drawing.Point(303, 890),
            Size = new System.Drawing.Size(229, 36)
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(18, 890),
            Size = new System.Drawing.Size(124, 36),
            DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange(new Control[]
        {
            Label("Name", 20), name,
            Label("Item type", 61), type,
            Label("Equipment slot", 103), slot,
            Label("Target player level", 144), level,
            Label("Rarity", 185), rarity,
            Label("Stat profile", 226), build,
            autoStats, autoRegen, autoPrice, autoDescription,
            applyLevel, replaceRequirements, preserveArt, warning, preview,
            apply, cancel
        });
        dialog.CancelButton = cancel;

        ItemType SelectedType() => supportedTypes[Math.Max(0, type.SelectedIndex)];

        GeneratedItemPreview Calculate()
        {
            var lvl = (double)level.Value;
            var rarityIndex = Math.Clamp(rarity.SelectedIndex, 0, 5);
            var rarityMultiplier = new[] { 1d, 1.14d, 1.34d, 1.6d, 1.95d, 2.4d }[rarityIndex];
            var category = SelectedType();
            var selectedSlot = slot.SelectedIndex;
            var isWeapon = category == ItemType.Equipment &&
                selectedSlot == Options.Instance.Equipment.WeaponSlot;
            var isShield = category == ItemType.Equipment &&
                selectedSlot == Options.Instance.Equipment.ShieldSlot;
            var offensive = build.SelectedIndex == 1;
            var defensive = build.SelectedIndex == 2;
            var magic = build.SelectedIndex == 3;
            var support = build.SelectedIndex == 4;
            var accessory = category == ItemType.Equipment && !isWeapon && !isShield;

            int Safe(double number) => (int)Math.Clamp(Math.Round(number), 0, 1_000_000_000d);
            var rawPrice = category == ItemType.Currency ? 0 :
                Safe((12 + 9 * lvl + 1.7 * lvl * lvl) *
                    rarityMultiplier * rarityMultiplier *
                    (category is ItemType.Spell or ItemType.Event ? 1.15d : 1d));
            var price = (int)Math.Clamp((decimal)rawPrice,
                nudPrice.Minimum, nudPrice.Maximum);
            var weaponDamage = isWeapon ?
                Safe((4 + 1.15 * lvl + .045 * lvl * lvl) *
                    rarityMultiplier * (offensive ? 1.2d : magic ? 0.85d : 1d)) : 0;
            var mainStat = category == ItemType.Equipment
                ? Safe((1.5d + lvl * (isWeapon ? .55d : .37d)) *
                    rarityMultiplier * (offensive || magic ? 1.25d : 1d)) : 0;
            var secondaryStat = category == ItemType.Equipment
                ? Safe((1d + lvl * .22d) * rarityMultiplier *
                    (defensive || support ? 1.35d : .8d)) : 0;
            var hp = category == ItemType.Equipment ?
                Safe((4 + lvl * (defensive || isShield ? 2.25d : .85d)) *
                    rarityMultiplier) : 0;
            var mp = category == ItemType.Equipment && (magic || support)
                ? Safe((3 + lvl * 1.2d) * rarityMultiplier) : 0;
            var regen = category == ItemType.Equipment && defensive &&
                rarityIndex >= 2 ? Math.Min(3, rarityIndex - 1) : 0;
            var mpRegen = category == ItemType.Equipment && (magic || support) &&
                rarityIndex >= 2 ? Math.Min(3, rarityIndex - 1) : 0;
            var blockChance = isShield ? Math.Clamp(Safe(10 + lvl * .17d +
                rarityIndex * 3), 0, 60) : 0;
            var blockAmount = isShield ? Math.Clamp(Safe(9 + lvl * .22d +
                rarityIndex * 4), 0, 80) : 0;
            var blockAbsorb = isShield ? Math.Clamp(Safe(5 + lvl * .17d +
                rarityIndex * 2), 0, 75) : 0;
            var consumableValue = category == ItemType.Consumable ?
                Safe((12 + lvl * 5d) * rarityMultiplier) : 0;
            var bagSlots = category == ItemType.Bag
                ? Math.Clamp(Safe(6 + lvl / 5d + rarityIndex * 4d), 1, 100) : 0;

            return new GeneratedItemPreview(price, weaponDamage,
                isWeapon ? Math.Clamp(Safe(3 + lvl / 14d + rarityIndex * 2d), 0, 35) : 0,
                isWeapon ? magic ? 1450 : offensive ? 760 : 1000 : 0,
                mainStat, secondaryStat, hp, mp, regen, mpRegen,
                blockChance, blockAmount, blockAbsorb, consumableValue,
                bagSlots, isWeapon ? (magic ? 85 : 100) : 0,
                $"{category} • level {(int)level.Value} • {rarity.Text} • {build.Text}");
        }

        void UpdatePreview()
        {
            var p = Calculate();
            slot.Enabled = SelectedType() == ItemType.Equipment;
            var hasConditions = HasCustomItemRequirements();
            preview.Text =
                $"{p.Summary}\n" +
                $"Price: {p.Price:N0}  |  Weapon Damage: {p.Damage:N0}\n" +
                $"Main/Secondary Stats: +{p.MainStat} / +{p.SecondaryStat}\n" +
                $"HP: +{p.Hp}  |  MP: +{p.Mana}\n" +
                $"Regen HP/MP: {p.HpRegen}% / {p.MpRegen}%\n" +
                $"Crit: {p.Crit}%  |  Attack interval: {p.AttackSpeedMs} ms\n" +
                $"Shield block chance/amount/absorb: {p.BlockChance}% / " +
                    $"{p.BlockAmount}% / {p.BlockAbsorption}%\n" +
                $"Consumable value: {p.ConsumableValue}  |  Bag slots: {p.BagSlots}\n" +
                $"Minimum level: {(int)level.Value}" +
                (hasConditions && !replaceRequirements.Checked
                    ? " (existing conditions preserved; level NOT installed)"
                    : " (will be installed if selected)") +
                "\nNo changes until Apply.";
        }
        type.SelectedIndexChanged += (_, _) => UpdatePreview();
        slot.SelectedIndexChanged += (_, _) => UpdatePreview();
        level.ValueChanged += (_, _) => UpdatePreview();
        rarity.SelectedIndexChanged += (_, _) => UpdatePreview();
        build.SelectedIndexChanged += (_, _) => UpdatePreview();
        replaceRequirements.CheckedChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        apply.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                MessageBox.Show(dialog, "Enter an item name.", "Item Auto Creator");
                return;
            }
            var itemType = SelectedType();
            var p = Calculate();
            var realRequirement = applyLevel.Checked &&
                (replaceRequirements.Checked || !HasCustomItemRequirements());

            if (applyLevel.Checked && !realRequirement)
            {
                var answer = MessageBox.Show(dialog,
                    "This item already has custom usage conditions. " +
                    "Its existing conditions will be preserved, and no new level " +
                    "requirement will be installed. Continue?",
                    "Existing Requirements", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            static void Set(NumericUpDown input, decimal value)
            {
                input.Value = Math.Clamp(value, input.Minimum, input.Maximum);
            }

            cmbType.SelectedIndex = (int)itemType;
            txtName.Text = name.Text;
            cmbRarity.SelectedIndex = Math.Clamp(rarity.SelectedIndex,
                0, Math.Max(0, cmbRarity.Items.Count - 1));
            if (itemType == ItemType.Equipment && slot.SelectedIndex >= 0)
                cmbEquipmentSlot.SelectedIndex = slot.SelectedIndex;

            if (autoPrice.Checked) Set(nudPrice, p.Price);
            if (autoStats.Checked && itemType == ItemType.Equipment)
            {
                // Distribute bonuses by selected build without touching the
                // item icon, event links, paperdolls or equipped animations.
                var index = build.SelectedIndex switch
                {
                    3 => 1, 2 => 2, 4 => 3, _ => 0
                };
                var statControls = new NumericUpDown[]
                {
                    nudStr, nudMag, nudDef, nudMR, nudSpd
                };
                for (var i = 0; i < statControls.Length; i++)
                    Set(statControls[i], i == index ? p.MainStat :
                        (i == (index == 0 ? 2 : 0) ? p.SecondaryStat : 0));
                Set(nudHealthBonus, p.Hp);
                Set(nudManaBonus, p.Mana);
                Set(nudDamage, p.Damage);
                Set(nudCritChance, p.Crit);
                if (p.Damage > 0)
                {
                    Set(nudCritMultiplier, rarity.SelectedIndex >= 4 ? 1.75m : 1.50m);
                    cmbDamageType.SelectedIndex = build.SelectedIndex == 3
                        ? (int)DamageType.Magic : (int)DamageType.Physical;
                    cmbScalingStat.SelectedIndex = build.SelectedIndex == 3
                        ? (int)Stat.AbilityPower : (int)Stat.Attack;
                    Set(nudScaling, p.Scaling);
                }
                if (slot.SelectedIndex == Options.Instance.Equipment.ShieldSlot)
                {
                    Set(nudBlockChance, p.BlockChance);
                    Set(nudBlockAmount, p.BlockAmount);
                    Set(nudBlockDmgAbs, p.BlockAbsorption);
                }
            }
            if (autoRegen.Checked && itemType == ItemType.Equipment)
            {
                Set(nudHPRegen, p.HpRegen);
                Set(nudMpRegen, p.MpRegen);
                if (p.AttackSpeedMs > 0 && cmbAttackSpeedModifier.Items.Count > 1)
                {
                    cmbAttackSpeedModifier.SelectedIndex = 1; // Static (ms)
                    Set(nudAttackSpeedValue, p.AttackSpeedMs);
                }
            }
            if (autoStats.Checked && itemType == ItemType.Consumable)
            {
                // Health by default; users can change to Mana or other types.
                cmbConsume.SelectedIndex = (int)ConsumableType.Health;
                Set(nudInterval, p.ConsumableValue);
                Set(nudIntervalPercentage, 0);
            }
            if (autoStats.Checked && itemType == ItemType.Bag)
                Set(nudBag, p.BagSlots);

            if (itemType is ItemType.Currency or ItemType.Consumable)
            {
                chkStackable.Checked = true;
                Set(nudInvStackLimit, 1000000);
                Set(nudBankStackLimit, 1000000);
            }

            if (realRequirement)
            {
                mEditorItem.UsageRequirements ??= new ConditionLists();
                if (replaceRequirements.Checked)
                    mEditorItem.UsageRequirements.Lists.Clear();
                else
                    mEditorItem.UsageRequirements.Lists.RemoveAll(
                        list => IsAutoGeneratedLevelList(list));
                mEditorItem.UsageRequirements.Lists.Add(new ConditionList
                {
                    Name = AutoLevelListName,
                    Conditions =
                    [
                        new LevelOrStatCondition
                        {
                            ComparingLevel = true,
                            Comparator = VariableComparator.GreaterOrEqual,
                            Value = (int)level.Value
                        }
                    ]
                });
                txtCannotUse.Text = $"Requires level {(int)level.Value}.";
            }

            if (autoDescription.Checked)
            {
                var old = txtDesc.Text ?? "";
                var markerIndex = old.IndexOf(GeneratedSummaryMarker,
                    StringComparison.Ordinal);
                if (markerIndex >= 0)
                    old = old[..markerIndex].TrimEnd();
                var levelInfo = realRequirement
                    ? $"Requires level {(int)level.Value}"
                    : "Custom usage requirements preserved";
                var summary = $"{GeneratedSummaryMarker}\n" +
                    $"Rarity: {rarity.Text} | Recommended level: {(int)level.Value}\n" +
                    $"{levelInfo}\n" +
                    $"Value: {p.Price:N0} Aurons\n" +
                    (itemType == ItemType.Equipment
                        ? $"Damage: {p.Damage} | Bonus stats: +{p.MainStat}/+{p.SecondaryStat}" +
                          $" | HP: +{p.Hp} | MP: +{p.Mana}"
                        : itemType == ItemType.Consumable
                            ? $"Effect: +{p.ConsumableValue} HP"
                            : itemType == ItemType.Bag
                                ? $"Bag capacity: {p.BagSlots} slots"
                                : $"Type: {itemType}");
                txtDesc.Text = string.IsNullOrWhiteSpace(old) ? summary
                    : old.TrimEnd() + "\n\n" + summary;
            }

            UpdateEditor();
            MessageBox.Show(this,
                "Item profile applied. Inspect the stats, rarity, price and " +
                "real usage requirements, then click Save. Balance values are " +
                "presets and must be tested in game.",
                "Item Auto Creator", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        dialog.ShowDialog(this);
    }

    private static bool IsAutoGeneratedLevelList(ConditionList list) =>
        list.Name == AutoLevelListName && list.Conditions.Count == 1 &&
        list.Conditions[0] is LevelOrStatCondition { ComparingLevel: true };

    private bool HasCustomItemRequirements() =>
        mEditorItem?.UsageRequirements?.Lists.Any(
            list => !IsAutoGeneratedLevelList(list)) == true;

    private int GetCurrentItemMinimumLevel()
    {
        var levels = mEditorItem?.UsageRequirements?.Lists
            .SelectMany(list => list.Conditions)
            .OfType<LevelOrStatCondition>()
            .Where(condition => condition.ComparingLevel &&
                condition.Comparator == VariableComparator.GreaterOrEqual)
            .Select(condition => condition.Value)
            .ToArray();
        return levels is { Length: > 0 } ? Math.Max(1, levels.Max()) : 1;
    }
}
