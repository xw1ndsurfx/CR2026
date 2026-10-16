using System.Drawing.Imaging;
namespace Intersect.Editor.Forms;

public sealed partial class FrmCharacterGenerator
{
    private static string StripExplicitGender(string name) => name.StartsWith("Female_", StringComparison.OrdinalIgnoreCase) ? name[7..] : name.StartsWith("Male_", StringComparison.OrdinalIgnoreCase) ? name[5..] : name;
    private static string GenderNeutralName(string name)
    {
        name = StripExplicitGender(name);
        if (name.Length > 2 && name[1] == '_' && "BFM".Contains(char.ToUpperInvariant(name[0]))) name = name[2..];
        return name;
    }
    private (PartFamily? Male, PartFamily? Female)? ResolveExportGenders(string category, PartFamily selected)
    {
        var explicitFemale = selected.Name.StartsWith("Female_", StringComparison.OrdinalIgnoreCase) || selected.Name.StartsWith("F_", StringComparison.OrdinalIgnoreCase);
        var explicitMale = selected.Name.StartsWith("Male_", StringComparison.OrdinalIgnoreCase) || selected.Name.StartsWith("M_", StringComparison.OrdinalIgnoreCase) || selected.Name.StartsWith("B_", StringComparison.OrdinalIgnoreCase);
        if (!explicitMale && !explicitFemale) explicitFemale = _selectedGender == CharacterGender.Female;
        var other = FindOppositeFamily(_partsByCategory.GetValueOrDefault(category) ?? [], selected, explicitFemale);
        if (other == null)
        {
            var answer = MessageBox.Show(this, "No matching paperdoll exists for the opposite sex. Use this same artwork for both?\nYes: copy it. No: export the selected sex only. Cancel: stop.", "Paperdoll gender variants", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return null;
            if (answer == DialogResult.Yes) other = selected;
        }
        return explicitFemale ? (other, selected) : (selected, other);
    }
    private static PartFamily? FindOppositeFamily(IEnumerable<PartFamily> families, PartFamily selected, bool female) =>
        families.FirstOrDefault(p => p != selected && GenderNeutralName(p.Name).Equals(GenderNeutralName(selected.Name), StringComparison.OrdinalIgnoreCase) &&
            (female ? p.Name.StartsWith("Male_", StringComparison.OrdinalIgnoreCase) || p.Name.StartsWith("M_", StringComparison.OrdinalIgnoreCase) || p.Name.StartsWith("B_", StringComparison.OrdinalIgnoreCase)
                : p.Name.StartsWith("Female_", StringComparison.OrdinalIgnoreCase) || p.Name.StartsWith("F_", StringComparison.OrdinalIgnoreCase)));
    private void ExportGenderVariants((PartFamily? Male, PartFamily? Female) variants, string stem) =>
        WriteGenderVariants(variants.Male, variants.Female, _paperdollsRoot, stem);
    private static void WriteGenderVariants(PartFamily? male, PartFamily? female, string directory, string stem)
    {
        Directory.CreateDirectory(directory);
        foreach (var pair in new[] { (Family: male, Gender: "male"), (Family: female, Gender: "female") })
        {
            if (pair.Family == null) continue;
            foreach (var definition in AnimationDefinitions)
            {
                using var bitmap = RenderPaperdollFamily(pair.Family, definition.Animation);
                if (bitmap == null) throw new InvalidDataException("Missing gender variant animation: " + definition.Label);
                var destination = Path.Combine(directory, stem + "_" + pair.Gender + definition.Suffix + ".png");
                var temporary = destination + ".tmp"; bitmap.Save(temporary, ImageFormat.Png); File.Move(temporary, destination, true);
            }
        }
    }
}
