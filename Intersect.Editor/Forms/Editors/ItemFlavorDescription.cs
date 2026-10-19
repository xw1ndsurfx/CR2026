using Intersect.Framework.Core.GameObjects.Items;
namespace Intersect.Editor.Forms.Editors;

internal static class ItemFlavorDescription
{
    internal static string Build(string name, ItemType type, ConsumableType consumable)
    {
        var text = (name ?? "").ToLowerInvariant();
        bool Has(params string[] words) => words.Any(text.Contains);
        if (type == ItemType.Equipment)
        {
            if (Has("sword", "blade", "dagger", "épée", "espada", "knife"))
                return "A carefully balanced blade, forged for steady hands and decisive strikes.";
            if (Has("axe", "hache", "hacha"))
                return "A sturdy axe with a keen edge, ready for hard work or a hard-fought battle.";
            if (Has("bow", "arc", "arco"))
                return "A finely strung bow, crafted for patient hunters and watchful adventurers.";
            if (Has("staff", "wand", "bâton", "varita"))
                return "An enchanted focus that hums softly when magic stirs nearby.";
            if (Has("shield", "bouclier", "escudo"))
                return "A dependable shield, made to stand between its bearer and danger.";
            if (Has("boot", "feet", "botte", "bota"))
                return "Well-made boots, ready to carry their wearer along unfamiliar paths.";
            if (Has("helm", "casque", "yelmo"))
                return "A carefully fitted helm, bearing the marks of a patient craftsperson.";
            if (Has("ring", "amulet", "necklace", "bague", "anneau", "anillo"))
                return "A finely worked ornament, carrying a quiet air of distinction.";
            return "Dependable equipment, carefully made to accompany an adventurer through the trials ahead.";
        }
        if (type == ItemType.Consumable)
            return consumable == ConsumableType.ExperienceBoost ? "A shimmering draught that clears the mind and kindles a thirst for discovery." : "A refreshing draught, carefully prepared to restore a weary adventurer.";
        if (type == ItemType.Bag)
            return "A practical bag with sturdy stitching, made to keep an adventurer’s belongings close at hand.";
        if (type == ItemType.Currency)
            return "A valued token of exchange, passed between merchants and adventurers across the kingdom.";
        return "A curious find, worth keeping close for the adventures still to come.";
    }
}
