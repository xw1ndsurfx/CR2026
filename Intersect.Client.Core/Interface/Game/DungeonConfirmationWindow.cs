using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class DungeonConfirmationWindow : Window
{
    private readonly Label _heading;
    private readonly ImagePanel _image;
    private readonly Label _rank;
    private readonly Label _name;
    private readonly Label _description;
    private readonly Label _location;
    private readonly Label _level;
    private readonly Label _recommended;
    private readonly Label _party;
    private readonly Label _time;
    private readonly Label _lives;
    private readonly Label _premium;
    private readonly Label _quest;
    private readonly Label _objectives;
    private readonly Label _rewards;
    private readonly Label _question;
    private readonly Button _enter;
    private readonly Button _cancel;
    private readonly Label _enterLabel;

    private Guid _eventId;
    private Guid _retryId;

    private static readonly Color Gold = new(a: 255, r: 224, g: 190, b: 113);
    private static readonly Color Cream = new(a: 255, r: 246, g: 229, b: 202);
    private static readonly Color Muted = new(a: 255, r: 193, g: 180, b: 161);
    private static readonly Color Good = new(a: 255, r: 154, g: 225, b: 151);

    public DungeonConfirmationWindow(Canvas parent)
        : base(parent, "Dungeon Gate", false, nameof(DungeonConfirmationWindow))
    {
        // Reserve a full footer for both actions: the old 590px window clipped
        // the bottoms of the native buttons on the game's scaled UI.
        SetSize(820, 700);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var frame = new GateChrome(this, "DungeonConfirmFrame", GateChromeStyle.Window);
        frame.SetBounds(14, 29, 792, 643);

        _heading = AddText(this, "DungeonConfirmHeading", "DUNGEON GATE DETECTED",
            145, 46, 530, 31, 18, Gold, bold: true, center: true);
        AddText(this, "DungeonConfirmSubtitle", "ROYAL GATE REGISTRY - REVIEW YOUR DESTINATION",
            125, 80, 570, 20, 9, Muted, center: true);

        var hero = new GateChrome(this, "DungeonConfirmHero", GateChromeStyle.Hero);
        hero.SetBounds(34, 115, 750, 179);

        var preview = new GateChrome(this, "DungeonConfirmPreview", GateChromeStyle.Preview);
        preview.SetBounds(49, 129, 178, 150);

        _image = new ImagePanel(this, "DungeonConfirmImage")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        _image.SetBounds(54, 134, 168, 140);

        _rank = AddText(this, "DungeonConfirmRank", string.Empty,
            721, 130, 45, 43, 23, Cream, bold: true, center: true);
        _name = AddText(this, "DungeonConfirmName", string.Empty,
            248, 135, 458, 32, 16, Cream, bold: true);
        _location = AddText(this, "DungeonConfirmLocation", string.Empty,
            249, 177, 455, 24, 11, Gold, bold: true);
        _description = AddText(this, "DungeonConfirmDescription", string.Empty,
            249, 209, 465, 46, 10, Muted);
        AddText(this, "DungeonConfirmRankCaption", "RANK",
            720, 171, 48, 17, 8, Gold, bold: true, center: true);

        var stats = new GateChrome(this, "DungeonConfirmStatsFrame", GateChromeStyle.Stats);
        stats.SetBounds(34, 306, 750, 105);

        _level = StatValue("DungeonConfirmLevel", "LEVEL", 58, 316);
        _recommended = StatValue("DungeonConfirmRecommended", "RECOMMENDED", 311, 316);
        _party = StatValue("DungeonConfirmParty", "PARTY", 560, 316);
        _time = StatValue("DungeonConfirmTime", "TIME LIMIT", 58, 365);
        _lives = StatValue("DungeonConfirmLives", "LIVES", 311, 365);
        _premium = StatValue("DungeonConfirmPremium", "PREMIUM", 560, 365);

        var questFrame = new GateChrome(this, "DungeonConfirmQuestFrame", GateChromeStyle.InfoRow);
        questFrame.SetBounds(34, 421, 750, 36);
        var objectiveFrame = new GateChrome(this, "DungeonConfirmObjectiveFrame", GateChromeStyle.InfoRow);
        objectiveFrame.SetBounds(34, 463, 750, 36);
        var rewardsFrame = new GateChrome(this, "DungeonConfirmRewardsFrame", GateChromeStyle.InfoRow);
        rewardsFrame.SetBounds(34, 505, 750, 36);

        _quest = InfoValue("DungeonConfirmQuest", "QUEST", 421);
        _objectives = InfoValue("DungeonConfirmObjectives", "OBJECTIVE", 463);
        _rewards = InfoValue("DungeonConfirmRewards", "REWARDS", 505);

        _question = AddText(this, "DungeonConfirmQuestion", "ENTER THIS DUNGEON?",
            158, 554, 504, 31, 15, Gold, bold: true, center: true);

        // Keep real native Button controls and their existing Clicked handlers,
        // but draw them with readable labels and high-contrast pixel-art chrome.
        // Chrome and text children ignore mouse input so the parent remains clickable.
        _enter = new Button(this, "DungeonConfirmEnter") { Text = string.Empty };
        _enter.SetBounds(191, 592, 202, 57);
        var enterFrame = new GateChrome(_enter, "DungeonConfirmEnterFrame", GateChromeStyle.PrimaryButton);
        enterFrame.SetBounds(0, 0, 202, 57);
        _enterLabel = AddText(_enter, "DungeonConfirmEnterLabel", "ENTER DUNGEON",
            11, 10, 180, 36, 15, Cream, bold: true, center: true);
        _enter.Clicked += (_, _) => Respond(accept: true);

        _cancel = new Button(this, "DungeonConfirmCancel") { Text = string.Empty };
        _cancel.SetBounds(427, 592, 202, 57);
        var cancelFrame = new GateChrome(_cancel, "DungeonConfirmCancelFrame", GateChromeStyle.SecondaryButton);
        cancelFrame.SetBounds(0, 0, 202, 57);
        AddText(_cancel, "DungeonConfirmCancelLabel", "CANCEL",
            11, 10, 180, 36, 15, Cream, bold: true, center: true);
        _cancel.Clicked += (_, _) => Respond(accept: false);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    private Label StatValue(string name, string caption, int x, int y)
    {
        AddText(this, name + "Caption", caption,
            x, y, 220, 19, 9, Gold, bold: true);
        return AddText(this, name, string.Empty,
            x, y + 20, 220, 24, 12, Cream, bold: true);
    }

    private Label InfoValue(string name, string caption, int y)
    {
        AddText(this, name + "Caption", caption,
            57, y + 5, 138, 26, 10, Gold, bold: true);
        return AddText(this, name, string.Empty,
            203, y + 5, 557, 26, 10, Cream);
    }

    private Label AddText(
        Base parent,
        string name,
        string text,
        int x,
        int y,
        int width,
        int height,
        int size,
        Color color,
        bool bold = false,
        bool center = false)
    {
        var label = new Label(parent, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                bold ? "sourcesansproblack" : "sourcesanspro") ?? Skin.DefaultFont,
            FontSize = size,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            TextColorOverride = color,
            Text = text,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static string Shorten(string? value, int maximum)
    {
        var clean = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        return clean.Length > maximum
            ? clean[..Math.Max(0, maximum - 3)] + "..."
            : clean;
    }

    public void Apply(DungeonConfirmationPacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);
        var dungeon = DungeonConfiguration.Instance.Find(packet.DungeonId);
        if (dungeon == null)
        {
            Hide();
            return;
        }

        _eventId = packet.EventId;
        _retryId = Guid.Empty;
        _heading.Text = "DUNGEON GATE DETECTED";
        _question.Text = "ENTER THIS DUNGEON?";
        _enterLabel.Text = "ENTER DUNGEON";

        _rank.Text = dungeon.Rank.ToString();
        _rank.TextColorOverride = RankColor(dungeon.Rank);
        _name.Text = Shorten(dungeon.Name, 43);
        _description.Text = Shorten(
            string.IsNullOrWhiteSpace(dungeon.Description) ? "No description provided." : dungeon.Description, 70);
        _location.Text = "LOCATION  " + Shorten(
            string.IsNullOrWhiteSpace(dungeon.Location) ? "Unknown" : dungeon.Location, 52);

        _image.Texture = null;
        _image.Hide();
        if (!string.IsNullOrWhiteSpace(dungeon.Image))
        {
            var manager = Globals.ContentManager;
            var texture =
                manager?.GetTexture(TextureType.Dungeon, dungeon.Image) ??
                manager?.GetTexture(TextureType.Image, dungeon.Image);
            if (texture != null)
            {
                _image.Texture = texture;
                _image.Show();
            }
        }

        _level.Text = dungeon.MaximumLevel > 0
            ? $"{dungeon.MinimumLevel} - {dungeon.MaximumLevel}"
            : $"{dungeon.MinimumLevel}+";
        _recommended.Text = $"{dungeon.RecommendedLevel}+";
        _party.Text = $"{dungeon.MinimumPartySize} - {dungeon.MaximumPartySize}";
        _time.Text = dungeon.TimeLimitMinutes > 0
            ? $"{dungeon.TimeLimitMinutes} MIN"
            : "NONE";
        _lives.Text = Math.Max(1, dungeon.MaxLives).ToString();
        _premium.Text = dungeon.PremiumRequired ? "REQUIRED" : "NOT REQUIRED";
        _premium.TextColorOverride = dungeon.PremiumRequired ? Gold : Good;

        var quest = dungeon.AssociatedQuestId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.AssociatedQuestId);
        var requiredQuest = dungeon.RequiredQuestInProgressId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.RequiredQuestInProgressId);
        var questText = requiredQuest != null
            ? $"{quest?.Name ?? "None"}  /  REQUIRED IN PROGRESS: {requiredQuest.Name}"
            : quest?.Name ?? "None";
        _quest.Text = Shorten(questText, 82);

        var requirements = dungeon.CompletionRequirements == DungeonCompletionRequirement.None
            ? DungeonCompletionRequirement.DefeatFinalBoss
            : dungeon.CompletionRequirements;

        var objectiveParts = new List<string>();
        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            objectiveParts.Add("Defeat Final Boss");
        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            objectiveParts.Add("Defeat All Monsters");

        _objectives.Text = objectiveParts.Count > 0
            ? Shorten(string.Join("  /  ", objectiveParts), 80)
            : "No objectives configured";

        var rewardParts = new List<string>();
        if (dungeon.CompletionExperience > 0)
            rewardParts.Add($"{dungeon.CompletionExperience:N0} EXP");

        var rewardItem = dungeon.CompletionItemId == Guid.Empty
            ? null
            : ItemDescriptor.Get(dungeon.CompletionItemId);
        if (rewardItem != null && dungeon.CompletionItemQuantity > 0)
            rewardParts.Add($"{rewardItem.Name} x{dungeon.CompletionItemQuantity:N0}");

        _rewards.Text = rewardParts.Count > 0
            ? Shorten(string.Join("  /  ", rewardParts), 84)
            : "None configured";

        Show();
        BringToFront();
    }


    public void ApplyRetry(DungeonRetryOfferPacket packet)
    {
        Apply(
            new DungeonConfirmationPacket(
                Guid.Empty,
                packet.DungeonId,
                packet.ConfigurationJson
            )
        );

        _eventId = Guid.Empty;
        _retryId = packet.RetryId;
        _heading.Text = "DUNGEON FAILED";
        _question.Text = "RESTART THIS DUNGEON?";
        _enterLabel.Text = "RETRY DUNGEON";
        Show();
        BringToFront();
    }

    private void Respond(bool accept)
    {
        if (_retryId != Guid.Empty)
        {
            var retryId = _retryId;
            _retryId = Guid.Empty;
            Hide();
            Networking.PacketSender.SendDungeonRetryResponse(retryId, accept);
            return;
        }

        if (_eventId == Guid.Empty)
            return;

        var eventId = _eventId;
        _eventId = Guid.Empty;
        Hide();

        Networking.PacketSender.SendDungeonConfirmationResponse(eventId, accept);
    }

    private static Color RankColor(DungeonRank rank) =>
        rank switch
        {
            DungeonRank.S => new Color(a: 255, r: 228, g: 92, b: 92),
            DungeonRank.A => new Color(a: 255, r: 224, g: 142, b: 68),
            DungeonRank.B => new Color(a: 255, r: 211, g: 185, b: 74),
            DungeonRank.C => new Color(a: 255, r: 111, g: 191, b: 115),
            DungeonRank.D => new Color(a: 255, r: 87, g: 161, b: 207),
            DungeonRank.E => new Color(a: 255, r: 139, g: 139, b: 201),
            _ => new Color(a: 255, r: 175, g: 175, b: 175),
        };


    private enum GateChromeStyle
    {
        Window,
        Hero,
        Preview,
        Stats,
        InfoRow,
        PrimaryButton,
        SecondaryButton,
    }

    /// <summary>
    /// Pixel-art frames are painted instead of relying on special glyphs or
    /// image assets, so every section renders with the current game font.
    /// </summary>
    private sealed class GateChrome : Base
    {
        private static readonly Color Gold = new(a: 255, r: 161, g: 116, b: 55);
        private static readonly Color Bright = new(a: 255, r: 228, g: 183, b: 93);
        private static readonly Color Inner = new(a: 255, r: 99, g: 69, b: 39);
        private static readonly Color Darkest = new(a: 255, r: 26, g: 16, b: 14);
        private static readonly Color Dark = new(a: 255, r: 42, g: 26, b: 21);
        private static readonly Color Brown = new(a: 255, r: 56, g: 35, b: 28);
        private static readonly Color Bronze = new(a: 255, r: 82, g: 48, b: 34);
        private static readonly Color Green = new(a: 255, r: 49, g: 77, b: 42);
        private static readonly Color GreenBorder = new(a: 255, r: 148, g: 183, b: 105);

        private readonly GateChromeStyle _style;

        public GateChrome(Base parent, string name, GateChromeStyle style)
            : base(parent, name)
        {
            _style = style;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var bounds = RenderBounds;
            switch (_style)
            {
                case GateChromeStyle.Window:
                    Fill(skin, bounds, Darkest);
                    Border(skin, bounds, Gold, 2);
                    Border(skin, Inset(bounds, 5), Inner, 1);
                    Fill(skin, bounds.X + 18, bounds.Y + 81, bounds.Width - 36, 1, Inner);
                    Fill(skin, bounds.X + 16, bounds.Bottom - 13, bounds.Width - 32, 2, Inner);
                    Corners(skin, bounds, Bright);
                    break;

                case GateChromeStyle.Hero:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Gold, 2);
                    Border(skin, Inset(bounds, 4), Inner, 1);
                    Fill(skin, bounds.X + 202, bounds.Y + 10, bounds.Width - 222, 41, Bronze);
                    Fill(skin, bounds.X + 202, bounds.Y + 50, bounds.Width - 222, 1, Gold);
                    Fill(skin, bounds.X + 202, bounds.Bottom - 30, bounds.Width - 222, 1, Inner);
                    Fill(skin, bounds.Right - 66, bounds.Y + 10, 52, 45, Darkest);
                    Border(skin, new UiRectangle(bounds.Right - 66, bounds.Y + 10, 52, 45), Inner, 1);
                    Corners(skin, bounds, Bright);
                    break;

                case GateChromeStyle.Preview:
                    Fill(skin, bounds, Darkest);
                    Border(skin, bounds, Bright, 2);
                    Border(skin, Inset(bounds, 4), Inner, 1);
                    // Fallback drawing if the designer didn't assign a dungeon image.
                    Fill(skin, bounds.X + 21, bounds.Y + 23, bounds.Width - 42,
                        bounds.Height - 34, new Color(a: 255, r: 65, g: 57, b: 50));
                    Fill(skin, bounds.X + 37, bounds.Y + 37, bounds.Width - 74,
                        bounds.Height - 48, new Color(a: 255, r: 31, g: 25, b: 24));
                    Fill(skin, bounds.X + 51, bounds.Y + 49, bounds.Width - 102,
                        bounds.Height - 62, Darkest);
                    Fill(skin, bounds.X + 27, bounds.Y + 34, 10, bounds.Height - 59, Gold);
                    Fill(skin, bounds.Right - 37, bounds.Y + 34, 10, bounds.Height - 59, Gold);
                    Fill(skin, bounds.X + 30, bounds.Y + 65, 5, 17, Bright);
                    Fill(skin, bounds.Right - 35, bounds.Y + 65, 5, 17, Bright);
                    break;

                case GateChromeStyle.Stats:
                    Fill(skin, bounds, Brown);
                    Border(skin, bounds, Inner, 1);
                    Fill(skin, bounds.X + 1, bounds.Y + 52, bounds.Width - 2, 1, Inner);
                    Fill(skin, bounds.X + 250, bounds.Y + 10, 1, bounds.Height - 20, Inner);
                    Fill(skin, bounds.X + 500, bounds.Y + 10, 1, bounds.Height - 20, Inner);
                    break;

                case GateChromeStyle.InfoRow:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Inner, 1);
                    Fill(skin, bounds.X + 17, bounds.Y + 10, 7, 12, Gold);
                    Fill(skin, bounds.X + 19, bounds.Y + 13, 3, 6, Bright);
                    Fill(skin, bounds.X + 158, bounds.Y + 6, 1, bounds.Height - 12, Inner);
                    break;

                case GateChromeStyle.PrimaryButton:
                    Fill(skin, bounds, Green);
                    Border(skin, bounds, GreenBorder, 3);
                    Border(skin, Inset(bounds, 5), Inner, 1);
                    Fill(skin, bounds.X + 12, bounds.Y + 7, bounds.Width - 24, 1, GreenBorder);
                    Fill(skin, bounds.X + 12, bounds.Bottom - 8, bounds.Width - 24, 1, Inner);
                    break;

                case GateChromeStyle.SecondaryButton:
                    Fill(skin, bounds, Bronze);
                    Border(skin, bounds, Bright, 3);
                    Border(skin, Inset(bounds, 5), Inner, 1);
                    Fill(skin, bounds.X + 12, bounds.Y + 7, bounds.Width - 24, 1, Bright);
                    Fill(skin, bounds.X + 12, bounds.Bottom - 8, bounds.Width - 24, 1, Inner);
                    break;
            }
        }

        private static UiRectangle Inset(UiRectangle rect, int amount) =>
            new(rect.X + amount, rect.Y + amount,
                rect.Width - amount * 2, rect.Height - amount * 2);

        private static void Fill(SkinBase skin, UiRectangle rect, Color color) =>
            Fill(skin, rect.X, rect.Y, rect.Width, rect.Height, color);

        private static void Fill(
            SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0)
                return;
            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(new UiRectangle(x, y, width, height));
        }

        private static void Border(SkinBase skin, UiRectangle rect, Color color, int thickness)
        {
            Fill(skin, rect.X, rect.Y, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Bottom - thickness, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Y, thickness, rect.Height, color);
            Fill(skin, rect.Right - thickness, rect.Y, thickness, rect.Height, color);
        }

        private static void Corners(SkinBase skin, UiRectangle rect, Color color)
        {
            const int length = 14;
            Fill(skin, rect.X + 5, rect.Y + 5, length, 2, color);
            Fill(skin, rect.X + 5, rect.Y + 5, 2, length, color);
            Fill(skin, rect.Right - length - 5, rect.Y + 5, length, 2, color);
            Fill(skin, rect.Right - 7, rect.Y + 5, 2, length, color);
            Fill(skin, rect.X + 5, rect.Bottom - 7, length, 2, color);
            Fill(skin, rect.X + 5, rect.Bottom - length - 5, 2, length, color);
            Fill(skin, rect.Right - length - 5, rect.Bottom - 7, length, 2, color);
            Fill(skin, rect.Right - 7, rect.Bottom - length - 5, 2, length, color);
        }
    }
}
