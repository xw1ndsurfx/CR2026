using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class PetsWindow : Window
{
    private readonly Label _summary;
    private readonly ScrollControl _scroll;

    public PetsWindow(Canvas parent) : base(parent, "Familiers", false, nameof(PetsWindow))
    {
        SetSize(735, 625);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var title = new Label(this, "PetsTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 21,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = "FAMILIERS",
        };
        title.SetBounds(20, 38, 695, 34);

        _summary = new Label(this, "PetsSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _summary.SetBounds(20, 75, 695, 30);

        _scroll = new ScrollControl(this, "PetsScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(24, 112, 687, 465);
        Hide();
    }

    protected override void EnsureInitialized() { }

    public void Apply(PetStatePacket packet)
    {
        _scroll.DeleteAll();
        var pets = packet.Pets ?? [];
        _summary.Text = pets.Length == 0
            ? "Aucun familier debloque. Utilise un objet d'invocation."
            : pets.Length + " familiers debloques  -  " +
              (pets.Any(p => p.IsActive) ? "1 invoque" : "aucun invoque");

        var y = 8;
        foreach (var pet in pets)
        {
            var card = new Button(_scroll, "PetCard" + pet.PetId)
            {
                Text = string.Empty,
                MouseInputEnabled = false,
            };
            card.SetBounds(8, y, 644, 272);
            card.SetStateTexture(ComponentState.Normal, "control_button.png");

            var portrait = new ImagePanel(card, "PetSprite" + pet.PetId)
            {
                MaintainAspectRatio = true,
                MouseInputEnabled = false,
            };
            portrait.SetBounds(14, 16, 76, 76);
            if (!string.IsNullOrWhiteSpace(pet.Sprite))
            {
                portrait.Texture = Globals.ContentManager.GetTexture(TextureType.Entity, pet.Sprite);
                if (portrait.Texture == null) portrait.Hide();
            }
            else portrait.Hide();

            AddText(card, "PetName" + pet.PetId, pet.Name +
                (pet.IsActive ? "  [INVOQUE]" : ""), 102, 14, 490, 30, 14, true);
            AddText(card, "PetLevel" + pet.PetId,
                "Niveau " + pet.Level + " / " + pet.MaximumLevel +
                "     HP " + pet.Health + "/" + pet.MaximumHealth +
                "     MP " + pet.Mana + "/" + pet.MaximumMana,
                102, 47, 512, 23, 10);

            var cap = pet.ExperienceToNextLevel <= 0;
            var ratio = cap ? 1.0 : Math.Clamp(
                (double)pet.Experience / Math.Max(1L, pet.ExperienceToNextLevel), 0, 1);
            AddText(card, "PetExp" + pet.PetId,
                cap ? "EXP  MAX" : "EXP  " + pet.Experience + " / " + pet.ExperienceToNextLevel +
                "  (" + (int)Math.Round(ratio * 100) + "%)",
                102, 72, 502, 22, 10);

            var labels = new[] { "ATK", "DEF", "SPD", "MAG", "RES" };
            var stats = pet.Stats ?? [];
            var statText = string.Join("     ", labels.Select((label, index) =>
                label + " " + (index < stats.Length ? stats[index] : 0)));
            AddText(card, "PetStats" + pet.PetId, statText, 18, 105, 610, 24, 10);
            AddText(card, "PetAbilities" + pet.PetId, "SORTS", 18, 137, 610, 21, 11, true);

            var abilities = pet.Abilities ?? [];
            var abilityText = abilities.Length == 0 ? "Aucun sort configure" :
                string.Join("  |  ", abilities.Take(5).Select(s =>
                    s.Name + (pet.Level >= s.RequiredLevel ? "" : " [niv. " + s.RequiredLevel + "]")));
            AddText(card, "PetAbilityNames" + pet.PetId, abilityText, 18, 160, 608, 25, 9);
            AddText(card, "PetLootStatus" + pet.PetId,
                "Auto-loot: " + (pet.AutoLoot ? "ON" : "OFF") +
                "     Rayon : " + pet.LootRadius + " cases",
                18, 190, 410, 23, 10);

            var summon = new Button(card, "PetSummon" + pet.PetId)
            {
                Text = pet.IsActive ? "Ranger" : "Invoquer",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
            };
            summon.SetBounds(18, 225, 170, 32);
            summon.Clicked += (_, _) => Networking.PacketSender.SendPetAction(
                pet.IsActive ? PetActionKind.Dismiss : PetActionKind.Summon, pet.PetId);

            var loot = new Button(card, "PetLoot" + pet.PetId)
            {
                Text = pet.AutoLoot ? "Loot: ON" : "Loot: OFF",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
                IsDisabled = !pet.IsActive,
            };
            loot.SetBounds(202, 225, 170, 32);
            loot.Clicked += (_, _) => Networking.PacketSender.SendPetAction(
                PetActionKind.ToggleAutoLoot, pet.PetId);

            y += 284;
        }
        _scroll.SetInnerSize(661, Math.Max(450, y + 10));
        _scroll.UpdateScrollBars();
    }

    private static void AddText(Base parent, string id, string value, int x, int y,
        int width, int height, int fontSize, bool heading = false)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(
                heading ? "sourcesansproblack" : "sourcesanspro") ?? Skin.DefaultFont,
            FontSize = fontSize,
            TextColorOverride = Color.White,
            Text = value,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
    }
}
