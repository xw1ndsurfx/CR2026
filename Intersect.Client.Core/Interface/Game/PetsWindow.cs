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
        SetSize(735, 560);
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
        title.SetBounds(20, 28, 695, 34);

        _summary = new Label(this, "PetsSummary")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _summary.SetBounds(20, 65, 695, 25);

        _scroll = new ScrollControl(this, "PetsScroll")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _scroll.SetBounds(24, 100, 687, 412);
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

        var y = 10;
        foreach (var pet in pets)
        {
            // Every pet has an independent, compact card, including its own actions.
            var card = new Button(_scroll, "PetCard" + pet.PetId)
            {
                Text = string.Empty,
                MouseInputEnabled = false,
            };
            card.SetBounds(8, y, 644, 378);
            card.SetStateTexture(ComponentState.Normal, "control_button.png");

            var portrait = new ImagePanel(card, "PetSprite" + pet.PetId)
            {
                MaintainAspectRatio = true,
                MouseInputEnabled = false,
            };
            portrait.SetBounds(16, 16, 78, 78);
            if (!string.IsNullOrWhiteSpace(pet.Sprite))
            {
                portrait.Texture = Globals.ContentManager.GetTexture(TextureType.Entity, pet.Sprite);
                if (portrait.Texture == null) portrait.Hide();
            }
            else portrait.Hide();

            var displayTitle = pet.Name +
                (string.IsNullOrWhiteSpace(pet.SpeciesName) || pet.Name == pet.SpeciesName
                    ? string.Empty : " (" + pet.SpeciesName + ")");
            AddText(card, "PetName" + pet.PetId, displayTitle, 108, 13, 400, 26, 15, true,
                new Color(a: 255, r: 255, g: 229, b: 177));
            AddText(card, "PetStatus" + pet.PetId,
                pet.IsActive ? "[ INVOQUE ]" : "[ RANGE ]", 510, 14, 118, 25, 10, true,
                pet.IsActive ? new Color(a: 255, r: 112, g: 231, b: 135)
                    : new Color(a: 255, r: 189, g: 177, b: 166));
            AddText(card, "PetLevel" + pet.PetId, "Niveau " + pet.Level + " / " + pet.MaximumLevel,
                108, 40, 290, 20, 11);

            // Native progress bars share the game's skin and adapt to any pet values.
            AddBar(card, "PetHp" + pet.PetId, "HP", pet.Health, pet.MaximumHealth, 108, 65, 245);
            AddBar(card, "PetMp" + pet.PetId, "MP", pet.Mana, pet.MaximumMana, 366, 65, 245);
            AddBar(card, "PetXp" + pet.PetId, "EXP", pet.Experience,
                pet.ExperienceToNextLevel, 108, 108, 503);

            AddText(card, "PetStatsTitle" + pet.PetId, "STATISTIQUES", 18, 154, 600, 22, 11,
                true, new Color(a: 255, r: 235, g: 201, b: 151));
            var labels = new[] { "ATK", "DEF", "SPD", "MAG", "RES" };
            var stats = pet.Stats ?? [];
            for (var i = 0; i < labels.Length; i++)
            {
                var value = i < stats.Length ? stats[i] : 0;
                AddText(card, "PetStat" + i + pet.PetId, labels[i] + "   " + value,
                    18 + i * 123, 180, 118, 26, 11, true);
            }

            AddText(card, "PetAbilities" + pet.PetId, "SORTS", 18, 215, 600, 22, 11, true,
                new Color(a: 255, r: 235, g: 201, b: 151));
            var abilities = pet.Abilities ?? [];
            var abilityText = abilities.Length == 0 ? "Aucun sort configure" :
                string.Join("  |  ", abilities.Take(5).Select(spell =>
                    spell.Name + (pet.Level >= spell.RequiredLevel ? "" :
                        " [niv. " + spell.RequiredLevel + "]")));
            AddText(card, "PetAbilityNames" + pet.PetId, abilityText, 18, 239, 610, 26, 10);

            AddText(card, "PetLootStatus" + pet.PetId,
                "RAMASSAGE AUTO : " + (pet.AutoLoot ? "ACTIF" : "INACTIF") +
                "      Rayon : " + pet.LootRadius + " cases",
                18, 268, 610, 22, 10, true,
                pet.AutoLoot ? new Color(a: 255, r: 147, g: 225, b: 161) : Color.White);

            AddText(card, "PetRenameLabel" + pet.PetId, "Nom :", 18, 301, 53, 25, 10);
            var nameInput = new TextBox(card, "PetNameInput" + pet.PetId)
            {
                Text = pet.Name,
                PlaceholderText = "Nom du familier",
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 10,
                TextColorOverride = Color.White,
                TextAlign = Pos.Left | Pos.CenterV,
            };
            nameInput.SetBounds(70, 295, 316, 30);
            nameInput.SetMaxLength(24);

            void SubmitName() => Networking.PacketSender.SendPetAction(
                PetActionKind.Rename, pet.PetId, nameInput.Text);
            nameInput.SubmitPressed += (_, _) => SubmitName();

            var rename = new Button(card, "PetRename" + pet.PetId)
            {
                Text = "Renommer",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
            };
            rename.SetBounds(400, 295, 226, 30);
            rename.Clicked += (_, _) => SubmitName();

            var summon = new Button(card, "PetSummon" + pet.PetId)
            {
                Text = pet.IsActive ? "Ranger" : "Invoquer",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
            };
            summon.SetBounds(18, 334, 296, 31);
            summon.Clicked += (_, _) => Networking.PacketSender.SendPetAction(
                pet.IsActive ? PetActionKind.Dismiss : PetActionKind.Summon, pet.PetId);

            var loot = new Button(card, "PetLoot" + pet.PetId)
            {
                Text = pet.AutoLoot ? "Desactiver l'auto-loot" : "Activer l'auto-loot",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
                IsDisabled = !pet.IsActive,
            };
            loot.SetBounds(330, 334, 296, 31);
            loot.Clicked += (_, _) => Networking.PacketSender.SendPetAction(
                PetActionKind.ToggleAutoLoot, pet.PetId);

            y += 392;
        }
        _scroll.SetInnerSize(661, Math.Max(400, y + 10));
        _scroll.UpdateScrollBars();
    }

    private void AddBar(Base parent, string id, string title, long current, long maximum,
        int x, int y, int width)
    {
        var capped = maximum <= 0;
        var ratio = capped ? 0f : (float)Math.Clamp((double)current / maximum, 0d, 1d);
        var value = capped ? (maximum == 0 ? current + " / 0" : "MAX")
            : current + " / " + maximum;
        AddText(parent, id + "Label", title + "  " + value, x, y, width, 18, 10);
        var bar = new ProgressBar(parent)
        {
            AutoLabel = false,
            Value = ratio,
            Text = capped && maximum < 0 ? "MAX" : (int)Math.Round(ratio * 100) + "%",
            FontSize = 9,
            MouseInputEnabled = false,
        };
        bar.SetBounds(x, y + 19, width, 16);
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
}
