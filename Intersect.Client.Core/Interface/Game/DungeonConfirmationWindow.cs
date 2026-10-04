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

    private Guid _eventId;
    private Guid _retryId;

    public DungeonConfirmationWindow(Canvas parent)
        : base(parent, "Dungeon Gate", false, nameof(DungeonConfirmationWindow))
    {
        SetSize(820, 590);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        _heading = new Label(this, "DungeonConfirmHeading")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
            Text = "DUNGEON GATE DETECTED",
        };
        _heading.SetBounds(20, 34, 780, 24);

        _image = new ImagePanel(this, "DungeonConfirmImage")
        {
            MaintainAspectRatio = true,
            MouseInputEnabled = false,
        };
        _image.SetBounds(30, 75, 300, 180);

        _rank = new Label(this, "DungeonConfirmRank")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 58,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _rank.SetBounds(675, 70, 105, 100);

        _name = new Label(this, "DungeonConfirmName")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 19,
            TextColorOverride = Color.White,
        };
        _name.SetBounds(355, 82, 305, 32);

        _description = new Label(this, "DungeonConfirmDescription")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 215, g: 211, b: 203),
        };
        _description.SetBounds(355, 120, 305, 78);

        _location = new Label(this, "DungeonConfirmLocation")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 8,
            TextColorOverride = new Color(a: 255, r: 185, g: 181, b: 173),
        };
        _location.SetBounds(355, 204, 305, 22);

        _level = InfoLabel("DungeonConfirmLevel", 32, 280, 235);
        _recommended = InfoLabel("DungeonConfirmRecommended", 290, 280, 235);
        _party = InfoLabel("DungeonConfirmParty", 548, 280, 235);

        _time = InfoLabel("DungeonConfirmTime", 32, 318, 235);
        _lives = InfoLabel("DungeonConfirmLives", 290, 318, 235);
        _premium = InfoLabel("DungeonConfirmPremium", 548, 318, 235);

        _quest = SectionLabel("DungeonConfirmQuest", 32, 364, 746, 24);
        _objectives = SectionLabel("DungeonConfirmObjectives", 32, 398, 746, 44);
        _rewards = SectionLabel("DungeonConfirmRewards", 32, 452, 746, 44);

        _question = new Label(this, "DungeonConfirmQuestion")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
            Text = "ENTER THIS DUNGEON?",
        };
        _question.SetBounds(230, 505, 360, 24);

        _enter = new Button(this, "DungeonConfirmEnter")
        {
            Text = "ENTER",
            FontSize = 11,
        };
        _enter.SetBounds(250, 535, 145, 36);
        _enter.Clicked += (_, _) => Respond(accept: true);

        _cancel = new Button(this, "DungeonConfirmCancel")
        {
            Text = "CANCEL",
            FontSize = 11,
        };
        _cancel.SetBounds(425, 535, 145, 36);
        _cancel.Clicked += (_, _) => Respond(accept: false);

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    private Label InfoLabel(string name, int x, int y, int width)
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        label.SetBounds(x, y, width, 26);
        return label;
    }

    private Label SectionLabel(string name, int x, int y, int width, int height)
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 9,
            TextAlign = Pos.Left | Pos.CenterV,
            TextColorOverride = new Color(a: 255, r: 220, g: 196, b: 135),
        };
        label.SetBounds(x, y, width, height);
        return label;
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
        _enter.Text = "ENTER";

        _rank.Text = dungeon.Rank.ToString();
        _rank.TextColorOverride = RankColor(dungeon.Rank);
        _name.Text = dungeon.Name;
        _description.Text = dungeon.Description;
        _location.Text = string.IsNullOrWhiteSpace(dungeon.Location)
            ? "LOCATION • Unknown"
            : $"LOCATION • {dungeon.Location}";

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

        var maximum = dungeon.MaximumLevel > 0
            ? dungeon.MaximumLevel.ToString()
            : "∞";

        _level.Text = $"LEVEL  {dungeon.MinimumLevel} - {maximum}";
        _recommended.Text = $"RECOMMENDED  {dungeon.RecommendedLevel}+";
        _party.Text = $"PARTY  {dungeon.MinimumPartySize} - {dungeon.MaximumPartySize}";

        _time.Text = dungeon.TimeLimitMinutes > 0
            ? $"TIME LIMIT  {dungeon.TimeLimitMinutes} MIN"
            : "TIME LIMIT  NONE";
        _lives.Text = $"LIVES  {Math.Max(1, dungeon.MaxLives)}";
        _premium.Text = dungeon.PremiumRequired
            ? "PREMIUM  REQUIRED"
            : "PREMIUM  NOT REQUIRED";
        _premium.TextColorOverride = dungeon.PremiumRequired
            ? new Color(a: 255, r: 230, g: 184, b: 70)
            : new Color(a: 255, r: 170, g: 170, b: 170);

        var quest = dungeon.AssociatedQuestId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.AssociatedQuestId);
        var requiredQuest = dungeon.RequiredQuestInProgressId == Guid.Empty
            ? null
            : QuestDescriptor.Get(dungeon.RequiredQuestInProgressId);
        _quest.Text = requiredQuest != null
            ? $"QUEST • {quest?.Name ?? "None"}   •   REQUIRED IN PROGRESS • {requiredQuest.Name}"
            : quest == null
                ? "QUEST • None"
                : $"QUEST • {quest.Name}";

        var requirements = dungeon.CompletionRequirements == DungeonCompletionRequirement.None
            ? DungeonCompletionRequirement.DefeatFinalBoss
            : dungeon.CompletionRequirements;

        var objectiveParts = new List<string>();
        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            objectiveParts.Add("Defeat Final Boss");
        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            objectiveParts.Add("Defeat All Monsters");

        _objectives.Text = $"OBJECTIVE • {string.Join("  +  ", objectiveParts)}";

        var rewardParts = new List<string>();
        if (dungeon.CompletionExperience > 0)
            rewardParts.Add($"{dungeon.CompletionExperience:N0} EXP");

        var rewardItem = dungeon.CompletionItemId == Guid.Empty
            ? null
            : ItemDescriptor.Get(dungeon.CompletionItemId);
        if (rewardItem != null && dungeon.CompletionItemQuantity > 0)
            rewardParts.Add($"{rewardItem.Name} x{dungeon.CompletionItemQuantity:N0}");

        _rewards.Text = rewardParts.Count > 0
            ? $"REWARDS • {string.Join("  •  ", rewardParts)}"
            : "REWARDS • None configured";

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
        _enter.Text = "RETRY";
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
}
