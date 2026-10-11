using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
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
    }

    private void BuildCrewLayout()
    {
        var memberCount = Options.Instance.Party.MaximumMembers;
        const int cardHeight = 104;
        const int cardGap = 7;
        const int cardTop = 103;
        var windowHeight = Math.Max(310, cardTop + memberCount * (cardHeight + cardGap) + 70);
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
            Text = "CREW",
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
            role.SetBounds(352, top + 11, 100, 23);
            role.Hide();
            _memberRoles.Add(role);

            mLblnames[i].SetBounds(44, top + 9, 290, 28);
            mLblnames[i].TextColorOverride = Color.White;
            mLblnames[i].Clicked += PartyWindow_Clicked;

            mHpLabel[i].SetBounds(43, top + 36, 48, 19);
            mHpLabel[i].Text = "HP";
            mHpValue[i].SetBounds(310, top + 36, 153, 19);
            mHpValue[i].TextAlign = Pos.Right | Pos.CenterV;
            mMpLabel[i].SetBounds(43, top + 67, 48, 19);
            mMpLabel[i].Text = "MP";
            mMpValue[i].SetBounds(310, top + 67, 153, 19);
            mMpValue[i].TextAlign = Pos.Right | Pos.CenterV;
            foreach (var label in new[] { mHpLabel[i], mHpValue[i], mMpLabel[i], mMpValue[i] })
            {
                label.TextColorOverride = Color.White;
                label.Font = GameContentManager.Current.GetFont("sourcesanspro") ?? mLeaveButton.Font;
                label.FontSize = 9;
            }

            if (i > 0)
            {
                mKickButtons[i].SetBounds(354, top + 11, 99, 25);
                mKickButtons[i].Text = "KICK";
                mKickButtons[i].Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? mLeaveButton.Font;
                mKickButtons[i].FontSize = 9;
                role.SetBounds(304, top + 11, 49, 23);
            }
        }

        mLeader.Hide();
        mLeaderText.Hide();
        mLeader.SetBounds(0, 0, 0, 0);
        mLeaderText.SetBounds(0, 0, 0, 0);
        mLeaveButton.SetBounds(135, windowHeight - 75, 245, 35);
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

    //Methods
    public void Update()
    {
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
        var memberId = (Guid)((Label)sender).UserData;

        if (Globals.Entities.TryGetValue(memberId, out var teammate))
        {
            Globals.Me.TryTarget(teammate);
        }
    }

    public void Show()
    {
        mPartyWindow.IsHidden = false;
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
            Fill(rect.X + 13, rect.Y + 31, rect.Width - 26, 1, copper);
            Fill(rect.X + 13, rect.Y + 58, rect.Width - 26, 12, dark);
            Fill(rect.X + 13, rect.Y + 85, rect.Width - 26, 12, dark);
            Fill(rect.X + 15, rect.Y + 60,
                (int)((rect.Width - 30) * Math.Clamp(HealthRatio, 0f, 1f)), 8,
                new Color(a:255,r:173,g:65,b:63));
            Fill(rect.X + 15, rect.Y + 87,
                (int)((rect.Width - 30) * Math.Clamp(ManaRatio, 0f, 1f)), 8,
                new Color(a:255,r:72,g:120,b:189));
        }
    }

}
