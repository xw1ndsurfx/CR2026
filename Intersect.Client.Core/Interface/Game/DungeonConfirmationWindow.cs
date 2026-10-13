using Intersect.GameObjects;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Admurin's full-width single-dungeon gate, with a single details registry
/// and the two large action buttons from the mockup. Entry/retry responses
/// preserve the existing packet flow and never bypass server validation.
/// </summary>
internal sealed class DungeonConfirmationWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 223, g: 187, b: 109);
    private static readonly Color Cream = new(a: 255, r: 243, g: 229, b: 196);
    private static readonly Color Good = new(a: 255, r: 132, g: 225, b: 149);

    private sealed class GateDataFrame : Base
    {
        public GateDataFrame(Base parent) : base(parent, "DungeonGateDataFrame")
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            skin.Renderer.DrawColor = new Color(a: 255, r: 168, g: 137, b: 77);
            skin.Renderer.DrawFilledRect(rect);
            skin.Renderer.DrawColor = new Color(a: 255, r: 12, g: 18, b: 9);
            skin.Renderer.DrawFilledRect(new UiRectangle(rect.X + 3, rect.Y + 3,
                rect.Width - 6, rect.Height - 6));
            skin.Renderer.DrawColor = new Color(a: 255, r: 118, g: 89, b: 49);
            skin.Renderer.DrawFilledRect(new UiRectangle(rect.X + 184, rect.Y + 11,
                1, rect.Height - 22));
        }
    }

    private sealed class GateActionFrame : Base
    {
        private readonly bool _primary;

        public GateActionFrame(Base parent, string name, bool primary) : base(parent, name)
        {
            _primary = primary;
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            skin.Renderer.DrawColor = new Color(a: 255, r: 19, g: 16, b: 12);
            skin.Renderer.DrawFilledRect(rect);
            skin.Renderer.DrawColor = _primary
                ? new Color(a: 255, r: 42, g: 142, b: 56)
                : new Color(a: 255, r: 103, g: 71, b: 56);
            skin.Renderer.DrawFilledRect(new UiRectangle(
                rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6));
            skin.Renderer.DrawColor = _primary
                ? new Color(a: 255, r: 103, g: 186, b: 110)
                : new Color(a: 255, r: 160, g: 117, b: 83);
            skin.Renderer.DrawFilledRect(new UiRectangle(
                rect.X + 5, rect.Y + 4, rect.Width - 10, 2));
        }
    }

    private readonly Label _heading;
    private readonly Label _name;
    private readonly Label _location;
    private readonly Label _availability;
    private readonly Label _rank;
    private readonly Label _quest;
    private readonly Label _level;
    private readonly Label _party;
    private readonly Label _lives;
    private readonly Label _objectives;
    private readonly Label _rewards;
    private readonly Label _extra;
    private readonly ImagePanel _icon;
    private readonly Button _enter;
    private readonly Button _cancel;
    private readonly Label _enterCaption;

    private Guid _eventId;
    private Guid _retryId;

    public DungeonConfirmationWindow(Canvas parent)
        : base(parent, "Dungeon Gate", false, nameof(DungeonConfirmationWindow))
    {
        SetSize(506, 396);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        // The native Window supplies the only outer frame and title bar.

        AdmurinGuiArt.Add(this, "DungeonGateHeaderIcon", "dungeon_gates_1",
            236, 4, 32, 32, 0, 0, 32, 32);

        AddText(this, "DungeonGateTitle", "DUNGEON", 40, 36, 426, 25, 23, Gold, center: true);
        _heading = AddText(this, "DungeonGateHeading", "DUNGEON GATE DETECTED",
            40, 61, 426, 16, 11, Gold, center: true);
        AddText(this, "DungeonGateSubtitle", "ROYAL GATE REGISTRY - REVIEW YOUR DESTINATION",
            20, 77, 466, 14, 9, Cream, center: true);

        var detailsFrame = new GateDataFrame(this);
        detailsFrame.SetBounds(18, 93, 470, 173);

        string[] captions =
        [
            "NAME", "LOCATION", "STATUS", "TIER", "QUEST REQUIRED",
            "LEVEL", "PARTY", "LIVES", "OBJECTIVE", "REWARDS",
        ];
        for (var i = 0; i < captions.Length; i++)
        {
            AddText(this, $"DungeonGateLabel{i}", captions[i],
                26, 99 + i * 16, 174, 16, 10, Gold);
        }
        _name = ValueText("Name", 0);
        _location = ValueText("Location", 1);
        _availability = ValueText("Status", 2);
        _rank = ValueText("Rank", 3);
        _quest = ValueText("Quest", 4);
        _level = ValueText("Level", 5);
        _party = ValueText("Party", 6);
        _lives = ValueText("Lives", 7);
        _objectives = ValueText("Objective", 8);
        _rewards = ValueText("Rewards", 9);
        _extra = AddText(this, "DungeonGateExtra", string.Empty,
            20, 268, 466, 15, 9, Cream);

        _icon = new ImagePanel(this, "DungeonCustomIcon")
        {
            MaintainAspectRatio = true,
            ShouldDrawBackground = false,
            MouseInputEnabled = false,
        };
        _icon.SetBounds(236, 4, 32, 32);
        _icon.Hide();

        _enter = new Button(this, "EnterDungeon") { Text = string.Empty };
        _enter.SetBounds(18, 289, 470, 32);
        var enterFrame = new GateActionFrame(_enter, "EnterDungeonArt", primary: true);
        enterFrame.SetBounds(0, 0, 470, 32);
        AdmurinGuiArt.Add(_enter, "DungeonEnterBadge", "dungeon_gates_5",
            13, 7, 19, 19, 0, 0, 12, 12);
        _enterCaption = AddText(_enter, "EnterCaption", "ENTER DUNGEON",
            10, 2, 450, 28, 11, Cream, center: true);
        _enter.Clicked += (_, _) => Respond(accept: true);

        _cancel = new Button(this, "CancelDungeon") { Text = string.Empty };
        _cancel.SetBounds(18, 326, 470, 32);
        var cancelFrame = new GateActionFrame(_cancel, "CancelDungeonArt", primary: false);
        cancelFrame.SetBounds(0, 0, 470, 32);
        AdmurinGuiArt.Add(_cancel, "DungeonCancelBadge", "dungeon_gates_5",
            13, 7, 19, 19, 12, 0, 12, 12);
        AddText(_cancel, "CancelCaption", "CANCEL",
            10, 2, 450, 28, 11, Cream, center: true);
        _cancel.Clicked += (_, _) => Respond(accept: false);

        Hide();
    }

    protected override void EnsureInitialized() { }

    private Label ValueText(string name, int row) =>
        AddText(this, $"DungeonGate{name}Value", string.Empty,
            208, 99 + row * 16, 274, 16, 10, Cream);

    private static Label AddText(
        Base parent, string id, string text, int x, int y, int width, int height,
        int size, Color color, bool center = false)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("empire7") ??
                   GameContentManager.Current.GetFont("sourcesansproblack") ?? parent.Skin.DefaultFont,
            Text = text,
            FontSize = size,
            TextColorOverride = color,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static string Shorten(string? text, int maximum)
    {
        var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= maximum ? value : value[..Math.Max(0, maximum - 3)] + "...";
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
        _enterCaption.Text = "ENTER DUNGEON";

        _name.Text = Shorten(dungeon.Name, 49);
        _location.Text = Shorten(string.IsNullOrWhiteSpace(dungeon.Location) ? "UNKNOWN" : dungeon.Location, 49);
        _availability.Text = "AVAILABLE";
        _availability.TextColorOverride = Good;
        _rank.Text = dungeon.Rank.ToString();
        _rank.TextColorOverride = RankColor(dungeon.Rank);

        _icon.Texture = null;
        _icon.Hide();
        if (!string.IsNullOrWhiteSpace(dungeon.Image))
        {
            var texture = Globals.ContentManager?.GetTexture(TextureType.Dungeon, dungeon.Image) ??
                          Globals.ContentManager?.GetTexture(TextureType.Image, dungeon.Image);
            if (texture != null)
            {
                _icon.Texture = texture;
                _icon.Show();
            }
        }

        var quest = dungeon.AssociatedQuestId == Guid.Empty
            ? null : QuestDescriptor.Get(dungeon.AssociatedQuestId);
        var requiredQuest = dungeon.RequiredQuestInProgressId == Guid.Empty
            ? null : QuestDescriptor.Get(dungeon.RequiredQuestInProgressId);
        _quest.Text = Shorten(requiredQuest?.Name ?? quest?.Name ?? "NONE", 49);

        var levelRange = dungeon.MaximumLevel > 0
            ? $"{dungeon.MinimumLevel}-{dungeon.MaximumLevel}"
            : $"{dungeon.MinimumLevel}+";
        _level.Text = $"{levelRange} / RECOMMENDED {dungeon.RecommendedLevel}+";
        _party.Text = $"{dungeon.MinimumPartySize}-{dungeon.MaximumPartySize}";
        _lives.Text = Math.Max(1, dungeon.MaxLives).ToString();

        var requirements = dungeon.CompletionRequirements == DungeonCompletionRequirement.None
            ? DungeonCompletionRequirement.DefeatFinalBoss : dungeon.CompletionRequirements;
        var objectives = new List<string>();
        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            objectives.Add("Final Boss");
        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            objectives.Add("All Monsters");
        _objectives.Text = objectives.Count > 0 ? string.Join(" / ", objectives) : "NONE";

        var rewards = new List<string>();
        if (dungeon.CompletionExperience > 0)
            rewards.Add($"{dungeon.CompletionExperience:N0} EXP");
        var rewardItem = dungeon.CompletionItemId == Guid.Empty
            ? null : ItemDescriptor.Get(dungeon.CompletionItemId);
        if (rewardItem != null && dungeon.CompletionItemQuantity > 0)
            rewards.Add($"{rewardItem.Name} x{dungeon.CompletionItemQuantity:N0}");
        _rewards.Text = Shorten(rewards.Count > 0 ? string.Join(" / ", rewards) : "NONE", 51);

        var extras = new List<string>();
        if (dungeon.TimeLimitMinutes > 0)
            extras.Add($"TIME {dungeon.TimeLimitMinutes} MIN");
        if (dungeon.PremiumRequired)
            extras.Add("PREMIUM REQUIRED");
        if (!string.IsNullOrWhiteSpace(dungeon.Description))
            extras.Add(dungeon.Description);
        _extra.Text = Shorten(string.Join("   /   ", extras), 92);

        Show();
        BringToFront();
    }

    public void ApplyRetry(DungeonRetryOfferPacket packet)
    {
        Apply(new DungeonConfirmationPacket(Guid.Empty, packet.DungeonId, packet.ConfigurationJson));
        _eventId = Guid.Empty;
        _retryId = packet.RetryId;
        _heading.Text = "DUNGEON FAILED";
        _enterCaption.Text = "RETRY DUNGEON";
        _availability.Text = "RETRY AVAILABLE";
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

    private static Color RankColor(DungeonRank rank) => rank switch
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
