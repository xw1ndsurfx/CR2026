using System.ComponentModel;
using System.Drawing.Imaging;
using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Content;
using Intersect.Editor.Core;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Editor.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.GameObjects;
using Intersect.Utilities;
using EventDescriptor = Intersect.Framework.Core.GameObjects.Events.EventDescriptor;
using Graphics = System.Drawing.Graphics;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmNpc : EditorForm
{

    private List<NPCDescriptor> mChanged = [];

    private string mCopiedItem;

    private NPCDescriptor mEditorItem;

    private List<string> mKnownFolders = [];

    private BindingList<NotifiableDrop> _dropList = [];

    private readonly DarkGroupBox _grpBoss = new();
    private readonly DarkCheckBox _chkIsBoss = new();
    private readonly DarkComboBox _cmbBossAnimation = new();
    private readonly DarkComboBox _cmbBossDeathAnimation = new();
    private readonly DarkNumericUpDown _nudBossAnimationOffsetY = new();

    private readonly DarkGroupBox _grpCombatAi = new();
    private readonly DarkCheckBox _chkSmartCombatMovement = new();
    private readonly DarkComboBox _cmbCombatMovementMode = new();
    private readonly DarkNumericUpDown _nudPreferredCombatRange = new();

    private readonly DarkGroupBox _grpPet = new();
    private readonly DarkCheckBox _chkIsPet = new();
    private readonly DarkComboBox _cmbPetItem = new();
    private readonly DarkNumericUpDown _nudPetLootRadius = new();
    private readonly DarkNumericUpDown _nudPetMaxLevel = new();
    private readonly DarkNumericUpDown _nudPetStatGrowth = new();
    private readonly DarkNumericUpDown _nudPetHealthGrowth = new();
    private readonly DarkNumericUpDown _nudPetSpellInterval = new();

    public FrmNpc()
    {
        ApplyHooks();
        InitializeComponent();
        BuildBossControls();
        BuildCombatAiControls();
        BuildPetControls();
        Icon = Program.Icon;
        _btnSave = btnSave;
        _btnCancel = btnCancel;

        lstGameObjects.Init(UpdateToolStripItems, AssignEditorItem, toolStripItemNew_Click, toolStripItemCopy_Click, toolStripItemUndo_Click, toolStripItemPaste_Click, toolStripItemDelete_Click);
    }
    private void BuildBossControls()
    {
        _grpBoss.Text = "Boss";
        _grpBoss.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        _grpBoss.BorderColor = System.Drawing.Color.FromArgb(90, 90, 90);
        _grpBoss.ForeColor = System.Drawing.Color.Gainsboro;
        _grpBoss.Location = new System.Drawing.Point(536, 724);
        _grpBoss.Size = new Size(298, 190);
        _grpBoss.TabStop = false;

        _chkIsBoss.Text = "This NPC is a boss";
        _chkIsBoss.AutoSize = true;
        _chkIsBoss.Location = new System.Drawing.Point(12, 26);
        _chkIsBoss.CheckedChanged += (_, _) =>
        {
            UpdateBossControlState();
            if (mEditorItem != null)
            {
                mEditorItem.IsBoss = _chkIsBoss.Checked;
            }
        };

        var lblBossAnimation = new Label
        {
            Text = "Boss overhead animation:",
            AutoSize = true,
            Location = new System.Drawing.Point(12, 57),
            ForeColor = System.Drawing.Color.Gainsboro,
        };

        _cmbBossAnimation.Location = new System.Drawing.Point(12, 76);
        _cmbBossAnimation.Size = new Size(272, 24);
        _cmbBossAnimation.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbBossAnimation.SelectedIndexChanged += (_, _) =>
        {
            if (mEditorItem == null)
            {
                return;
            }

            mEditorItem.BossAnimation =
                AnimationDescriptor.Get(
                    AnimationDescriptor.IdFromList(_cmbBossAnimation.SelectedIndex - 1)
                );
        };

        var lblOffset = new Label
        {
            Text = "Overhead Y offset:",
            AutoSize = true,
            Location = new System.Drawing.Point(12, 108),
            ForeColor = System.Drawing.Color.Gainsboro,
        };

        _nudBossAnimationOffsetY.Location = new System.Drawing.Point(166, 105);
        _nudBossAnimationOffsetY.Size = new Size(118, 24);
        _nudBossAnimationOffsetY.Minimum = -512;
        _nudBossAnimationOffsetY.Maximum = 512;
        _nudBossAnimationOffsetY.Value = -48;
        _nudBossAnimationOffsetY.ValueChanged += (_, _) =>
        {
            if (mEditorItem != null)
            {
                mEditorItem.BossAnimationOffsetY =
                    (int)_nudBossAnimationOffsetY.Value;
            }
        };

        var lblDeathAnimation = new Label
        {
            Text = "Animation played on death:",
            AutoSize = true,
            Location = new System.Drawing.Point(12, 137),
            ForeColor = System.Drawing.Color.Gainsboro,
        };

        _cmbBossDeathAnimation.Location = new System.Drawing.Point(12, 156);
        _cmbBossDeathAnimation.Size = new Size(272, 24);
        _cmbBossDeathAnimation.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbBossDeathAnimation.SelectedIndexChanged += (_, _) =>
        {
            if (mEditorItem == null)
            {
                return;
            }

            mEditorItem.DeathAnimation =
                AnimationDescriptor.Get(
                    AnimationDescriptor.IdFromList(_cmbBossDeathAnimation.SelectedIndex - 1)
                );
        };

        _grpBoss.Controls.Add(_chkIsBoss);
        _grpBoss.Controls.Add(lblBossAnimation);
        _grpBoss.Controls.Add(_cmbBossAnimation);
        _grpBoss.Controls.Add(lblOffset);
        _grpBoss.Controls.Add(_nudBossAnimationOffsetY);
        _grpBoss.Controls.Add(lblDeathAnimation);
        _grpBoss.Controls.Add(_cmbBossDeathAnimation);
        pnlContainer.Controls.Add(_grpBoss);

        UpdateBossControlState();
    }

    private void UpdateBossControlState()
    {
        var enabled = _chkIsBoss.Checked;
        _cmbBossAnimation.Enabled = enabled;
        _nudBossAnimationOffsetY.Enabled = enabled;
        _cmbBossDeathAnimation.Enabled = enabled;
    }

    private void BuildCombatAiControls()
    {
        _grpCombatAi.Text = "Combat AI";
        _grpCombatAi.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        _grpCombatAi.BorderColor = System.Drawing.Color.FromArgb(90, 90, 90);
        _grpCombatAi.ForeColor = System.Drawing.Color.Gainsboro;
        _grpCombatAi.Location = new System.Drawing.Point(844, 744);
        _grpCombatAi.Size = new Size(264, 170);
        _grpCombatAi.TabStop = false;

        _chkSmartCombatMovement.Text = "Smart combat movement";
        _chkSmartCombatMovement.AutoSize = true;
        _chkSmartCombatMovement.Location = new System.Drawing.Point(12, 25);
        _chkSmartCombatMovement.Checked = true;
        _chkSmartCombatMovement.CheckedChanged += (_, _) =>
        {
            UpdateCombatAiControlState();
            if (mEditorItem != null)
            {
                mEditorItem.SmartCombatMovement = _chkSmartCombatMovement.Checked;
            }
        };

        var lblMode = new Label
        {
            Text = "Combat style:",
            AutoSize = true,
            Location = new System.Drawing.Point(12, 55),
            ForeColor = System.Drawing.Color.Gainsboro,
        };

        _cmbCombatMovementMode.Location = new System.Drawing.Point(12, 74);
        _cmbCombatMovementMode.Size = new Size(238, 24);
        _cmbCombatMovementMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCombatMovementMode.Items.AddRange(
            new object[]
            {
                "Auto",
                "Melee",
                "Ranged",
                "Caster",
                "Kite",
                "Hold Position",
            }
        );
        _cmbCombatMovementMode.SelectedIndex = 0;
        _cmbCombatMovementMode.SelectedIndexChanged += (_, _) =>
        {
            if (mEditorItem != null)
            {
                mEditorItem.CombatMovementMode =
                    Math.Max(0, _cmbCombatMovementMode.SelectedIndex);
            }
        };

        var lblRange = new Label
        {
            Text = "Preferred range (0 = Auto):",
            AutoSize = true,
            Location = new System.Drawing.Point(12, 108),
            ForeColor = System.Drawing.Color.Gainsboro,
        };

        _nudPreferredCombatRange.Location = new System.Drawing.Point(12, 128);
        _nudPreferredCombatRange.Size = new Size(238, 24);
        _nudPreferredCombatRange.Minimum = 0;
        _nudPreferredCombatRange.Maximum = 20;
        _nudPreferredCombatRange.ValueChanged += (_, _) =>
        {
            if (mEditorItem != null)
            {
                mEditorItem.PreferredCombatRange =
                    (int)_nudPreferredCombatRange.Value;
            }
        };

        _grpCombatAi.Controls.Add(_chkSmartCombatMovement);
        _grpCombatAi.Controls.Add(lblMode);
        _grpCombatAi.Controls.Add(_cmbCombatMovementMode);
        _grpCombatAi.Controls.Add(lblRange);
        _grpCombatAi.Controls.Add(_nudPreferredCombatRange);
        pnlContainer.Controls.Add(_grpCombatAi);

        UpdateCombatAiControlState();
    }

    private void UpdateCombatAiControlState()
    {
        var enabled = _chkSmartCombatMovement.Checked;
        _cmbCombatMovementMode.Enabled = enabled;
        _nudPreferredCombatRange.Enabled = enabled;
    }

    private void BuildPetControls()
    {
        _grpPet.Text = "Player companion / Pet";
        _grpPet.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        _grpPet.BorderColor = System.Drawing.Color.FromArgb(90, 90, 90);
        _grpPet.ForeColor = System.Drawing.Color.Gainsboro;
        _grpPet.Location = new System.Drawing.Point(844, 925);
        _grpPet.Size = new Size(264, 384);
        _chkIsPet.Text = "Summonable companion";
        _chkIsPet.Location = new System.Drawing.Point(12, 23);
        _chkIsPet.AutoSize = true;
        _chkIsPet.CheckedChanged += (_, _) =>
        {
            UpdatePetControlState();
            if (mEditorItem != null) mEditorItem.IsPet = _chkIsPet.Checked;
        };
        _grpPet.Controls.Add(_chkIsPet);
        _grpPet.Controls.Add(new Label { Text = "Unlock/summon item:", AutoSize = true,
            Location = new System.Drawing.Point(12, 54), ForeColor = System.Drawing.Color.Gainsboro });
        _cmbPetItem.Location = new System.Drawing.Point(12, 75);
        _cmbPetItem.Size = new Size(238, 24);
        _cmbPetItem.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPetItem.SelectedIndexChanged += (_, _) =>
        {
            if (mEditorItem != null)
                mEditorItem.PetSummonItemId = ItemDescriptor.IdFromList(_cmbPetItem.SelectedIndex - 1);
        };
        _grpPet.Controls.Add(_cmbPetItem);
        AddPetNumber("Loot range (tiles, 0 = off):", _nudPetLootRadius, 110, 0, 8,
            v => mEditorItem.PetLootRadius = v);
        AddPetNumber("Maximum pet level:", _nudPetMaxLevel, 167, 1, 200,
            v => mEditorItem.PetMaxLevel = v);
        AddPetNumber("Stats gained per level:", _nudPetStatGrowth, 224, 0, 100,
            v => mEditorItem.PetStatGrowth = v);
        AddPetNumber("Health gained per level:", _nudPetHealthGrowth, 281, 0, 10000,
            v => mEditorItem.PetHealthGrowth = v);
        AddPetNumber("Spell unlock every N levels:", _nudPetSpellInterval, 338, 1, 200,
            v => mEditorItem.PetSpellUnlockInterval = v);
        pnlContainer.Controls.Add(_grpPet);
        UpdatePetControlState();
    }

    private void AddPetNumber(string caption, DarkNumericUpDown control, int y,
        int minimum, int maximum, Action<int> onChanged)
    {
        _grpPet.Controls.Add(new Label { Text = caption, AutoSize = true,
            Location = new System.Drawing.Point(12, y), ForeColor = System.Drawing.Color.Gainsboro });
        control.Location = new System.Drawing.Point(12, y + 19);
        control.Size = new Size(238, 24);
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.ValueChanged += (_, _) =>
        {
            if (mEditorItem != null) onChanged((int)control.Value);
        };
        _grpPet.Controls.Add(control);
    }

    private void UpdatePetControlState()
    {
        var enabled = _chkIsPet.Checked;
        _cmbPetItem.Enabled = enabled;
        _nudPetLootRadius.Enabled = enabled;
        _nudPetMaxLevel.Enabled = enabled;
        _nudPetStatGrowth.Enabled = enabled;
        _nudPetHealthGrowth.Enabled = enabled;
        _nudPetSpellInterval.Enabled = enabled;
    }

    private void AssignEditorItem(Guid id)
    {
        mEditorItem = NPCDescriptor.Get(id);
        UpdateEditor();
    }

    public bool SelectBalanceObject(Guid id)
    {
        return lstGameObjects.SelectObject(id);
    }

    protected override void GameObjectUpdatedDelegate(GameObjectType type)
    {
        if (type == GameObjectType.Npc)
        {
            InitEditor();
            if (mEditorItem != null && !NPCDescriptor.Lookup.Values.Contains(mEditorItem))
            {
                mEditorItem = null;
                UpdateEditor();
            }
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        foreach (var item in mChanged)
        {
            item.RestoreBackup();
            item.DeleteBackup();
        }

        Hide();
        Globals.CurrentEditor = -1;
        Dispose();
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        //Send Changed items
        foreach (var item in mChanged)
        {
            // Sort immunities to keep change checker consistent
            item.Immunities.Sort();

            PacketSender.SendSaveObject(item);
            item.DeleteBackup();
        }

        Hide();
        Globals.CurrentEditor = -1;
        Dispose();
    }

    public void RefreshEntitySpriteChoices()
    {
        // This method is only for refreshing an already-open editor after new
        // entity textures are generated. Preserve the current text without
        // forcing a fallback selection, which would trigger change handlers.
        var selected = cmbSprite.Text;

        cmbSprite.BeginUpdate();
        try
        {
            cmbSprite.Items.Clear();
            cmbSprite.Items.Add(Strings.General.None);
            cmbSprite.Items.AddRange(
                GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Entity)
            );

            var index = cmbSprite.FindStringExact(selected);
            if (index >= 0)
            {
                cmbSprite.SelectedIndex = index;
            }
        }
        finally
        {
            cmbSprite.EndUpdate();
        }
    }

    private void frmNpc_Load(object sender, EventArgs e)
    {
        // Keep the original load path event-safe. Selecting an item here can fire
        // cmbSprite events before the rest of the NPC editor controls are initialized.
        cmbSprite.Items.Clear();
        cmbSprite.Items.Add(Strings.General.None);
        cmbSprite.Items.AddRange(
            GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Entity)
        );

        cmbSpell.Items.Clear();
        cmbSpell.Items.AddRange(SpellDescriptor.Names);
        cmbHostileNPC.Items.Clear();
        cmbHostileNPC.Items.AddRange(NPCDescriptor.Names);
        cmbDropItem.Items.Clear();
        cmbDropItem.Items.Add(Strings.General.None);
        cmbDropItem.Items.AddRange(ItemDescriptor.Names);
        _cmbPetItem.Items.Clear();
        _cmbPetItem.Items.Add(Strings.General.None);
        _cmbPetItem.Items.AddRange(ItemDescriptor.Names);
        cmbAttackAnimation.Items.Clear();
        cmbAttackAnimation.Items.Add(Strings.General.None);
        cmbAttackAnimation.Items.AddRange(AnimationDescriptor.Names);
        _cmbBossAnimation.Items.Clear();
        _cmbBossAnimation.Items.Add(Strings.General.None);
        _cmbBossAnimation.Items.AddRange(AnimationDescriptor.Names);
        _cmbBossDeathAnimation.Items.Clear();
        _cmbBossDeathAnimation.Items.Add(Strings.General.None);
        _cmbBossDeathAnimation.Items.AddRange(AnimationDescriptor.Names);
        cmbOnDeathEventKiller.Items.Clear();
        cmbOnDeathEventKiller.Items.Add(Strings.General.None);
        cmbOnDeathEventKiller.Items.AddRange(EventDescriptor.Names);
        cmbOnDeathEventParty.Items.Clear();
        cmbOnDeathEventParty.Items.Add(Strings.General.None);
        cmbOnDeathEventParty.Items.AddRange(EventDescriptor.Names);
        cmbScalingStat.Items.Clear();
        for (var x = 0; x < Enum.GetValues<Stat>().Length; x++)
        {
            cmbScalingStat.Items.Add(Globals.GetStatName(x));
        }

        lstDrops.DataSource = _dropList;
        lstDrops.DisplayMember = nameof(NotifiableDrop.DisplayName);

        nudStr.Maximum = Options.Instance.Player.MaxStat;
        nudMag.Maximum = Options.Instance.Player.MaxStat;
        nudDef.Maximum = Options.Instance.Player.MaxStat;
        nudMR.Maximum = Options.Instance.Player.MaxStat;
        nudSpd.Maximum = Options.Instance.Player.MaxStat;
        InitLocalization();
        UpdateEditor();
    }

    private void InitLocalization()
    {
        Text = Strings.NpcEditor.title;
        toolStripItemNew.Text = Strings.NpcEditor.New;
        toolStripItemDelete.Text = Strings.NpcEditor.delete;
        toolStripItemCopy.Text = Strings.NpcEditor.copy;
        toolStripItemPaste.Text = Strings.NpcEditor.paste;
        toolStripItemUndo.Text = Strings.NpcEditor.undo;

        grpNpcs.Text = Strings.NpcEditor.npcs;

        grpGeneral.Text = Strings.NpcEditor.general;
        lblName.Text = Strings.NpcEditor.name;
        grpBehavior.Text = Strings.NpcEditor.behavior;

        lblPic.Text = Strings.NpcEditor.sprite;
        lblRed.Text = Strings.NpcEditor.Red;
        lblGreen.Text = Strings.NpcEditor.Green;
        lblBlue.Text = Strings.NpcEditor.Blue;
        lblAlpha.Text = Strings.NpcEditor.Alpha;

        lblSpawnDuration.Text = Strings.NpcEditor.spawnduration;

        //Behavior
        chkAggressive.Text = Strings.NpcEditor.aggressive;
        lblSightRange.Text = Strings.NpcEditor.sightrange;
        lblMovement.Text = Strings.NpcEditor.movement;
        lblResetRadius.Text = Strings.NpcEditor.resetradius;
        cmbMovement.Items.Clear();
        for (var i = 0; i < Strings.NpcEditor.movements.Count; i++)
        {
            cmbMovement.Items.Add(Strings.NpcEditor.movements[i]);
        }

        chkSwarm.Text = Strings.NpcEditor.swarm;
        lblFlee.Text = Strings.NpcEditor.flee;
        grpConditions.Text = Strings.NpcEditor.conditions;
        btnPlayerFriendProtectorCond.Text = Strings.NpcEditor.playerfriendprotectorconditions;
        btnAttackOnSightCond.Text = Strings.NpcEditor.attackonsightconditions;
        btnPlayerCanAttackCond.Text = Strings.NpcEditor.playercanattackconditions;
        chkFocusDamageDealer.Text = Strings.NpcEditor.focusdamagedealer;

        grpCommonEvents.Text = Strings.NpcEditor.commonevents;
        lblOnDeathEventKiller.Text = Strings.NpcEditor.ondeathevent;
        lblOnDeathEventParty.Text = Strings.NpcEditor.ondeathpartyevent;

        grpStats.Text = Strings.NpcEditor.stats;
        lblHP.Text = Strings.NpcEditor.hp;
        lblMana.Text = Strings.NpcEditor.mana;
        lblStr.Text = Strings.NpcEditor.attack;
        lblDef.Text = Strings.NpcEditor.defense;
        lblSpd.Text = Strings.NpcEditor.speed;
        lblMag.Text = Strings.NpcEditor.abilitypower;
        lblMR.Text = Strings.NpcEditor.magicresist;
        lblExp.Text = Strings.NpcEditor.exp;

        grpRegen.Text = Strings.NpcEditor.regen;
        lblHpRegen.Text = Strings.NpcEditor.hpregen;
        lblManaRegen.Text = Strings.NpcEditor.mpregen;
        lblRegenHint.Text = Strings.NpcEditor.regenhint;

        grpSpells.Text = Strings.NpcEditor.spells;
        lblSpell.Text = Strings.NpcEditor.spell;
        btnAdd.Text = Strings.NpcEditor.addspell;
        btnRemove.Text = Strings.NpcEditor.removespell;
        lblFreq.Text = Strings.NpcEditor.frequency;
        cmbFreq.Items.Clear();
        for (var i = 0; i < Strings.NpcEditor.frequencies.Count; i++)
        {
            cmbFreq.Items.Add(Strings.NpcEditor.frequencies[i]);
        }

        grpAttackSpeed.Text = Strings.NpcEditor.attackspeed;
        lblAttackSpeedModifier.Text = Strings.NpcEditor.attackspeedmodifier;
        lblAttackSpeedValue.Text = Strings.NpcEditor.attackspeedvalue;
        cmbAttackSpeedModifier.Items.Clear();
        foreach (var val in Strings.NpcEditor.attackspeedmodifiers.Values)
        {
            cmbAttackSpeedModifier.Items.Add(val.ToString());
        }

        grpNpcVsNpc.Text = Strings.NpcEditor.npcvsnpc;
        chkEnabled.Text = Strings.NpcEditor.enabled;
        chkAttackAllies.Text = Strings.NpcEditor.attackallies;
        lblNPC.Text = Strings.NpcEditor.npc;
        btnAddAggro.Text = Strings.NpcEditor.addhostility;
        btnRemoveAggro.Text = Strings.NpcEditor.removehostility;

        grpDrops.Text = Strings.NpcEditor.drops;
        lblDropItem.Text = Strings.NpcEditor.dropitem;
        lblDropMaxAmount.Text = Strings.NpcEditor.DropMaxAmount;
        lblDropMinAmount.Text = Strings.NpcEditor.DropMinAmount;
        lblDropChance.Text = Strings.NpcEditor.dropchance;
        btnDropAdd.Text = Strings.NpcEditor.dropadd;
        btnDropRemove.Text = Strings.NpcEditor.dropremove;
        chkIndividualLoot.Text = Strings.NpcEditor.individualizedloot;

        grpCombat.Text = Strings.NpcEditor.combat;
        lblDamage.Text = Strings.NpcEditor.basedamage;
        lblCritChance.Text = Strings.NpcEditor.critchance;
        lblCritMultiplier.Text = Strings.NpcEditor.critmultiplier;
        lblDamageType.Text = Strings.NpcEditor.damagetype;
        cmbDamageType.Items.Clear();
        for (var i = 0; i < Strings.Combat.damagetypes.Count; i++)
        {
            cmbDamageType.Items.Add(Strings.Combat.damagetypes[i]);
        }

        lblScalingStat.Text = Strings.NpcEditor.scalingstat;
        lblScaling.Text = Strings.NpcEditor.scalingamount;
        lblAttackAnimation.Text = Strings.NpcEditor.attackanimation;

        //Searching/Sorting
        btnAlphabetical.ToolTipText = Strings.NpcEditor.sortalphabetically;
        txtSearch.Text = Strings.NpcEditor.searchplaceholder;
        lblFolder.Text = Strings.NpcEditor.folderlabel;

        grpImmunities.Text = Strings.NpcEditor.ImmunitiesTitle;
        chkKnockback.Text = Strings.NpcEditor.Immunities[SpellEffect.Knockback];
        chkSilence.Text = Strings.NpcEditor.Immunities[SpellEffect.Silence];
        chkStun.Text = Strings.NpcEditor.Immunities[SpellEffect.Stun];
        chkSnare.Text = Strings.NpcEditor.Immunities[SpellEffect.Snare];
        chkBlind.Text = Strings.NpcEditor.Immunities[SpellEffect.Blind];
        chkTransform.Text = Strings.NpcEditor.Immunities[SpellEffect.Transform];
        chkTaunt.Text = Strings.NpcEditor.Immunities[SpellEffect.Taunt];
        chkSleep.Text = Strings.NpcEditor.Immunities[SpellEffect.Sleep];
        lblTenacity.Text = Strings.NpcEditor.Tenacity;

        btnSave.Text = Strings.NpcEditor.save;
        btnCancel.Text = Strings.NpcEditor.cancel;
    }

    private void UpdateEditor()
    {
        if (mEditorItem != null)
        {
            pnlContainer.Show();

            txtName.Text = mEditorItem.Name;
            cmbFolder.Text = mEditorItem.Folder;
            cmbSprite.SelectedIndex = cmbSprite.FindString(TextUtils.NullToNone(mEditorItem.Sprite));
            nudRgbaR.Value = mEditorItem.Color.R;
            nudRgbaG.Value = mEditorItem.Color.G;
            nudRgbaB.Value = mEditorItem.Color.B;
            nudRgbaA.Value = mEditorItem.Color.A;

            nudLevel.Value = mEditorItem.Level;
            nudSpawnDuration.Value = mEditorItem.SpawnDuration;

            _chkIsBoss.Checked = mEditorItem.IsBoss;
            _cmbBossAnimation.SelectedIndex =
                AnimationDescriptor.ListIndex(mEditorItem.BossAnimationId) + 1;
            _cmbBossDeathAnimation.SelectedIndex =
                AnimationDescriptor.ListIndex(mEditorItem.DeathAnimationId) + 1;
            _nudBossAnimationOffsetY.Value = Math.Clamp(
                mEditorItem.BossAnimationOffsetY,
                (int)_nudBossAnimationOffsetY.Minimum,
                (int)_nudBossAnimationOffsetY.Maximum
            );
            UpdateBossControlState();

            _chkSmartCombatMovement.Checked = mEditorItem.SmartCombatMovement;
            _cmbCombatMovementMode.SelectedIndex = Math.Clamp(
                mEditorItem.CombatMovementMode,
                0,
                _cmbCombatMovementMode.Items.Count - 1
            );
            _nudPreferredCombatRange.Value = Math.Clamp(
                mEditorItem.PreferredCombatRange,
                (int)_nudPreferredCombatRange.Minimum,
                (int)_nudPreferredCombatRange.Maximum
            );
            UpdateCombatAiControlState();
            _chkIsPet.Checked = mEditorItem.IsPet;
            _cmbPetItem.SelectedIndex = ItemDescriptor.ListIndex(mEditorItem.PetSummonItemId) + 1;
            _nudPetLootRadius.Value = Math.Clamp(mEditorItem.PetLootRadius, 0, 8);
            _nudPetMaxLevel.Value = Math.Clamp(mEditorItem.PetMaxLevel, 1, 200);
            _nudPetStatGrowth.Value = Math.Clamp(mEditorItem.PetStatGrowth, 0, 100);
            _nudPetHealthGrowth.Value = Math.Clamp(mEditorItem.PetHealthGrowth, 0, 10000);
            _nudPetSpellInterval.Value = Math.Clamp(mEditorItem.PetSpellUnlockInterval, 1, 200);
            UpdatePetControlState();

            //Behavior
            chkAggressive.Checked = mEditorItem.Aggressive;
            if (mEditorItem.Aggressive)
            {
                btnAttackOnSightCond.Text = Strings.NpcEditor.dontattackonsightconditions;
            }
            else
            {
                btnAttackOnSightCond.Text = Strings.NpcEditor.attackonsightconditions;
            }

            nudSightRange.Value = mEditorItem.SightRange;
            cmbMovement.SelectedIndex = Math.Min(mEditorItem.Movement, cmbMovement.Items.Count - 1);
            chkSwarm.Checked = mEditorItem.Swarm;
            nudFlee.Value = mEditorItem.FleeHealthPercentage;
            chkFocusDamageDealer.Checked = mEditorItem.FocusHighestDamageDealer;
            nudResetRadius.Value = mEditorItem.ResetRadius;

            //Common Events
            cmbOnDeathEventKiller.SelectedIndex = EventDescriptor.ListIndex(mEditorItem.OnDeathEventId) + 1;
            cmbOnDeathEventParty.SelectedIndex = EventDescriptor.ListIndex(mEditorItem.OnDeathPartyEventId) + 1;

            nudStr.Value = mEditorItem.Stats[(int)Stat.Attack];
            nudMag.Value = mEditorItem.Stats[(int)Stat.AbilityPower];
            nudDef.Value = mEditorItem.Stats[(int)Stat.Defense];
            nudMR.Value = mEditorItem.Stats[(int)Stat.MagicResist];
            nudSpd.Value = mEditorItem.Stats[(int)Stat.Speed];
            nudHp.Value = mEditorItem.MaxVitals[(int)Vital.Health];
            nudMana.Value = mEditorItem.MaxVitals[(int)Vital.Mana];
            nudExp.Value = mEditorItem.Experience;
            chkAttackAllies.Checked = mEditorItem.AttackAllies;
            chkEnabled.Checked = mEditorItem.NpcVsNpcEnabled;

            //Combat
            nudDamage.Value = mEditorItem.Damage;
            nudCritChance.Value = mEditorItem.CritChance;
            nudCritMultiplier.Value = (decimal)mEditorItem.CritMultiplier;
            nudScaling.Value = mEditorItem.Scaling;
            cmbDamageType.SelectedIndex = mEditorItem.DamageType;
            cmbScalingStat.SelectedIndex = mEditorItem.ScalingStat;
            cmbAttackAnimation.SelectedIndex = AnimationDescriptor.ListIndex(mEditorItem.AttackAnimationId) + 1;
            cmbAttackSpeedModifier.SelectedIndex = mEditorItem.AttackSpeedModifier;
            nudAttackSpeedValue.Value = mEditorItem.AttackSpeedValue;

            //Regen
            nudHpRegen.Value = mEditorItem.VitalRegen[(int)Vital.Health];
            nudMpRegen.Value = mEditorItem.VitalRegen[(int)Vital.Mana];

            // Add the spells to the list
            lstSpells.Items.Clear();
            for (var i = 0; i < mEditorItem.Spells.Count; i++)
            {
                if (mEditorItem.Spells[i] != Guid.Empty)
                {
                    lstSpells.Items.Add(SpellDescriptor.GetName(mEditorItem.Spells[i]));
                }
                else
                {
                    lstSpells.Items.Add(Strings.General.None);
                }
            }

            if (lstSpells.Items.Count > 0)
            {
                lstSpells.SelectedIndex = 0;
                cmbSpell.SelectedIndex = SpellDescriptor.ListIndex(mEditorItem.Spells[lstSpells.SelectedIndex]);
            }

            cmbFreq.SelectedIndex = mEditorItem.SpellFrequency;

            // Add the aggro NPC's to the list
            lstAggro.Items.Clear();
            for (var i = 0; i < mEditorItem.AggroList.Count; i++)
            {
                if (mEditorItem.AggroList[i] != Guid.Empty)
                {
                    lstAggro.Items.Add(NPCDescriptor.GetName(mEditorItem.AggroList[i]));
                }
                else
                {
                    lstAggro.Items.Add(Strings.General.None);
                }
            }

            UpdateDropValues();
            chkIndividualLoot.Checked = mEditorItem.IndividualizedLoot;

            DrawNpcSprite();
            if (mChanged.IndexOf(mEditorItem) == -1)
            {
                mChanged.Add(mEditorItem);
                mEditorItem.MakeBackup();
            }

            // Tenacity and immunities
            nudTenacity.Value = (decimal)mEditorItem.Tenacity;

            UpdateImmunities();
        }
        else
        {
            pnlContainer.Hide();
        }

        var hasItem = mEditorItem != null;
        UpdateEditorButtons(hasItem);
        UpdateToolStripItems();
    }

    private void txtName_TextChanged(object sender, EventArgs e)
    {
        mEditorItem.Name = txtName.Text;
        lstGameObjects.UpdateText(txtName.Text);
    }

    private void cmbSprite_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.Sprite = TextUtils.SanitizeNone(cmbSprite.Text);
        DrawNpcSprite();
    }

    private void DrawNpcSprite()
    {
        var picSpriteBmp = new Bitmap(picNpc.Width, picNpc.Height);
        var gfx = Graphics.FromImage(picSpriteBmp);
        gfx.FillRectangle(Brushes.Black, new Rectangle(0, 0, picNpc.Width, picNpc.Height));
        if (cmbSprite.SelectedIndex > 0)
        {
            var img = Image.FromFile("resources/entities/" + cmbSprite.Text);
            var imgAttributes = new ImageAttributes();

            // Microsoft, what the heck is this crap?
            imgAttributes.SetColorMatrix(
                new ColorMatrix(
                    new float[][]
                    {
                        new float[] { (float)nudRgbaR.Value / 255,  0,  0,  0, 0},  // Modify the red space
                        new float[] {0, (float)nudRgbaG.Value / 255,  0,  0, 0},    // Modify the green space
                        new float[] {0,  0, (float)nudRgbaB.Value / 255,  0, 0},    // Modify the blue space
                        new float[] {0,  0,  0, (float)nudRgbaA.Value / 255, 0},    // Modify the alpha space
                        new float[] {0, 0, 0, 0, 1}                                 // We're not adding any non-linear changes. Value of 1 at the end is a dummy value!
                    }
                )
            );

            gfx.DrawImage(
                img, new Rectangle(0, 0, img.Width / Options.Instance.Sprites.NormalFrames, img.Height / Options.Instance.Sprites.Directions),
                0, 0, img.Width / Options.Instance.Sprites.NormalFrames, img.Height / Options.Instance.Sprites.Directions, GraphicsUnit.Pixel, imgAttributes
            );

            img.Dispose();
            imgAttributes.Dispose();
        }

        gfx.Dispose();

        picNpc.BackgroundImage = picSpriteBmp;
    }

    private void UpdateDropValues()
    {
        _dropList.Clear();
        foreach (var drop in mEditorItem.Drops)
        {
            _dropList.Add(new NotifiableDrop
            {
                ItemId = drop.ItemId,
                MinQuantity = drop.MinQuantity,
                MaxQuantity = drop.MaxQuantity,
                Chance = drop.Chance
            });
        }
    }

    private void frmNpc_FormClosed(object sender, FormClosedEventArgs e)
    {
        btnCancel_Click(null, null);
    }

    private void btnAdd_Click(object sender, EventArgs e)
    {
        mEditorItem.Spells.Add(SpellDescriptor.IdFromList(cmbSpell.SelectedIndex));
        var n = lstSpells.SelectedIndex;
        lstSpells.Items.Clear();
        for (var i = 0; i < mEditorItem.Spells.Count; i++)
        {
            lstSpells.Items.Add(SpellDescriptor.GetName(mEditorItem.Spells[i]));
        }

        lstSpells.SelectedIndex = n;
    }

    private void btnRemove_Click(object sender, EventArgs e)
    {
        if (lstSpells.SelectedIndex > -1)
        {
            var i = lstSpells.SelectedIndex;
            lstSpells.Items.RemoveAt(i);
            mEditorItem.Spells.RemoveAt(i);
        }
    }

    private void cmbFreq_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.SpellFrequency = cmbFreq.SelectedIndex;
    }

    private void chkEnabled_CheckedChanged(object sender, EventArgs e)
    {
        mEditorItem.NpcVsNpcEnabled = chkEnabled.Checked;
    }

    private void chkAttackAllies_CheckedChanged(object sender, EventArgs e)
    {
        mEditorItem.AttackAllies = chkAttackAllies.Checked;
    }

    private void btnAddAggro_Click(object sender, EventArgs e)
    {
        mEditorItem.AggroList.Add(NPCDescriptor.IdFromList(cmbHostileNPC.SelectedIndex));
        lstAggro.Items.Clear();
        for (var i = 0; i < mEditorItem.AggroList.Count; i++)
        {
            if (mEditorItem.AggroList[i] != Guid.Empty)
            {
                lstAggro.Items.Add(NPCDescriptor.GetName(mEditorItem.AggroList[i]));
            }
            else
            {
                lstAggro.Items.Add(Strings.General.None);
            }
        }
    }

    private void btnRemoveAggro_Click(object sender, EventArgs e)
    {
        if (lstAggro.SelectedIndex > -1)
        {
            var i = lstAggro.SelectedIndex;
            lstAggro.Items.RemoveAt(i);
            mEditorItem.AggroList.RemoveAt(i);
        }
    }

    private void toolStripItemNew_Click(object sender, EventArgs e)
    {
        PacketSender.SendCreateObject(GameObjectType.Npc);
    }

    private void toolStripItemDelete_Click(object sender, EventArgs e)
    {
        if (mEditorItem != null && lstGameObjects.Focused)
        {
            if (DarkMessageBox.ShowWarning(
                    Strings.NpcEditor.deleteprompt, Strings.NpcEditor.deletetitle, DarkDialogButton.YesNo,
                    Icon
                ) ==
                DialogResult.Yes)
            {
                PacketSender.SendDeleteObject(mEditorItem);
            }
        }
    }

    private void toolStripItemCopy_Click(object sender, EventArgs e)
    {
        if (mEditorItem != null && lstGameObjects.Focused)
        {
            mCopiedItem = mEditorItem.JsonData;
            toolStripItemPaste.Enabled = true;
        }
    }

    private void toolStripItemPaste_Click(object sender, EventArgs e)
    {
        if (mEditorItem != null && mCopiedItem != null && lstGameObjects.Focused)
        {
            mEditorItem.Load(mCopiedItem, true);
            UpdateEditor();
        }
    }

    private void toolStripItemUndo_Click(object sender, EventArgs e)
    {
        if (mChanged.Contains(mEditorItem) && mEditorItem != null)
        {
            if (DarkMessageBox.ShowWarning(
                    Strings.NpcEditor.undoprompt, Strings.NpcEditor.undotitle, DarkDialogButton.YesNo,
                    Icon
                ) ==
                DialogResult.Yes)
            {
                mEditorItem.RestoreBackup();
                UpdateEditor();
            }
        }
    }

    private void UpdateToolStripItems()
    {
        toolStripItemCopy.Enabled = mEditorItem != null && lstGameObjects.Focused;
        toolStripItemPaste.Enabled = mEditorItem != null && mCopiedItem != null && lstGameObjects.Focused;
        toolStripItemDelete.Enabled = mEditorItem != null && lstGameObjects.Focused;
        toolStripItemUndo.Enabled = mEditorItem != null && lstGameObjects.Focused;
    }

    private void UpdateImmunities()
    {
        chkKnockback.Checked = mEditorItem.Immunities.Contains(SpellEffect.Knockback);
        chkSilence.Checked = mEditorItem.Immunities.Contains(SpellEffect.Silence);
        chkSnare.Checked = mEditorItem.Immunities.Contains(SpellEffect.Snare);
        chkStun.Checked = mEditorItem.Immunities.Contains(SpellEffect.Stun);
        chkSleep.Checked = mEditorItem.Immunities.Contains(SpellEffect.Sleep);
        chkTransform.Checked = mEditorItem.Immunities.Contains(SpellEffect.Transform);
        chkTaunt.Checked = mEditorItem.Immunities.Contains(SpellEffect.Taunt);
        chkBlind.Checked = mEditorItem.Immunities.Contains(SpellEffect.Blind);
    }

    private void form_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control)
        {
            if (e.KeyCode == Keys.N)
            {
                toolStripItemNew_Click(null, null);
            }
        }
    }

    private void cmbAttackAnimation_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.AttackAnimation =
            AnimationDescriptor.Get(AnimationDescriptor.IdFromList(cmbAttackAnimation.SelectedIndex - 1));
    }

    private void cmbDamageType_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.DamageType = cmbDamageType.SelectedIndex;
    }

    private void cmbScalingStat_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.ScalingStat = cmbScalingStat.SelectedIndex;
    }

    private void lstSpells_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstSpells.SelectedIndex > -1)
        {
            cmbSpell.SelectedIndex = SpellDescriptor.ListIndex(mEditorItem.Spells[lstSpells.SelectedIndex]);
        }
    }

    private void cmbSpell_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstSpells.SelectedIndex > -1 && lstSpells.SelectedIndex < mEditorItem.Spells.Count)
        {
            mEditorItem.Spells[lstSpells.SelectedIndex] = SpellDescriptor.IdFromList(cmbSpell.SelectedIndex);
        }

        var n = lstSpells.SelectedIndex;
        lstSpells.Items.Clear();
        for (var i = 0; i < mEditorItem.Spells.Count; i++)
        {
            lstSpells.Items.Add(SpellDescriptor.GetName(mEditorItem.Spells[i]));
        }

        lstSpells.SelectedIndex = n;
    }

    private void nudScaling_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Scaling = (int)nudScaling.Value;
    }

    private void nudSpawnDuration_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.SpawnDuration = (int)nudSpawnDuration.Value;
    }

    private void nudSightRange_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.SightRange = (int)nudSightRange.Value;
    }

    private void nudStr_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Stats[(int)Stat.Attack] = (int)nudStr.Value;
    }

    private void nudMag_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Stats[(int)Stat.AbilityPower] = (int)nudMag.Value;
    }

    private void nudDef_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Stats[(int)Stat.Defense] = (int)nudDef.Value;
    }

    private void nudMR_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Stats[(int)Stat.MagicResist] = (int)nudMR.Value;
    }

    private void nudSpd_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Stats[(int)Stat.Speed] = (int)nudSpd.Value;
    }

    private void nudDamage_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Damage = (int)nudDamage.Value;
    }

    private void nudCritChance_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.CritChance = (int)nudCritChance.Value;
    }

    private void nudHp_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.MaxVitals[(int)Vital.Health] = (int)nudHp.Value;
    }

    private void nudMana_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.MaxVitals[(int)Vital.Mana] = (int)nudMana.Value;
    }

    private void nudExp_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Experience = (int)nudExp.Value;
    }

    private void lstDrops_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstDrops.SelectedIndex > -1)
        {
            cmbDropItem.SelectedIndex = ItemDescriptor.ListIndex(mEditorItem.Drops[lstDrops.SelectedIndex].ItemId) + 1;
            nudDropMaxAmount.Value = mEditorItem.Drops[lstDrops.SelectedIndex].MaxQuantity;
            nudDropMinAmount.Value = mEditorItem.Drops[lstDrops.SelectedIndex].MinQuantity;
            nudDropChance.Value = (decimal)mEditorItem.Drops[lstDrops.SelectedIndex].Chance;
        }
    }

    private void cmbDropItem_SelectedIndexChanged(object sender, EventArgs e)
    {
        int index = lstDrops.SelectedIndex;
        if (index < 0 || index > lstDrops.Items.Count)
        {
            return;
        }

        mEditorItem.Drops[index].ItemId = ItemDescriptor.IdFromList(cmbDropItem.SelectedIndex - 1);
        _dropList[index].ItemId = mEditorItem.Drops[index].ItemId;
    }

    private void nudDropMaxAmount_ValueChanged(object sender, EventArgs e)
    {
        int index = lstDrops.SelectedIndex;
        if (index < 0 || index > lstDrops.Items.Count)
        {
            return;
        }

        mEditorItem.Drops[index].MaxQuantity = (int)nudDropMaxAmount.Value;
        _dropList[index].MaxQuantity = mEditorItem.Drops[index].MaxQuantity;
    }

    private void nudDropMinAmount_ValueChanged(object sender, EventArgs e)
    {
        int index = lstDrops.SelectedIndex;
        if (index < 0 || index > lstDrops.Items.Count)
        {
            return;
        }

        mEditorItem.Drops[index].MinQuantity = (int)nudDropMinAmount.Value;
        _dropList[index].MinQuantity = mEditorItem.Drops[index].MinQuantity;
    }

    private void nudDropChance_ValueChanged(object sender, EventArgs e)
    {
        int index = lstDrops.SelectedIndex;
        if (index < 0 || index > lstDrops.Items.Count)
        {
            return;
        }

        mEditorItem.Drops[index].Chance = (double)nudDropChance.Value;
        _dropList[index].Chance = mEditorItem.Drops[index].Chance;
    }

    private void btnDropAdd_Click(object sender, EventArgs e)
    {
        var drop = new Drop()
        {
            ItemId = ItemDescriptor.IdFromList(cmbDropItem.SelectedIndex - 1),
            MaxQuantity = (int)nudDropMaxAmount.Value,
            MinQuantity = (int)nudDropMinAmount.Value,
            Chance = (double)nudDropChance.Value
        };

        mEditorItem.Drops.Add(drop);

        _dropList.Add(new NotifiableDrop
        {
            ItemId = drop.ItemId,
            MinQuantity = drop.MinQuantity,
            MaxQuantity = drop.MaxQuantity,
            Chance = drop.Chance
        });

        lstDrops.SelectedIndex = _dropList.Count - 1;
    }

    private void btnDropRemove_Click(object sender, EventArgs e)
    {
        if (lstDrops.SelectedIndex < 0)
        {
            return;
        }

        var index = lstDrops.SelectedIndex;
        mEditorItem.Drops.RemoveAt(index);
        _dropList.RemoveAt(index);
    }

    private void chkIndividualLoot_CheckedChanged(object sender, EventArgs e)
    {
        mEditorItem.IndividualizedLoot = chkIndividualLoot.Checked;
    }

    private void nudLevel_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Level = (int)nudLevel.Value;
    }

    private void nudHpRegen_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.VitalRegen[(int)Vital.Health] = (int)nudHpRegen.Value;
    }

    private void nudMpRegen_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.VitalRegen[(int)Vital.Mana] = (int)nudMpRegen.Value;
    }

    private void chkAggressive_CheckedChanged(object sender, EventArgs e)
    {
        mEditorItem.Aggressive = chkAggressive.Checked;
        if (mEditorItem.Aggressive)
        {
            btnAttackOnSightCond.Text = Strings.NpcEditor.dontattackonsightconditions;
        }
        else
        {
            btnAttackOnSightCond.Text = Strings.NpcEditor.attackonsightconditions;
        }
    }

    private void cmbMovement_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.Movement = (byte)cmbMovement.SelectedIndex;
    }

    private void chkSwarm_CheckedChanged(object sender, EventArgs e)
    {
        mEditorItem.Swarm = chkSwarm.Checked;
    }

    private void nudFlee_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.FleeHealthPercentage = (byte)nudFlee.Value;
    }

    private void btnPlayerFriendProtectorCond_Click(object sender, EventArgs e)
    {
        var frm = new FrmDynamicRequirements(mEditorItem.PlayerFriendConditions, RequirementType.NpcFriend);
        frm.TopMost = true;
        frm.ShowDialog();
    }

    private void btnAttackOnSightCond_Click(object sender, EventArgs e)
    {
        if (chkAggressive.Checked)
        {
            var frm = new FrmDynamicRequirements(
                mEditorItem.AttackOnSightConditions, RequirementType.NpcDontAttackOnSight
            );

            frm.TopMost = true;
            frm.ShowDialog();
        }
        else
        {
            var frm = new FrmDynamicRequirements(
                mEditorItem.AttackOnSightConditions, RequirementType.NpcAttackOnSight
            );

            frm.TopMost = true;
            frm.ShowDialog();
        }
    }

    private void btnPlayerCanAttackCond_Click(object sender, EventArgs e)
    {
        var frm = new FrmDynamicRequirements(
            mEditorItem.PlayerCanAttackConditions, RequirementType.NpcCanBeAttacked
        );

        frm.TopMost = true;
        frm.ShowDialog();
    }

    private void cmbOnDeathEventKiller_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.OnDeathEvent = EventDescriptor.Get(EventDescriptor.IdFromList(cmbOnDeathEventKiller.SelectedIndex - 1));
    }

    private void cmbOnDeathEventParty_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.OnDeathPartyEvent = EventDescriptor.Get(EventDescriptor.IdFromList(cmbOnDeathEventParty.SelectedIndex - 1));
    }

    private void chkFocusDamageDealer_CheckedChanged(object sender, EventArgs e)
    {
        mEditorItem.FocusHighestDamageDealer = chkFocusDamageDealer.Checked;
    }

    private void nudCritMultiplier_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.CritMultiplier = (double)nudCritMultiplier.Value;
    }

    private void cmbAttackSpeedModifier_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.AttackSpeedModifier = cmbAttackSpeedModifier.SelectedIndex;
        nudAttackSpeedValue.Enabled = cmbAttackSpeedModifier.SelectedIndex > 0;
    }

    private void nudAttackSpeedValue_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.AttackSpeedValue = (int)nudAttackSpeedValue.Value;
    }

    private void nudRgbaR_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Color.R = (byte)nudRgbaR.Value;
        DrawNpcSprite();
    }

    private void nudRgbaG_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Color.G = (byte)nudRgbaG.Value;
        DrawNpcSprite();
    }

    private void nudRgbaB_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Color.B = (byte)nudRgbaB.Value;
        DrawNpcSprite();
    }

    private void nudRgbaA_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Color.A = (byte)nudRgbaA.Value;
        DrawNpcSprite();
    }

    private void nudResetRadius_ValueChanged(object sender, EventArgs e)
    {
        // So, the pathfinder on the server maintains a set max distance of whichever the largest value is, map height or width. Limit ourselves to this!
        var maxPathFindingDistance = Math.Max(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
        var maxUserEnteredValue = Math.Max(Options.Instance.Npc.ResetRadius, nudResetRadius.Value);

        // Use whatever is the lowest, either the maximum path find distance or the user entered value.
        nudResetRadius.Value = Math.Min(maxPathFindingDistance, maxUserEnteredValue);
        mEditorItem.ResetRadius = (int)nudResetRadius.Value;
    }

    #region "Item List - Folders, Searching, Sorting, Etc"

    public void InitEditor()
    {
        //Collect folders
        var mFolders = new List<string>();
        foreach (var itm in NPCDescriptor.Lookup)
        {
            if (!string.IsNullOrEmpty(((NPCDescriptor)itm.Value).Folder) &&
                !mFolders.Contains(((NPCDescriptor)itm.Value).Folder))
            {
                mFolders.Add(((NPCDescriptor)itm.Value).Folder);
                if (!mKnownFolders.Contains(((NPCDescriptor)itm.Value).Folder))
                {
                    mKnownFolders.Add(((NPCDescriptor)itm.Value).Folder);
                }
            }
        }

        mFolders.Sort();
        mKnownFolders.Sort();
        cmbFolder.Items.Clear();
        cmbFolder.Items.Add("");
        cmbFolder.Items.AddRange(mKnownFolders.ToArray());

        var items = NPCDescriptor.Lookup.OrderBy(p => p.Value?.Name).Select(pair => new KeyValuePair<Guid, KeyValuePair<string, string>>(pair.Key,
            new KeyValuePair<string, string>(((NPCDescriptor)pair.Value)?.Name ?? Models.DatabaseObject<NPCDescriptor>.Deleted, ((NPCDescriptor)pair.Value)?.Folder ?? ""))).ToArray();
        lstGameObjects.Repopulate(items, mFolders, btnAlphabetical.Checked, CustomSearch(), txtSearch.Text);
    }

    private void btnAddFolder_Click(object sender, EventArgs e)
    {
        var folderName = string.Empty;
        var result = DarkInputBox.ShowInformation(
            Strings.NpcEditor.folderprompt, Strings.NpcEditor.foldertitle, ref folderName, DarkDialogButton.OkCancel
        );

        if (result == DialogResult.OK && !string.IsNullOrEmpty(folderName))
        {
            if (!cmbFolder.Items.Contains(folderName))
            {
                mEditorItem.Folder = folderName;
                lstGameObjects.UpdateText(folderName);
                InitEditor();
                cmbFolder.Text = folderName;
            }
        }
    }

    private void cmbFolder_SelectedIndexChanged(object sender, EventArgs e)
    {
        mEditorItem.Folder = cmbFolder.Text;
        InitEditor();
    }

    private void btnAlphabetical_Click(object sender, EventArgs e)
    {
        btnAlphabetical.Checked = !btnAlphabetical.Checked;
        InitEditor();
    }

    private void txtSearch_TextChanged(object sender, EventArgs e)
    {
        InitEditor();
    }

    private void txtSearch_Leave(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSearch.Text))
        {
            txtSearch.Text = Strings.NpcEditor.searchplaceholder;
        }
    }

    private void txtSearch_Enter(object sender, EventArgs e)
    {
        txtSearch.SelectAll();
        txtSearch.Focus();
    }

    private void btnClearSearch_Click(object sender, EventArgs e)
    {
        txtSearch.Text = Strings.NpcEditor.searchplaceholder;
    }

    private bool CustomSearch()
    {
        return !string.IsNullOrWhiteSpace(txtSearch.Text) && txtSearch.Text != Strings.NpcEditor.searchplaceholder;
    }

    private void txtSearch_Click(object sender, EventArgs e)
    {
        if (txtSearch.Text == Strings.NpcEditor.searchplaceholder)
        {
            txtSearch.SelectAll();
        }
    }

    #endregion

    private void ChangeImmunity(SpellEffect status, bool isImmune)
    {
        if (isImmune && !mEditorItem.Immunities.Contains(status))
        {
            mEditorItem.Immunities.Add(status);
        }
        else if (!isImmune)
        {
            mEditorItem.Immunities.Remove(status);
        }
    }

    private void chkKnockback_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Knockback, chkKnockback.Checked);
    }

    private void chkSilence_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Silence, chkSilence.Checked);
    }

    private void chkStun_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Stun, chkStun.Checked);
    }

    private void chkSnare_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Snare, chkSnare.Checked);
    }

    private void chkBlind_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Blind, chkBlind.Checked);
    }

    private void chkTransform_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Transform, chkTransform.Checked);
    }

    private void chkSleep_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Sleep, chkSleep.Checked);
    }

    private void chkTaunt_CheckedChanged(object sender, EventArgs e)
    {
        ChangeImmunity(SpellEffect.Taunt, chkTaunt.Checked);
    }

    private void nudTenacity_ValueChanged(object sender, EventArgs e)
    {
        mEditorItem.Tenacity = (double)nudTenacity.Value;
    }
}
