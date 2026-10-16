using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class PetsWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 224, g: 190, b: 113);
    private static readonly Color Cream = new(a: 255, r: 246, g: 229, b: 202);
    private static readonly Color Muted = new(a: 255, r: 193, g: 180, b: 161);
    private readonly Label _summary;
    private readonly bool _readOnly;
    private readonly ScrollControl _scroll;

    public PetsWindow(Canvas parent, bool readOnly = false) : base(parent,
        readOnly ? "Player Familiars" : "Pets", false,
        readOnly ? "InspectedPetsWindow" : nameof(PetsWindow))
    {
        _readOnly = readOnly;
        SetSize(735, 560);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var frame = new PetChrome(this, "PetsWindowFrame", PetChromeStyle.Window);
        frame.SetBounds(14, 30, 707, 500);

        var title = new Label(this, "PetsTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 17,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = readOnly ? "PLAYER FAMILIARS" : "PETS",
        };
        title.SetBounds(20, 39, 695, 32);

        _summary = new Label(this, "PetsSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _summary.SetBounds(20, 74, 695, 23);
        _summary.TextColorOverride = Muted;

        _scroll = new ScrollControl(this, "PetsScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(31, 108, 673, 403);
        Hide();
    }

    protected override void EnsureInitialized() { }

    public void Apply(PetStatePacket packet)
    {
        _scroll.DeleteAll();
        var pets = packet.Pets ?? [];
        _summary.Text = (_readOnly ? packet.InspectedPlayerName + "  |  VIEW ONLY  |  " : "") +
            (pets.Length == 0
                ? (_readOnly ? "No familiars" : "No pets unlocked. Use a pet summoning item.")
                : pets.Length + " familiars  -  " +
                  (pets.Any(p => p.IsActive) ? "1 summoned" : "none summoned"));

        var y = 10;
        foreach (var pet in pets)
        {
            // Every pet has an independent, compact card, including its own actions.
            var card = new Base(_scroll, "PetCard" + pet.PetId)
            {
                MouseInputEnabled = false,
            };
            card.SetBounds(5, y, 644, _readOnly ? 312 : 378);
            var cardFrame = new PetChrome(card, "PetCardFrame" + pet.PetId, PetChromeStyle.Card);
            cardFrame.SetBounds(0, 0, 644, _readOnly ? 312 : 378);
            var portraitFrame = new PetChrome(card, "PetPortraitFrame" + pet.PetId, PetChromeStyle.Portrait);
            portraitFrame.SetBounds(13, 12, 85, 85);
            var statsFrame = new PetChrome(card, "PetStatsFrame" + pet.PetId, PetChromeStyle.Section);
            statsFrame.SetBounds(12, 150, 620, 60);
            var abilitiesFrame = new PetChrome(card, "PetAbilitiesFrame" + pet.PetId, PetChromeStyle.Section);
            abilitiesFrame.SetBounds(12, 215, 620, 53);
            var optionsFrame = new PetChrome(card, "PetOptionsFrame" + pet.PetId, PetChromeStyle.Section);
            optionsFrame.SetBounds(12, 272, 620, 25);

            var portrait = new ImagePanel(card, "PetSprite" + pet.PetId)
            {
                MaintainAspectRatio = true,
                MouseInputEnabled = false,
            };
            portrait.SetBounds(18, 17, 76, 76);
            if (!string.IsNullOrWhiteSpace(pet.Sprite))
            {
                portrait.Texture = Globals.ContentManager.GetTexture(TextureType.Entity, pet.Sprite);
                if (portrait.Texture != null)
                {
                    // Entity textures are sheets of frames x directions, not portraits.
                    // Match the first facing-down frame used in character selection.
                    var frameWidth = portrait.Texture.Width / Math.Max(1, Options.Instance.Sprites.NormalFrames);
                    var frameHeight = portrait.Texture.Height / Math.Max(1, Options.Instance.Sprites.Directions);
                    if (frameWidth > 0 && frameHeight > 0)
                        portrait.SetTextureRect(0, 0, frameWidth, frameHeight);
                    else
                        portrait.Hide();
                }
                else portrait.Hide();
            }
            else portrait.Hide();

            var displayTitle = pet.Name +
                (string.IsNullOrWhiteSpace(pet.SpeciesName) || pet.Name == pet.SpeciesName
                    ? string.Empty : " (" + pet.SpeciesName + ")");
            AddText(card, "PetName" + pet.PetId, displayTitle, 108, 13, 400, 26, 14, true,
                new Color(a: 255, r: 255, g: 229, b: 177));
            AddText(card, "PetStatus" + pet.PetId,
                pet.IsActive ? "[ SUMMONED ]" : "[ DISMISSED ]", 510, 14, 118, 25, 10, true,
                pet.IsActive ? new Color(a: 255, r: 112, g: 231, b: 135)
                    : new Color(a: 255, r: 189, g: 177, b: 166));
            AddText(card, "PetLevel" + pet.PetId, "Level " + pet.Level + " / " + pet.MaximumLevel,
                108, 40, 290, 20, 11);

            // Native progress bars share the game's skin and adapt to any pet values.
            AddBar(card, "PetHp" + pet.PetId, "HP", pet.Health, pet.MaximumHealth, 108, 65, 245);
            AddBar(card, "PetMp" + pet.PetId, "MP", pet.Mana, pet.MaximumMana, 366, 65, 245);
            AddBar(card, "PetXp" + pet.PetId, "EXP", pet.Experience,
                pet.ExperienceToNextLevel, 108, 108, 503);

            AddText(card, "PetStatsTitle" + pet.PetId, "STATS", 22, 151, 600, 22, 11,
                true, new Color(a: 255, r: 235, g: 201, b: 151));
            var labels = new[] { "ATK", "DEF", "SPD", "MAG", "RES" };
            var stats = pet.Stats ?? [];
            for (var i = 0; i < labels.Length; i++)
            {
                var value = i < stats.Length ? stats[i] : 0;
                AddText(card, "PetStat" + i + pet.PetId, labels[i] + "   " + value,
                    22 + i * 122, 179, 110, 24, 10, true);
            }

            AddText(card, "PetAbilities" + pet.PetId, "SPELLS", 22, 217, 600, 19, 11, true,
                new Color(a: 255, r: 235, g: 201, b: 151));
            var abilities = pet.Abilities ?? [];
            var abilityText = abilities.Length == 0 ? "No spells configured" :
                string.Join("  |  ", abilities.Take(5).Select(spell =>
                    spell.Name + (pet.Level >= spell.RequiredLevel ? "" :
                        " [Lv. " + spell.RequiredLevel + "]")));
            AddText(card, "PetAbilityNames" + pet.PetId, abilityText, 22, 241, 602, 22, 10);

            AddText(card, "PetLootStatus" + pet.PetId,
                "AUTO-LOOT: " + (pet.AutoLoot ? "ON" : "OFF") +
                "      Radius: " + pet.LootRadius + " tiles",
                22, 274, 602, 20, 10, true,
                pet.AutoLoot ? new Color(a: 255, r: 147, g: 225, b: 161) : Color.White);

            // Observed familiars are informational only; never create
            // rename, summon, dismiss, or auto-loot actions for another player.
            if (!_readOnly)
            {
                AddText(card, "PetRenameLabel" + pet.PetId, "Name:", 18, 301, 53, 25, 10);
                var nameInput = new TextBox(card, "PetNameInput" + pet.PetId)
                {
                    Text = pet.Name,
                    PlaceholderText = "Pet name",
                    Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                    FontSize = 10,
                    TextColorOverride = Color.White,
                    TextAlign = Pos.Left | Pos.CenterV,
                };
                nameInput.SetBounds(70, 299, 316, 29);
                nameInput.SetMaxLength(24);
    
                void SubmitName() => Networking.PacketSender.SendPetAction(
                    PetActionKind.Rename, pet.PetId, nameInput.Text);
                nameInput.SubmitPressed += (_, _) => SubmitName();
    
                var rename = new Button(card, "PetRename" + pet.PetId)
                {
                    Text = Intersect.Client.Localization.ClientLanguages.Text("Rename"),
                    Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                    FontSize = 10,
                };
                rename.SetBounds(400, 299, 226, 29);
                StyleButton(rename, "PetRenameStyle" + pet.PetId, "RENAME", 226, 29, false);
                rename.Clicked += (_, _) => SubmitName();
    
                var summon = new Button(card, "PetSummon" + pet.PetId)
                {
                    Text = pet.IsActive ? "Dismiss" : "Summon",
                    Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                    FontSize = 10,
                };
                summon.SetBounds(18, 337, 296, 32);
                StyleButton(summon, "PetSummonStyle" + pet.PetId,
                    pet.IsActive ? "DISMISS" : "SUMMON", 296, 32, !pet.IsActive);
                summon.Clicked += (_, _) => Networking.PacketSender.SendPetAction(
                    pet.IsActive ? PetActionKind.Dismiss : PetActionKind.Summon, pet.PetId);
    
                var loot = new Button(card, "PetLoot" + pet.PetId)
                {
                    Text = pet.AutoLoot ? "Disable auto-loot" : "Enable auto-loot",
                    Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                    FontSize = 10,
                    IsDisabled = !pet.IsActive,
                };
                loot.SetBounds(330, 337, 296, 32);
                StyleButton(loot, "PetLootStyle" + pet.PetId,
                    pet.AutoLoot ? "DISABLE AUTO-LOOT" : "ENABLE AUTO-LOOT",
                    296, 32, pet.IsActive && !pet.AutoLoot);
                loot.Clicked += (_, _) => Networking.PacketSender.SendPetAction(
                    PetActionKind.ToggleAutoLoot, pet.PetId);
    
    
            }

            y += _readOnly ? 324 : 392;
        }
        _scroll.SetInnerSize(661, Math.Max(400, y + 10));
        _scroll.UpdateScrollBars();
    }

    private static void StyleButton(Button button, string id, string caption,
        int width, int height, bool primary)
    {
        button.Text = string.Empty;
        var frame = new PetChrome(button, id,
            primary ? PetChromeStyle.PrimaryButton : PetChromeStyle.SecondaryButton);
        frame.SetBounds(0, 0, width, height);
        var label = new Label(button, id + "Text")
        {
            AutoSizeToContents = false,
            // Match CharacterWindow's explicit font workaround: Empire bitmap
            // fallback can collapse a label's glyph geometry to 1-2 letters.
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? button.Font ?? button.Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Cream,
            Text = caption,
            MouseInputEnabled = false,
        };
        label.SetBounds(6, 0, width - 12, height);
        label.BringToFront();
    }

    private void AddBar(Base parent, string id, string title, long current, long maximum,
        int x, int y, int width)
    {
        var capped = maximum < 0;
        var ratio = capped ? 1f : maximum == 0 ? 0f :
            (float)Math.Clamp((double)current / maximum, 0d, 1d);
        var value = capped ? "MAX" : current + " / " + maximum;
        AddText(parent, id + "Label", title + "  " + value,
            x, y, width, 18, 10, true, Gold);
        var fillColor = title == "HP"
            ? new Color(a: 255, r: 167, g: 66, b: 63)
            : title == "MP" ? new Color(a: 255, r: 71, g: 116, b: 189)
            : new Color(a: 255, r: 82, g: 159, b: 91);
        var bar = new PetChrome(parent, id + "Bar", PetChromeStyle.Bar)
        {
            Progress = ratio,
            ProgressColor = fillColor,
        };
        bar.SetBounds(x, y + 19, width, 13);
        AddText(parent, id + "Percent", capped ? "MAX" :
            (int)Math.Round(ratio * 100) + "%", x, y + 18, width, 16, 9,
            false, Cream).TextAlign = Pos.Center;
    }

    private Label AddText(Base parent, string id, string value, int x, int y,
        int width, int height, int fontSize, bool heading = false, Color? color = null)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                heading ? "sourcesansproblack" : "sourcesanspro") ?? Skin.DefaultFont,
            FontSize = fontSize,
            TextColorOverride = color ?? Color.White,
            Text = value,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }
    private enum PetChromeStyle
    {
        Window, Card, Portrait, Section, Bar, PrimaryButton, SecondaryButton
    }

    // Draw the same dark copper/gold pixel-art frames used by Dungeon Gate.
    private sealed class PetChrome : Base
    {
        private static readonly Color Dark = new(a: 255, r: 28, g: 18, b: 17);
        private static readonly Color Brown = new(a: 255, r: 56, g: 35, b: 28);
        private static readonly Color Copper = new(a: 255, r: 90, g: 55, b: 37);
        private static readonly Color BorderColor = new(a: 255, r: 162, g: 116, b: 55);
        private static readonly Color Highlight = new(a: 255, r: 228, g: 183, b: 93);
        private readonly PetChromeStyle _style;

        public float Progress { get; set; }
        public Color ProgressColor { get; set; } = Color.White;

        public PetChrome(Base parent, string name, PetChromeStyle style) : base(parent, name)
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
                case PetChromeStyle.Window:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, BorderColor, 2);
                    Border(skin, Inset(bounds, 5), Copper, 1);
                    Fill(skin, bounds.X + 18, bounds.Y + 72, bounds.Width - 36, 1, Copper);
                    break;
                case PetChromeStyle.Card:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, BorderColor, 2);
                    Border(skin, Inset(bounds, 4), Copper, 1);
                    Fill(skin, bounds.X + 105, bounds.Y + 10, bounds.Width - 119, 50, Copper);
                    Fill(skin, bounds.X + 105, bounds.Y + 59, bounds.Width - 119, 1, Highlight);
                    break;
                case PetChromeStyle.Portrait:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Highlight, 2);
                    Border(skin, Inset(bounds, 5), Copper, 1);
                    break;
                case PetChromeStyle.Section:
                    Fill(skin, bounds, Brown);
                    Border(skin, bounds, Copper, 1);
                    break;
                case PetChromeStyle.Bar:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Copper, 1);
                    Fill(skin, bounds.X + 2, bounds.Y + 2,
                        (int)((bounds.Width - 4) * Math.Clamp(Progress, 0f, 1f)),
                        bounds.Height - 4, ProgressColor);
                    break;
                case PetChromeStyle.PrimaryButton:
                    Fill(skin, bounds, new Color(a: 255, r: 49, g: 77, b: 42));
                    Border(skin, bounds, new Color(a: 255, r: 148, g: 183, b: 105), 2);
                    Border(skin, Inset(bounds, 4), Copper, 1);
                    break;
                case PetChromeStyle.SecondaryButton:
                    Fill(skin, bounds, Copper);
                    Border(skin, bounds, Highlight, 2);
                    Border(skin, Inset(bounds, 4), Brown, 1);
                    break;
            }
        }

        private static UiRectangle Inset(UiRectangle rect, int amount) =>
            new(rect.X + amount, rect.Y + amount,
                rect.Width - amount * 2, rect.Height - amount * 2);

        private static void Fill(SkinBase skin, UiRectangle rect, Color color)
        {
            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(rect);
        }

        private static void Fill(SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0) return;
            Fill(skin, new UiRectangle(x, y, width, height), color);
        }

        private static void Border(SkinBase skin, UiRectangle rect, Color color, int thickness)
        {
            Fill(skin, rect.X, rect.Y, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Bottom - thickness, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Y, thickness, rect.Height, color);
            Fill(skin, rect.Right - thickness, rect.Y, thickness, rect.Height, color);
        }
    }

}
