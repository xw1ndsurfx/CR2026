using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game.Character;


public partial class CharacterWindow
{

    //Equipment List
    public List<EquipmentItem> Items = new List<EquipmentItem>();

    Label mAbilityPwrLabel;

    Button mAddAbilityPwrBtn;

    Button mAddAttackBtn;

    Button mAddDefenseBtn;

    Button mAddMagicResistBtn;

    Button mAddSpeedBtn;

    //Stats
    Label mAttackLabel;

    private ImagePanel mCharacterContainer;

    private Label mCharacterLevelAndClass;

    private Label mCharacterName;

    private ImagePanel mCharacterPortrait;

    private string mCharacterPortraitImg = string.Empty;

    //Controls
    private WindowControl mCharacterWindow;

    private string mCurrentSprite = string.Empty;

    Label mDefenseLabel;

    private ItemProperties mItemProperties = null;

    Label mMagicRstLabel;

    Label mPointsLabel;

    Label mSpeedLabel;

    public ImagePanel[] PaperdollPanels;

    public string[] PaperdollTextures;

    //Location
    public int X;

    public int Y;

    //Extra Buffs
    Button _detailsButton;

    private WindowControl _detailsWindow;
    private ScrollControl _detailsScroll;

    Button _achievementsButton;

    Button _petsButton;
    
    ClassDescriptor mPlayer;

    long HpRegenAmount;

    long ManaRegenAmount;

    int LifeStealAmount = 0;

    int ExtraExpAmount = 0;

    int LuckAmount = 0;

    int TenacityAmount = 0;

    int CooldownAmount = 0;

    int ManaStealAmount = 0;

    private PlayerProfessionProfilePacket[] _professionDetails = [];

    //Init
    public CharacterWindow(Canvas gameCanvas)
    {
        mCharacterWindow = new WindowControl(gameCanvas, Strings.Character.Title, false, "CharacterWindow");
        mCharacterWindow.DisableResizing();

        mCharacterName = new Label(mCharacterWindow, "CharacterNameLabel");
        mCharacterName.SetTextColor(Color.White, ComponentState.Normal);

        mCharacterLevelAndClass = new Label(mCharacterWindow, "ChatacterInfoLabel");
        mCharacterLevelAndClass.SetText("");

        mCharacterContainer = new ImagePanel(mCharacterWindow, "CharacterContainer");

        mCharacterPortrait = new ImagePanel(mCharacterContainer);

        PaperdollPanels = new ImagePanel[Options.Instance.Equipment.Slots.Count + 1];
        PaperdollTextures = new string[Options.Instance.Equipment.Slots.Count + 1];
        for (var i = 0; i <= Options.Instance.Equipment.Slots.Count; i++)
        {
            PaperdollPanels[i] = new ImagePanel(mCharacterContainer);
            PaperdollTextures[i] = string.Empty;
            PaperdollPanels[i].Hide();
        }

        var equipmentLabel = new Label(mCharacterWindow, "EquipmentLabel");
        equipmentLabel.SetText(Strings.Character.Equipment);

        var statsLabel = new Label(mCharacterWindow, "StatsLabel");
        statsLabel.SetText(Strings.Character.Stats);

        mAttackLabel = new Label(mCharacterWindow, "AttackLabel");

        mAddAttackBtn = new Button(mCharacterWindow, "IncreaseAttackButton");
        mAddAttackBtn.Clicked += _addAttackBtn_Clicked;

        mDefenseLabel = new Label(mCharacterWindow, "DefenseLabel");
        mAddDefenseBtn = new Button(mCharacterWindow, "IncreaseDefenseButton");
        mAddDefenseBtn.Clicked += _addDefenseBtn_Clicked;

        mSpeedLabel = new Label(mCharacterWindow, "SpeedLabel");
        mAddSpeedBtn = new Button(mCharacterWindow, "IncreaseSpeedButton");
        mAddSpeedBtn.Clicked += _addSpeedBtn_Clicked;

        mAbilityPwrLabel = new Label(mCharacterWindow, "AbilityPowerLabel");
        mAddAbilityPwrBtn = new Button(mCharacterWindow, "IncreaseAbilityPowerButton");
        mAddAbilityPwrBtn.Clicked += _addAbilityPwrBtn_Clicked;

        mMagicRstLabel = new Label(mCharacterWindow, "MagicResistLabel");
        mAddMagicResistBtn = new Button(mCharacterWindow, "IncreaseMagicResistButton");
        mAddMagicResistBtn.Clicked += _addMagicResistBtn_Clicked;

        mPointsLabel = new Label(mCharacterWindow, "PointsLabel");

        for (var i = 0; i < Options.Instance.Equipment.Slots.Count; i++)
        {
            Items.Add(new EquipmentItem(i, mCharacterWindow));
            Items[i].Pnl = new ImagePanel(mCharacterWindow, "EquipmentItem" + i);
            Items[i].Setup();
        }

        _detailsButton = new Button(mCharacterWindow, nameof(_detailsButton))
        {
            Text = Strings.Character.ExtraBuffDetails,
        };
        // Details now opens a dedicated, scrollable RPG panel rather than a tooltip.
        _detailsButton.Clicked += (_, _) => OpenDetails();

        mCharacterWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());

        _detailsWindow = new WindowControl(gameCanvas, "Character Details", false, "CharacterDetailsWindow");
        _detailsWindow.SetSize(650, 530);
        _detailsWindow.DisableResizing();
        _detailsWindow.Alignment = [Alignments.Center];
        _detailsScroll = new ScrollControl(_detailsWindow, "CharacterDetailsScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _detailsScroll.SetBounds(20, 48, 610, 445);
        _detailsWindow.IsHidden = true;

        // Create Achievements after JSON layout is loaded. This prevents legacy
        // CharacterWindow JSON/font data from leaving the dynamically-added button
        // with stale text geometry (which could render as only a single glyph).
        _achievementsButton = new Button(mCharacterWindow, nameof(_achievementsButton))
        {
            // The button's internal Text control can collapse to a single glyph with
            // the global Empire 7 bitmap override. Keep the clickable button textless
            // and render its caption with a normal Label child instead.
            Text = string.Empty,
        };
        _achievementsButton.Clicked += (_, _) =>
            PacketSender.SendRequestAchievementState(openWindow: true);

        _petsButton = new Button(mCharacterWindow, nameof(_petsButton)) { Text = string.Empty };
        _petsButton.Clicked += (_, _) => PacketSender.SendRequestPetState(openWindow: true);
        var petsCaption = new Label(_petsButton, "PetsCaption")
        {
            AutoSizeToContents = false,
            Font = _detailsButton.Font,
            FontSize = _detailsButton.FontSize,
            Text = "Pets",
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
        };

        var achievementsCaption = new Label(_achievementsButton, "AchievementsCaption")
        {
            AutoSizeToContents = false,
            Font = _detailsButton.Font,
            FontSize = _detailsButton.FontSize,
            Text = "Achievements",
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
        };

        // Keep all three actions evenly spaced within the character window.
        // Fixed 132px buttons exceeded the window width and clipped Pets.
        const int actionButtonMargin = 12;
        const int actionButtonGap = 6;
        var actionButtonWidth = Math.Max(60, (mCharacterWindow.Width - actionButtonMargin * 2 - actionButtonGap * 2) / 3);
        var actionButtonHeight = Math.Max(24, _detailsButton.Height);
        var actionButtonsX = actionButtonMargin;
        _detailsButton.FontSize = 9;
        var actionButtonsY = _detailsButton.Y;

        _detailsButton.SetBounds(
            actionButtonsX,
            actionButtonsY,
            actionButtonWidth,
            actionButtonHeight
        );
        _achievementsButton.SetBounds(
            actionButtonsX + actionButtonWidth + actionButtonGap,
            actionButtonsY,
            actionButtonWidth,
            actionButtonHeight
        );
        _petsButton.SetBounds(actionButtonsX + 2 * (actionButtonWidth + actionButtonGap),
            actionButtonsY, actionButtonWidth, actionButtonHeight);
        petsCaption.SetBounds(0, 0, actionButtonWidth, actionButtonHeight);
        petsCaption.BringToFront();
        achievementsCaption.SetBounds(0, 0, actionButtonWidth, actionButtonHeight);
        achievementsCaption.BringToFront();
    }

    //Update Button Event Handlers
    void _addMagicResistBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        PacketSender.SendUpgradeStat((int) Stat.MagicResist);
    }

    void _addAbilityPwrBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        PacketSender.SendUpgradeStat((int) Stat.AbilityPower);
    }

    void _addSpeedBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        PacketSender.SendUpgradeStat((int) Stat.Speed);
    }

    void _addDefenseBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        PacketSender.SendUpgradeStat((int) Stat.Defense);
    }

    void _addAttackBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        PacketSender.SendUpgradeStat((int) Stat.Attack);
    }

    //Methods
    public void Update()
    {
        if (mCharacterWindow.IsHidden || Globals.Me is null)
        {
            return;
        }

        mCharacterName.Text = Globals.Me.Name;
        mCharacterLevelAndClass.Text = Strings.Character.LevelAndClass.ToString(
            Globals.Me.Level, ClassDescriptor.GetName(Globals.Me.Class)
        );

        //Load Portrait
        //UNCOMMENT THIS LINE IF YOU'D RATHER HAVE A FACE HERE IGameTexture faceTex = Globals.ContentManager.GetTexture(Framework.Content.TextureType.Face, Globals.Me.Face);
        var entityTex = Globals.ContentManager.GetTexture(
            Framework.Content.TextureType.Entity, Globals.Me.Sprite
        );

        /* UNCOMMENT THIS BLOCK IF YOU"D RATHER HAVE A FACE HERE if (Globals.Me.Face != "" && Globals.Me.Face != _currentSprite && faceTex != null)
         {
             _characterPortrait.Texture = faceTex;
             _characterPortrait.SetTextureRect(0, 0, faceTex.GetWidth(), faceTex.GetHeight());
             _characterPortrait.SizeToContents();
             Align.Center(_characterPortrait);
             _characterPortrait.IsHidden = false;
             for (int i = 0; i < Options.Instance.Equipment.Slots.Count; i++)
             {
                 _paperdollPanels[i].Hide();
             }
         }
         else */
        if (!string.IsNullOrWhiteSpace(Globals.Me.Sprite) && Globals.Me.Sprite != mCurrentSprite && entityTex != null)
        {
            for (var z = 0; z < Options.Instance.Equipment.Paperdoll.Directions[1].Count; z++)
            {
                var paperdoll = string.Empty;
                if (Options.Instance.Equipment.Slots.IndexOf(Options.Instance.Equipment.Paperdoll.Directions[1][z]) > -1)
                {
                    var equipment = Globals.Me.MyEquipment;
                    if (equipment[Options.Instance.Equipment.Slots.IndexOf(Options.Instance.Equipment.Paperdoll.Directions[1][z])] > -1 &&
                        equipment[Options.Instance.Equipment.Slots.IndexOf(Options.Instance.Equipment.Paperdoll.Directions[1][z])] <
                        Options.Instance.Player.MaxInventory)
                    {
                        var itemNum = Globals.Me
                            .Inventory[equipment[Options.Instance.Equipment.Slots.IndexOf(Options.Instance.Equipment.Paperdoll.Directions[1][z])]]
                            .ItemId;

                        if (ItemDescriptor.TryGet(itemNum, out var itemDescriptor))
                        {
                            paperdoll = Globals.Me.Gender == 0
                                ? itemDescriptor.MalePaperdoll : itemDescriptor.FemalePaperdoll;
                            PaperdollPanels[z].RenderColor = itemDescriptor.Color;
                        }
                    }
                }
                else if (Options.Instance.Equipment.Paperdoll.Directions[1][z] == "Player")
                {
                    PaperdollPanels[z].Show();
                    PaperdollPanels[z].Texture = entityTex;
                    PaperdollPanels[z].SetTextureRect(0, 0, entityTex.Width / Options.Instance.Sprites.NormalFrames, entityTex.Height / Options.Instance.Sprites.Directions);
                    PaperdollPanels[z].SizeToContents();
                    PaperdollPanels[z].RenderColor = Globals.Me.Color;
                    Align.Center(PaperdollPanels[z]);
                }

                if (string.IsNullOrWhiteSpace(paperdoll) && !string.IsNullOrWhiteSpace(PaperdollTextures[z]) && Options.Instance.Equipment.Paperdoll.Directions[1][z] != "Player")
                {
                    PaperdollPanels[z].Texture = null;
                    PaperdollPanels[z].Hide();
                    PaperdollTextures[z] = string.Empty;
                }
                else if (paperdoll != "" && paperdoll != PaperdollTextures[z])
                {
                    var paperdollTex = Globals.ContentManager.GetTexture(
                        Framework.Content.TextureType.Paperdoll, paperdoll
                    );

                    PaperdollPanels[z].Texture = paperdollTex;
                    if (paperdollTex != null)
                    {
                        PaperdollPanels[z]
                            .SetTextureRect(
                                0, 0, PaperdollPanels[z].Texture.Width / Options.Instance.Sprites.NormalFrames,
                                PaperdollPanels[z].Texture.Height / Options.Instance.Sprites.Directions
                            );

                        PaperdollPanels[z]
                            .SetSize(
                                PaperdollPanels[z].Texture.Width / Options.Instance.Sprites.NormalFrames,
                                PaperdollPanels[z].Texture.Height / Options.Instance.Sprites.Directions
                            );

                        PaperdollPanels[z]
                            .SetPosition(
                                mCharacterContainer.Width / 2 - PaperdollPanels[z].Width / 2,
                                mCharacterContainer.Height / 2 - PaperdollPanels[z].Height / 2
                            );
                    }

                    PaperdollPanels[z].Show();
                    PaperdollTextures[z] = paperdoll;
                }
            }
        }
        else if (Globals.Me.Sprite != mCurrentSprite && Globals.Me.Face != mCurrentSprite)
        {
            mCharacterPortrait.IsHidden = true;
            for (var i = 0; i < Options.Instance.Equipment.Slots.Count; i++)
            {
                PaperdollPanels[i].Hide();
            }
        }

        mAttackLabel.SetText(
            Strings.Character.StatLabelValue.ToString(
                Strings.Combat.Stats[Stat.Attack],
                Globals.Me.Stat[(int)Stat.Attack]
            )
        );

        mAbilityPwrLabel.SetText(
            Strings.Character.StatLabelValue.ToString(
                Strings.Combat.Stats[Stat.AbilityPower],
                Globals.Me.Stat[(int)Stat.AbilityPower]
            )
        );

        mDefenseLabel.SetText(
            Strings.Character.StatLabelValue.ToString(
                Strings.Combat.Stats[Stat.Defense],
                Globals.Me.Stat[(int)Stat.Defense]
            )
        );

        mMagicRstLabel.SetText(
            Strings.Character.StatLabelValue.ToString(
                Strings.Combat.Stats[Stat.MagicResist],
                Globals.Me.Stat[(int)Stat.MagicResist]
            )
        );

        mSpeedLabel.SetText(
            Strings.Character.StatLabelValue.ToString(
                Strings.Combat.Stats[Stat.Speed],
                Globals.Me.Stat[(int)Stat.Speed]
            )
        );

        mPointsLabel.SetText(Strings.Character.Points.ToString(Globals.Me.StatPoints));
        mAddAbilityPwrBtn.IsHidden = Globals.Me.StatPoints == 0 ||
                                     Globals.Me.Stat[(int) Stat.AbilityPower] == Options.Instance.Player.MaxStat;

        mAddAttackBtn.IsHidden =
            Globals.Me.StatPoints == 0 || Globals.Me.Stat[(int) Stat.Attack] == Options.Instance.Player.MaxStat;

        mAddDefenseBtn.IsHidden = Globals.Me.StatPoints == 0 ||
                                  Globals.Me.Stat[(int) Stat.Defense] == Options.Instance.Player.MaxStat;

        mAddMagicResistBtn.IsHidden = Globals.Me.StatPoints == 0 ||
                                      Globals.Me.Stat[(int) Stat.MagicResist] == Options.Instance.Player.MaxStat;

        mAddSpeedBtn.IsHidden =
            Globals.Me.StatPoints == 0 || Globals.Me.Stat[(int) Stat.Speed] == Options.Instance.Player.MaxStat;

        UpdateEquippedItems();
    }

    private void UpdateEquippedItems(bool updateExtraBuffs = false)
    {
        if (Globals.Me is not { } player)
        {
            return;
        }

        for (var i = 0; i < Options.Instance.Equipment.Slots.Count; i++)
        {
            var invSlot = player.MyEquipment[i];
            if (invSlot < 0 || invSlot >= Options.Instance.Player.MaxInventory)
            {
                Items[i].Update(Guid.Empty, mItemProperties);
                continue;
            }

            var item = player.Inventory[invSlot];
            if (item.ItemId == Guid.Empty)
            {
                Items[i].Update(Guid.Empty, mItemProperties);
                continue;
            }

            Items[i].Update(item.ItemId, item.ItemProperties);
            if (updateExtraBuffs)
            {
                UpdateExtraBuffs(item.ItemId);
            }
        }
    }

    /// <summary>
    /// Update Extra Buffs Effects like hp/mana regen and items effect types
    /// </summary>
    /// <param name="itemId">Id of item to update extra buffs</param>
    private void UpdateExtraBuffs(Guid itemId)
    {
        var item = ItemDescriptor.Get(itemId);

        if (item == null)
        {
            return;
        }

        //Getting HP and Mana Regen from items
        if (item.VitalsRegen[(int)Vital.Health] != 0)
        {
            HpRegenAmount += item.VitalsRegen[(int)Vital.Health];
        }

        if (item.VitalsRegen[(int)Vital.Mana] != 0)
        {
            ManaRegenAmount += item.VitalsRegen[(int)Vital.Mana];
        }

        //Getting extra buffs from items
        if (item.Effects.Find(effect => effect.Type != ItemEffect.None && effect.Percentage > 0) != default)
        {
            foreach (var effect in item.Effects)
            {
                if (effect.Percentage <= 0)
                {
                    continue;
                }

                switch (effect.Type)
                {
                    case ItemEffect.CooldownReduction:
                        CooldownAmount += effect.Percentage;
                        break;
                    case ItemEffect.Lifesteal:
                        LifeStealAmount += effect.Percentage;
                        break;
                    case ItemEffect.Tenacity:
                        TenacityAmount += effect.Percentage;
                        break;
                    case ItemEffect.Luck:
                        LuckAmount += effect.Percentage;
                        break;
                    case ItemEffect.EXP:
                        ExtraExpAmount += effect.Percentage;
                        break;
                    case ItemEffect.Manasteal:
                        ManaStealAmount += effect.Percentage;
                        break;
                }
            }
        }
    }

    private void UpdateExtraBuffTooltip(Base? sender, EventArgs? arguments)
    {
        //Reset all values
        HpRegenAmount = mPlayer?.VitalRegen[(int)Vital.Health] ?? 0;
        ManaRegenAmount = mPlayer?.VitalRegen[(int)Vital.Mana] ?? 0;
        CooldownAmount = 0;
        LifeStealAmount = 0;
        TenacityAmount = 0;
        LuckAmount = 0;
        ExtraExpAmount = 0;
        ManaStealAmount = 0;

        // Update extra buffs from equipped items
        UpdateEquippedItems(true);

        // No hover tooltip: details are presented in the dedicated window.
    }

    private void OpenDetails()
    {
        RefreshDetails();
        _detailsWindow.IsHidden = false;
        _detailsWindow.BringToFront();
        if (Globals.Me != null)
            PacketSender.SendRequestPlayerProfile(Globals.Me.Id, openWindow: false);
    }

    private void RefreshDetails()
    {
        UpdateExtraBuffTooltip(null, null);
        _detailsScroll.DeleteAll();
        var y = 8;

        void Heading(string title)
        {
            var section = new Label(_detailsScroll, "DetailsSection" + y)
            {
                Text = title.ToUpperInvariant(),
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? _detailsButton.Font,
                FontSize = 13,
                TextColorOverride = new Color(a: 255, r: 224, g: 190, b: 113),
                MouseInputEnabled = false,
            };
            section.SetBounds(16, y, 560, 29);
            y += 32;
        }

        void Row(string caption, string value)
        {
            var background = new DetailsRow(_detailsScroll, "DetailsRow" + y);
            background.SetBounds(12, y, 555, 33);
            var name = new Label(_detailsScroll, "DetailsName" + y)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? _detailsButton.Font,
                FontSize = 11,
                Text = caption,
                TextColorOverride = new Color(a: 255, r: 246, g: 229, b: 202),
                MouseInputEnabled = false,
                TextAlign = Pos.Left | Pos.CenterV,
            };
            name.SetBounds(24, y, 334, 31);
            var stat = new Label(_detailsScroll, "DetailsValue" + y)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? _detailsButton.Font,
                FontSize = 11,
                Text = value,
                TextColorOverride = new Color(a: 255, r: 224, g: 190, b: 113),
                MouseInputEnabled = false,
                TextAlign = Pos.Right | Pos.CenterV,
            };
            stat.SetBounds(363, y, 188, 31);
            y += 37;
        }

        Heading("Regeneration");
        Row("Health Regen", $"{HpRegenAmount}%");
        Row("Mana Regen", $"{ManaRegenAmount}%");
        Row("Lifesteal", $"{LifeStealAmount}%");
        Row("Manasteal", $"{ManaStealAmount}%");

        y += 10;
        Heading("Combat Bonuses");
        Row("Attack Speed", $"{Globals.Me?.CalculateAttackTime() / 1000f:0.###}s");
        Row("Bonus EXP", $"{ExtraExpAmount}%");
        Row("Luck", $"{LuckAmount}%");
        Row("Tenacity", $"{TenacityAmount}%");
        Row("Cooldown Reduction", $"{CooldownAmount}%");

        y += 10;
        Heading("Professions");
        if (_professionDetails.Length == 0)
        {
            Row("Professions", "None learned");
        }
        else
        {
            foreach (var profession in _professionDetails.OrderBy(entry => entry.Name))
            {
                Row(profession.Name,
                    $"Lv {profession.Level}/{profession.MaximumLevel}");
                Row("Experience to next",
                    profession.ExperienceToNextLevel < 0 ? "MAX" :
                        $"{profession.ExperienceToNextLevel:N0} XP");
                if (profession.LocksPicked > 0 || profession.PerfectPicks > 0 ||
                    profession.PicksBroken > 0 || profession.HighestLockDifficulty > 0)
                {
                    Row("Locks picked", $"{profession.LocksPicked:N0}");
                    Row("Perfect / broken",
                        $"{profession.PerfectPicks:N0} / {profession.PicksBroken:N0}");
                    Row("Highest difficulty", $"D{profession.HighestLockDifficulty}");
                    Row("Fastest lockpick",
                        profession.FastestLockpickMilliseconds > 0
                            ? $"{profession.FastestLockpickMilliseconds / 1000d:0.00}s" : "-");
                    Row("Fastest D5",
                        profession.FastestDifficultyFiveLockpickMilliseconds > 0
                            ? $"{profession.FastestDifficultyFiveLockpickMilliseconds / 1000d:0.00}s" : "-");
                }
                y += 8;
            }
        }

        _detailsScroll.SetInnerSize(580, Math.Max(430, y + 14));
        _detailsScroll.UpdateScrollBars();
    }

    private sealed class DetailsRow : Base
    {
        public DetailsRow(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var rect = RenderBounds;
            skin.Renderer.DrawColor = new Color(a: 255, r: 56, g: 35, b: 28);
            skin.Renderer.DrawFilledRect(rect);
            skin.Renderer.DrawColor = new Color(a: 255, r: 109, g: 70, b: 43);
            skin.Renderer.DrawFilledRect(
                new Intersect.Client.Framework.GenericClasses.Rectangle(
                    rect.X, rect.Bottom - 1, rect.Width, 1));
        }
    }

    public void ApplyPlayerProfile(PlayerProfilePacket packet)
    {
        if (!packet.Found || Globals.Me == null || packet.PlayerId != Globals.Me.Id)
            return;

        _professionDetails = packet.Professions ?? [];
        if (!_detailsWindow.IsHidden)
            RefreshDetails();
    }

    /// <summary>
    /// Show the window
    /// </summary>
    public void Show()
    {
        mCharacterWindow.IsHidden = false;

        if (Globals.Me != null)
            PacketSender.SendRequestPlayerProfile(Globals.Me.Id, openWindow: false);
    }

    /// <summary>
    /// </summary>
    /// <returns>Returns if window is visible</returns>
    public bool IsVisible()
    {
        return !mCharacterWindow.IsHidden;
    }

    /// <summary>
    /// Hide the window
    /// </summary>
    public void Hide()
    {
        mCharacterWindow.IsHidden = true;
    }

}
