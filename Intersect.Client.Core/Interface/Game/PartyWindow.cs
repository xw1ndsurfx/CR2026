using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Client.Framework.File_Management;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;


public partial class PartyWindow
{

    private List<ImagePanel> mHpBar = new List<ImagePanel>();

    private List<ImagePanel> mHpBarContainer = new List<ImagePanel>();

    private List<Label> mHpLabel = new List<Label>();

    private List<Label> mHpValue = new List<Label>();

    private List<Button> mKickButtons = new List<Button>();

    private List<Label> mLblnames = new List<Label>();

    private readonly List<PartyCardChrome> _memberCards = new();
    private readonly List<Label> _memberRoles = new();
    private Label _partySummary;
    private Label _leaveCaption;
    private PartyRosterHud _rosterHud;
    private readonly List<Label> _hudNames = new();
    private Label _hudHeading;
    private Label _hudExpBonus;
    private readonly List<ImagePanel> _hudPortraits = new();
    private readonly List<Button> _hudKickButtons = new();
    private Button _hudLeaveButton;
    private bool _hudManuallyHidden;

    private ImagePanel mLeader;

    private Label mLeaderText;

    private Button mLeaveButton;

    private List<ImagePanel> mMpBar = new List<ImagePanel>();

    private List<ImagePanel> mMpBarContainer = new List<ImagePanel>();

    private List<Label> mMpLabel = new List<Label>();

    private List<Label> mMpValue = new List<Label>();

    //Controls
    private WindowControl mPartyWindow;

    //Init
    public PartyWindow(Canvas gameCanvas)
    {
        mPartyWindow = new WindowControl(gameCanvas, Strings.Parties.Title, false, "PartyWindow");
        mPartyWindow.DisableResizing();

        //Add the icon representing party leader (ALWAYS member 1 in the party list)
        mLeader = new ImagePanel(mPartyWindow, "LeaderIcon");
        mLeader.SetToolTipText(Strings.Parties.LeaderTip);
        mLeader.Hide();

        mLeaderText = new Label(mPartyWindow, "LeaderText");
        mLeaderText.SetTextColor(new Color(0, 0, 0, 0), ComponentState.Normal);
        mLeaderText.Text = Strings.Parties.Leader;
        mLeaderText.Hide();

        if (Globals.Me.Party.Count > 0)
        {
            // The captain is identified inside the first member card.
        }

        mLblnames.Clear();
        mKickButtons.Clear();
        mHpBarContainer.Clear();
        mHpBar.Clear();

        for (var i = 0; i < Options.Instance.Party.MaximumMembers; i++)
        {
            //Labels
            mLblnames.Add(new Label(mPartyWindow, "MemberName" + i));
            if (i < Globals.Me.Party.Count)
            {
                mLblnames[i].Text = Strings.Parties.Name.ToString(
                    Globals.Me.Party[i].Name, Globals.Me.Party[i].Level
                );
            }
            else
            {
                mLblnames[i].Text = string.Empty;
            }

            //Health bars
            mHpBarContainer.Add(new ImagePanel(mPartyWindow, "HealthBarContainer" + i));
            mHpBarContainer[i].Hide();
            mHpLabel.Add(new Label(mPartyWindow, "HealthLabel" + i));
            mHpLabel[i].Hide();
            mHpLabel[i].SetTextColor(new Color(0, 0, 0, 0), ComponentState.Normal);
            mHpLabel[i].Text = Strings.Parties.Vital0;
            mHpValue.Add(new Label(mPartyWindow, "HealthValue" + i));
            mHpValue[i].Hide();
            mHpValue[i].SetTextColor(new Color(0, 0, 0, 0), ComponentState.Normal);
            if (i < Globals.Me.Party.Count)
            {
                mHpBarContainer[i].Show();
            }

            mHpBar.Add(new ImagePanel(mHpBarContainer[i], "HealthBar" + i));

            //Mana bars
            mMpBarContainer.Add(new ImagePanel(mPartyWindow, "ManaBarContainer" + i));
            mMpBarContainer[i].Hide();
            mMpBarContainer[i].RenderColor = new Color(0, 0, 0, 0);
            mMpLabel.Add(new Label(mPartyWindow, "ManaLabel" + i));
            mMpLabel[i].Hide();
            mMpLabel[i].SetTextColor(new Color(0, 0, 0, 0), ComponentState.Normal);
            mMpLabel[i].Text = Strings.Parties.Vital1;
            mMpValue.Add(new Label(mPartyWindow, "ManaValue" + i));
            mMpValue[i].Hide();
            mMpValue[i].SetTextColor(new Color(0, 0, 0, 0), ComponentState.Normal);
            mMpBar.Add(new ImagePanel(mMpBarContainer[i], "ManaBar" + i));
            mMpBar[i].RenderColor = new Color(0, 0, 0, 0);

            if (i == 0)
            {
                mKickButtons.Add(null);
            }
            else
            {
                mKickButtons.Add(new Button(mPartyWindow, "KickButton" + i));
                mKickButtons[i].Clicked += kick_Clicked;
                mKickButtons[i].Text = Strings.Parties.KickLabel;
                if (i < Globals.Me.Party.Count)
                {
                    mKickButtons[i]
                        .SetToolTipText(
                            Strings.Parties.Kick.ToString(Globals.Entities[Globals.Me.Party[i].Id].Name)
                        );
                }
                else
                {
                    mKickButtons[i].SetToolTipText("");
                }

                mKickButtons[i].Hide();

                //Only show the kick buttons if its you or you are the party leader
                if (i < Globals.Me.Party.Count)
                {
                    if (Globals.Me.Party[0].Id == Globals.Me.Id)
                    {
                        mKickButtons[i].Show();
                    }
                }
            }
        }

        mLeaveButton = new Button(mPartyWindow, "LeavePartyButton");
        mLeaveButton.Text = Strings.Parties.Leave;
        mLeaveButton.SetToolTipText(Strings.Parties.LeaveTip);
        mLeaveButton.Clicked += leave_Clicked;

        mPartyWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());
        BuildCrewLayout();
        BuildPartyHud(gameCanvas);
    }

    private void BuildCrewLayout()
    {
        var memberCount = Options.Instance.Party.MaximumMembers;
        const int cardHeight = 82;
        const int cardGap = 5;
        const int cardTop = 99;
        // Five roster cards fit in a compact window without clipping the footer.
        var windowHeight = Math.Max(310, cardTop + memberCount * (cardHeight + cardGap) + 65);
        mPartyWindow.SetSize(515, windowHeight);
        mPartyWindow.Alignment = [Alignments.Center];

        var outer = new PartyCardChrome(mPartyWindow, "CrewOuter", false);
        outer.SetBounds(13, 29, 489, windowHeight - 52);
        outer.SendToBack();

        var title = new Label(mPartyWindow, "CrewHeading")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
            FontSize = 16,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a:255,r:236,g:210,b:153),
            Text = "PARTY",
            MouseInputEnabled = false,
        };
        title.SetBounds(30, 39, 455, 27);

        _partySummary = new Label(mPartyWindow, "CrewSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? mLeaveButton.Font,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
        };
        _partySummary.SetBounds(30, 70, 455, 23);

        // Member information comes from authoritative party packets. Portraits
        // aren't available in PartyMember, so we use a clear leader/crew badge.
        for (var i = 0; i < memberCount; i++)
        {
            var top = cardTop + i * (cardHeight + cardGap);
            var card = new PartyCardChrome(mPartyWindow, "CrewCard" + i, true);
            card.SetBounds(28, top, 459, cardHeight);
            card.SendToBack();
            _memberCards.Add(card);
            card.Hide();

            var role = new Label(mPartyWindow, "CrewRole" + i)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
                FontSize = 9,
                TextAlign = Pos.Center,
                TextColorOverride = new Color(a:255,r:236,g:210,b:153),
                MouseInputEnabled = false,
            };
            role.SetBounds(352, top + 7, 100, 23);
            role.Hide();
            _memberRoles.Add(role);

            mLblnames[i].SetBounds(44, top + 5, 290, 25);
            mLblnames[i].TextColorOverride = Color.White;
            mLblnames[i].Clicked += PartyWindow_Clicked;

            mHpLabel[i].SetBounds(43, top + 29, 48, 19);
            mHpLabel[i].Text = "HP";
            mHpValue[i].SetBounds(310, top + 29, 153, 19);
            mHpValue[i].TextAlign = Pos.Right | Pos.CenterV;
            mMpLabel[i].SetBounds(43, top + 54, 48, 19);
            mMpLabel[i].Text = "MP";
            mMpValue[i].SetBounds(310, top + 54, 153, 19);
            mMpValue[i].TextAlign = Pos.Right | Pos.CenterV;
            foreach (var label in new[] { mHpLabel[i], mHpValue[i], mMpLabel[i], mMpValue[i] })
            {
                label.TextColorOverride = Color.White;
                label.Font = GameContentManager.Current.GetFont("sourcesanspro") ?? mLeaveButton.Font;
                label.FontSize = 9;
            }

            if (i > 0)
            {
                mKickButtons[i].SetBounds(354, top + 7, 99, 25);
                mKickButtons[i].Text = "KICK";
                mKickButtons[i].Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font;
                mKickButtons[i].FontSize = 9;
                role.SetBounds(304, top + 7, 49, 23);
            }
        }

        mLeader.Hide();
        mLeaderText.Hide();
        mLeader.SetBounds(0, 0, 0, 0);
        mLeaderText.SetBounds(0, 0, 0, 0);
        mLeaveButton.SetBounds(135, windowHeight - 57, 245, 35);
        mLeaveButton.Text = string.Empty;
        _leaveCaption = new Label(mLeaveButton, "CrewLeaveCaption")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            Text = "LEAVE CREW",
            MouseInputEnabled = false,
        };
        _leaveCaption.SetBounds(3, 0, 239, 35);
    }

    private void BuildPartyHud(Canvas canvas)
    {
        // Menu buttons occupy the leftmost ~70 pixels. The roster starts
        // beyond them and never overlays their clickable area.
        const int leftMenuClearance = 86;
        _rosterHud = new PartyRosterHud(canvas, "PartyRosterHud");
        _rosterHud.SetBounds(leftMenuClearance, 12, 258, 48 + Options.Instance.Party.MaximumMembers * 42);
        _rosterHud.Hide();

        _hudHeading = new Label(_rosterHud, "PartyRosterHeading")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
            FontSize = 11,
            Text = "CREW",
            TextColorOverride = new Color(a:255,r:236,g:210,b:153),
            MouseInputEnabled = false,
        };
        _hudHeading.SetBounds(7, 3, 162, 23);
        _hudExpBonus = new Label(_rosterHud, "PartyHudExpBonus")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
            FontSize = 9,
            TextColorOverride = new Color(a:255,r:151,g:235,b:134),
            MouseInputEnabled = false,
        };
        _hudExpBonus.SetBounds(7, 23, 220, 18);

        _hudLeaveButton = new Button(_rosterHud, "PartyHudLeave")
        {
            Text = "LEAVE",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
            FontSize = 9,
        };
        _hudLeaveButton.SetBounds(191, 3, 58, 23);
        _hudLeaveButton.SetToolTipText(Strings.Parties.LeaveTip);
        _hudLeaveButton.Clicked += leave_Clicked;

        for (var i = 0; i < Options.Instance.Party.MaximumMembers; i++)
        {
            var name = new Label(_rosterHud, "PartyHudName" + i)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? mLeaveButton.Font,
                FontSize = 9,
                TextColorOverride = Color.White,
                MouseInputEnabled = true,
            };
            name.SetBounds(43, 43 + i * 42, 166, 18);
            var portrait = new ImagePanel(_rosterHud, "PartyHudPortrait" + i)
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
                MouseInputEnabled = true,
            };
            portrait.SetBounds(10, 42 + i * 42, 25, 25);
            portrait.Clicked += PartyWindow_Clicked;
            portrait.Hide();
            _hudPortraits.Add(portrait);
            name.Clicked += PartyWindow_Clicked;
            name.Hide();
            _hudNames.Add(name);

            var kick = new Button(_rosterHud, "PartyHudKick" + i)
            {
                Text = "KICK",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font,
                FontSize = 8,
            };
            kick.SetBounds(205, 46 + i * 42, 44, 21);
            var memberIndex = i;
            kick.Clicked += (_, _) =>
            {
                if (Globals.Me?.Party.Count > memberIndex &&
                    Globals.Me.Party[0].Id == Globals.Me.Id && memberIndex > 0)
                    PacketSender.SendPartyKick(Globals.Me.Party[memberIndex].Id);
            };
            kick.Hide();
            _hudKickButtons.Add(kick);
        }
    }

    // The left-side Party menu icon toggles this HUD rather than opening the
    // former empty centered roster window. Crew controls live in the HUD.
    public void ToggleHud()
    {
        mPartyWindow.Hide();
        _hudManuallyHidden = !_hudManuallyHidden;
        UpdatePartyHud();
    }

    private void UpdatePartyHud()
    {
        if (Globals.Me == null || Globals.Me.Party.Count == 0 || _hudManuallyHidden)
        {
            _rosterHud.Hide();
            return;
        }

        var count = Math.Min(Globals.Me.Party.Count, _hudNames.Count);
        _rosterHud.SetSize(258, 46 + count * 42);
        _hudLeaveButton.Show();
        _rosterHud.VisibleMembers = count;
        _hudHeading.Text = $"PARTY  {count}/{Options.Instance.Party.MaximumMembers}";
        // Only other nearby party members count and EXP goes to the killer.
        // This is the maximum potential bonus; actual kills require nearby,
        // alive companions, which the server validates authoritatively.
        var maximumBonus = Math.Max(0, count - 1) *
            Math.Max(0f, Options.Instance.Party.BonusExperiencePercentPerMember);
        _hudExpBonus.Text = $"UP TO +{maximumBonus:0.#}% EXP";
        for (var i = 0; i < _hudNames.Count; i++)
        {
            if (i >= count)
            {
                _hudNames[i].Hide();
                _hudPortraits[i].Hide();
                _hudKickButtons[i].Hide();
                continue;
            }

            var member = Globals.Me.Party[i];
            _hudNames[i].Text = $"{(i == 0 ? "[L] " : "")}{member.Name}  Lv. {member.Level}";
            // Sprites are available when the member's entity is loaded locally.
            var portrait = _hudPortraits[i];
            portrait.UserData = member.Id;
            portrait.Hide();
            // The local player may not be present in Globals.Entities even
            // though their own sprite is available via Globals.Me.
            var sprite = member.Id == Globals.Me.Id ? Globals.Me.Sprite :
                Globals.Entities.TryGetValue(member.Id, out var entity) ? entity.Sprite : null;
            if (!string.IsNullOrWhiteSpace(sprite))
            {
                var texture = Globals.ContentManager.GetTexture(TextureType.Entity, sprite);
                if (texture != null)
                {
                    portrait.Texture = texture;
                    // Entity images are sprite sheets, not single 32x32 icons.
                    // Use the same frame layout as the character preview.
                    var frameWidth = Math.Max(1, texture.Width /
                        Math.Max(1, Options.Instance.Sprites.NormalFrames));
                    var frameHeight = Math.Max(1, texture.Height /
                        Math.Max(1, Options.Instance.Sprites.Directions));
                    portrait.SetTextureRect(0, 0, frameWidth, frameHeight);
                    portrait.Show();
                }
            }
            _hudNames[i].UserData = member.Id;
            _hudNames[i].Show();
            if (i > 0 && Globals.Me.Party[0].Id == Globals.Me.Id)
                _hudKickButtons[i].Show();
            else
                _hudKickButtons[i].Hide();
            var hpMax = member.MaxVital[(int)Vital.Health];
            var mpMax = member.MaxVital[(int)Vital.Mana];
            _rosterHud.Health[i] = hpMax > 0
                ? (float)Math.Clamp((double)member.Vital[(int)Vital.Health] / hpMax, 0d, 1d)
                : 0f;
            _rosterHud.Mana[i] = mpMax > 0
                ? (float)Math.Clamp((double)member.Vital[(int)Vital.Mana] / mpMax, 0d, 1d)
                : 0f;
        }

        _rosterHud.Show();
    }

    private sealed class PartyRosterHud : Base
    {
        public int VisibleMembers { get; set; }
        public float[] Health { get; } = new float[Options.Instance.Party.MaximumMembers];
        public float[] Mana { get; } = new float[Options.Instance.Party.MaximumMembers];

        public PartyRosterHud(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = true;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            void Fill(int x, int y, int w, int h, Color color)
            {
                if (w <= 0 || h <= 0) return;
                skin.Renderer.DrawColor = color;
                skin.Renderer.DrawFilledRect(new UiRectangle(x, y, w, h));
            }

            var dark = new Color(a:215,r:26,g:18,b:24);
            var border = new Color(a:255,r:160,g:113,b:57);
            Fill(rect.X, rect.Y, rect.Width, rect.Height, dark);
            Fill(rect.X, rect.Y, rect.Width, 2, border);
            Fill(rect.X, rect.Y, 2, rect.Height, border);
            Fill(rect.Right - 2, rect.Y, 2, rect.Height, border);
            Fill(rect.X, rect.Bottom - 2, rect.Width, 2, border);

            for (var i = 0; i < VisibleMembers; i++)
            {
                var y = rect.Y + 41 + i * 42;
                // Circular avatar substitute: party packets do not contain
                // sprite/portrait data. Never guess another player's sprite.
                Fill(rect.X + 8, y + 3, 27, 27,
                    new Color(a:255,r:91,g:65,b:50));
                Fill(rect.X + 11, y + 6, 21, 21,
                    i == 0 ? border : new Color(a:255,r:73,g:87,b:102));
                Fill(rect.X + 43, y + 20, 155, 7,
                    new Color(a:255,r:18,g:15,b:19));
                Fill(rect.X + 43, y + 20,
                    (int)(155 * Math.Clamp(Health[i], 0f, 1f)), 7,
                    new Color(a:255,r:89,g:189,b:81));
                Fill(rect.X + 43, y + 29, 155, 3,
                    new Color(a:255,r:18,g:15,b:19));
                Fill(rect.X + 43, y + 29,
                    (int)(155 * Math.Clamp(Mana[i], 0f, 1f)), 3,
                    new Color(a:255,r:73,g:129,b:207));
            }
        }
    }

    //Methods
    public void Update()
    {
        UpdatePartyHud();
        if (mPartyWindow.IsHidden)
        {
            return;
        }

        mLeader.Hide();
        mLeaderText.Hide();
        mLeaveButton.Hide();
        _leaveCaption.Hide();
        _partySummary.Text = "NO CREW FORMED";
        for (var i = 0; i < Options.Instance.Party.MaximumMembers; i++)
        {
            mHpBarContainer[i].Hide();
            mHpLabel[i].Hide();
            mHpValue[i].Hide();
            mLblnames[i].Text = string.Empty;
            _memberCards[i].Hide();
            _memberRoles[i].Hide();
            mMpBarContainer[i].Hide();
            mMpLabel[i].Hide();
            mMpValue[i].Hide();
            if (i > 0)
            {
                mKickButtons[i].Hide();
            }
        }

        if (Globals.Me.IsInParty())
        {
            mLeader.Show();
            mLeaderText.Show();
            mLeaveButton.Show();
            _leaveCaption.Show();
            _partySummary.Text = $"{Globals.Me.Party.Count} / {Options.Instance.Party.MaximumMembers} CREW MEMBERS";

            for (var i = 0; i < Options.Instance.Party.MaximumMembers; i++)
            {
                if (i < Globals.Me.Party.Count)
                {
                    mHpLabel[i].Show();
                    mHpValue[i].Show();
                    mHpBarContainer[i].Show();

                    mMpLabel[i].Show();
                    mMpValue[i].Show();
                    mMpBarContainer[i].Show();

                    var nameLabel = mLblnames[i];

                    nameLabel.Text = Strings.Parties.Name.ToString(
                        Globals.Me.Party[i].Name, Globals.Me.Party[i].Level
                    );

                    nameLabel.UserData = Globals.Me.Party[i].Id;
                    // Bound once during construction; updating every frame would
                    // accumulate click handlers and repeat target selection.

                    if (mHpBar[i].Texture != null)
                    {
                        var partyHpWidthRatio = 1f;
                        if (Globals.Me.Party[i].MaxVital[(int)Vital.Health] > 0)
                        {
                            var vitalHp = Globals.Me.Party[i].Vital[(int)Vital.Health];
                            var vitalMaxHp = Globals.Me.Party[i].MaxVital[(int)Vital.Health];
                            var ratioHp = (float)vitalHp / (float)vitalMaxHp;
                            partyHpWidthRatio = Math.Min(1, Math.Max(0, ratioHp));
                        }

                        mHpBar[i]
                            .SetTextureRect(
                                0, 0, Convert.ToInt32(mHpBar[i].Texture.Width * partyHpWidthRatio),
                                mHpBar[i].Texture.Height
                            );

                        mHpBar[i]
                            .SetSize(
                                Convert.ToInt32(partyHpWidthRatio * mHpBarContainer[i].Width), mHpBarContainer[i].Height
                            );
                    }

                    mHpValue[i].Text = Strings.Parties.Vital0Value.ToString(
                        Globals.Me.Party[i].Vital[(int) Vital.Health],
                        Globals.Me.Party[i].MaxVital[(int) Vital.Health]
                    );

                    if (mMpBar[i].Texture != null)
                    {
                        var partyMpWidthRatio = 1f;
                        if (Globals.Me.Party[i].MaxVital[(int)Vital.Mana] > 0)
                        {
                            var vitalMp = Globals.Me.Party[i].Vital[(int)Vital.Mana];
                            var vitalMaxMp = Globals.Me.Party[i].MaxVital[(int)Vital.Mana];
                            var ratioMp = (float)vitalMp / (float)vitalMaxMp;
                            partyMpWidthRatio = Math.Min(1, Math.Max(0, ratioMp));
                        }

                        mMpBar[i]
                            .SetTextureRect(
                                0, 0, Convert.ToInt32(mMpBar[i].Texture.Width * partyMpWidthRatio),
                                mMpBar[i].Texture.Height
                            );

                        mMpBar[i]
                            .SetSize(
                                Convert.ToInt32(partyMpWidthRatio * mMpBarContainer[i].Width), mMpBarContainer[i].Height
                            );
                    }

                    mMpValue[i].Text = Strings.Parties.Vital1Value.ToString(
                        Globals.Me.Party[i].Vital[(int) Vital.Mana],
                        Globals.Me.Party[i].MaxVital[(int) Vital.Mana]
                    );

                    var hpMax = Globals.Me.Party[i].MaxVital[(int)Vital.Health];
                    var mpMax = Globals.Me.Party[i].MaxVital[(int)Vital.Mana];
                    _memberCards[i].HealthRatio = hpMax > 0
                        ? (float)Math.Clamp((double)Globals.Me.Party[i].Vital[(int)Vital.Health] / hpMax, 0d, 1d)
                        : 0f;
                    _memberCards[i].ManaRatio = mpMax > 0
                        ? (float)Math.Clamp((double)Globals.Me.Party[i].Vital[(int)Vital.Mana] / mpMax, 0d, 1d)
                        : 0f;
                    _memberCards[i].Show();
                    _memberRoles[i].Text = i == 0 ? "CAPTAIN" :
                        Globals.Me.Party[i].Id == Globals.Me.Id ? "YOU" : "CREW";
                    _memberRoles[i].Show();
                    // The new crew cards draw the gauges; old texture bars
                    // must not cover the text or the custom pixel-art gauges.
                    mHpBarContainer[i].Hide();
                    mMpBarContainer[i].Hide();

                    if (i > 0)
                    {
                        mKickButtons[i].Hide();
                    }

                    //Only show the kick buttons if its you or you are the party leader
                    if (Globals.Me.Party[0].Id == Globals.Me.Id && i > 0)
                    {
                        mKickButtons[i].Show();
                        mKickButtons[i].SetToolTipText(Strings.Parties.Kick.ToString(Globals.Me.Party[i].Name));
                    }
                }
                else
                {
                    if (i > 0)
                    {
                        mKickButtons[i].SetToolTipText("");
                    }

                    mLblnames[i].Text = string.Empty;
                    mHpBar[i].SetSize(0, mHpBarContainer[i].Height);
                    mHpBarContainer[i].Hide();
                }
            }
        }
    }

    private void PartyWindow_Clicked(Base sender, MouseButtonState arguments)
    {
        // Both member names (Label) and portraits (ImagePanel) call this handler.
        // Read the common Base.UserData without casting the control type.
        if (sender.UserData is not Guid memberId)
            return;

        if (Globals.Me == null || memberId == Guid.Empty)
            return;

        // Fetch character info by ID, even when the member is on another map.
        PacketSender.SendRequestPlayerProfile(memberId, openWindow: true);
    }

    public void Show()
    {
        // Legacy calls must never reopen the centered Party window.
        mPartyWindow.Hide();
        _hudManuallyHidden = false;
        UpdatePartyHud();
    }

    public bool IsVisible()
    {
        return !mPartyWindow.IsHidden;
    }

    public void Hide()
    {
        mPartyWindow.IsHidden = true;
    }

    //Input Handlers
    void kick_Clicked(Base sender, MouseButtonState arguments)
    {
        for (var i = 1; i < Globals.Me.Party.Count; i++)
        {
            if (mKickButtons[i] == sender)
            {
                PacketSender.SendPartyKick(Globals.Me.Party[i].Id);

                return;
            }
        }
    }

    void leave_Clicked(Base sender, MouseButtonState arguments)
    {
        if (Globals.Me.Party.Count > 0)
        {
            PacketSender.SendPartyLeave();
        }
    }

    private sealed class PartyCardChrome : Base
    {
        private readonly bool _member;
        public float HealthRatio { get; set; }
        public float ManaRatio { get; set; }

        public PartyCardChrome(Base parent, string name, bool member) : base(parent, name)
        {
            _member = member;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var rect = RenderBounds;
            var dark = new Color(a:255,r:28,g:18,b:17);
            var copper = new Color(a:255,r:90,g:55,b:37);
            var gold = new Color(a:255,r:162,g:116,b:55);
            var bg = _member ? new Color(a:255,r:56,g:35,b:28) : dark;
            void Fill(int x, int y, int w, int h, Color color)
            {
                if (w <= 0 || h <= 0) return;
                skin.Renderer.DrawColor = color;
                skin.Renderer.DrawFilledRect(new UiRectangle(x,y,w,h));
            }

            Fill(rect.X, rect.Y, rect.Width, rect.Height, bg);
            Fill(rect.X, rect.Y, rect.Width, 2, gold);
            Fill(rect.X, rect.Bottom - 2, rect.Width, 2, gold);
            Fill(rect.X, rect.Y, 2, rect.Height, gold);
            Fill(rect.Right - 2, rect.Y, 2, rect.Height, gold);
            Fill(rect.X + 5, rect.Y + 5, rect.Width - 10, 1, copper);

            if (!_member) return;
            Fill(rect.X + 13, rect.Y + 28, rect.Width - 26, 1, copper);
            Fill(rect.X + 13, rect.Y + 46, rect.Width - 26, 10, dark);
            Fill(rect.X + 13, rect.Y + 67, rect.Width - 26, 10, dark);
            Fill(rect.X + 15, rect.Y + 48,
                (int)((rect.Width - 30) * Math.Clamp(HealthRatio, 0f, 1f)), 8,
                new Color(a:255,r:173,g:65,b:63));
            Fill(rect.X + 15, rect.Y + 69,
                (int)((rect.Width - 30) * Math.Clamp(ManaRatio, 0f, 1f)), 8,
                new Color(a:255,r:72,g:120,b:189));
        }
    }

}
