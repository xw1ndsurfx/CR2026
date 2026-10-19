using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Editor.Forms;

internal static class CharacterWeaponPaperdollTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(FrmCharacterGenerator);
        var animation = type.GetNestedType("CharacterAnimation", BindingFlags.NonPublic)!;
        object Pose(string name) => Enum.Parse(animation, name);
        var root = Path.Combine(Path.GetTempPath(), "cr-weapon-pose-" + Guid.NewGuid().ToString("N"));
        var category = Path.Combine(root, "Cape");
        try
        {
            foreach (var gender in new[] { "Male", "Female" })
            foreach (var prefix in new[] { "", "B_", "F_" })
            foreach (var pose in new[] { "Mov", "Mel", "Use" })
            {
                var folder = Path.Combine(category, gender); Directory.CreateDirectory(folder);
                using var bitmap = new Bitmap(32, 32);
                using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(pose == "Mov" ? Color.Green : pose == "Use" ? Color.Blue : gender == "Male" ? Color.Red : Color.Yellow);
                bitmap.Save(Path.Combine(folder, prefix + pose + "_Test.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            var families = (IEnumerable)type.GetMethod("DiscoverFamilies", flags)!.Invoke(null, [category])!;
            var male = families.Cast<object>().Single(f => (string)f.GetType().GetProperty("Name")!.GetValue(f)! == "Male_Test");
            var female = families.Cast<object>().Single(f => (string)f.GetType().GetProperty("Name")!.GetValue(f)! == "Female_Test");
            foreach (var family in new[] { male, female })
            {
                foreach (var resolve in new[] { "Resolve", "ResolveBack", "ResolveFront" })
                {
                    var method = family.GetType().GetMethod(resolve)!;
                    var attack = (string)method.Invoke(family, [Pose("Attack")])!;
                    Check(attack.Contains("Mel_") && attack == (string)method.Invoke(family, [Pose("Weapon")])!,
                        "Weapon preview resolves the Attack art for normal, back and front layers even when Use art exists");
                }
                using var attackSheet = (Bitmap)type.GetMethod("RenderPaperdollFamily", flags)!.Invoke(null, [family, Pose("Attack")])!;
                using var weaponSheet = (Bitmap)type.GetMethod("RenderPaperdollFamily", flags)!.Invoke(null, [family, Pose("Weapon")])!;
                Check(Equal(attackSheet, weaponSheet), "Individual Weapon paperdoll exports match Attack pixel for pixel");
            }
            var generator = (FrmCharacterGenerator)RuntimeHelpers.GetUninitializedObject(type); GC.SuppressFinalize(generator);
            var instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var partsField = type.GetField("_partsByCategory", instanceFlags)!;
            var parts = (IDictionary)Activator.CreateInstance(partsField.FieldType)!; parts.Add("Cape", families); partsField.SetValue(generator, parts);
            type.GetField("_selectedPartByCategory", instanceFlags)!.SetValue(generator, new Dictionary<string, string> { ["Cape"] = "Male_Test" });
            var render = type.GetMethod("RenderCharacter", instanceFlags)!;
            using var previewAttack = (Bitmap)render.Invoke(generator, [Pose("Attack")])!;
            using var previewWeapon = (Bitmap)render.Invoke(generator, [Pose("Weapon")])!;
            Check(Equal(previewAttack, previewWeapon), "Composited character preview and full character exports use the same Weapon and Attack pose");
            var output = Path.Combine(root, "exports");
            type.GetMethod("WriteGenderVariants", flags)!.Invoke(null, [male, female, output, "cape"]);
            foreach (var gender in new[] { "male", "female" })
            {
                using var attack = new Bitmap(Path.Combine(output, "cape_" + gender + "_attack.png"));
                using var weapon = new Bitmap(Path.Combine(output, "cape_" + gender + "_weapon.png"));
                using var move = new Bitmap(Path.Combine(output, "cape_" + gender + ".png"));
                Check(Equal(attack, weapon) && weapon.GetPixel(0,0).ToArgb() == (gender == "male" ? Color.Red : Color.Yellow).ToArgb() && move.GetPixel(0,0).ToArgb() == Color.Green.ToArgb(),
                    "Gender exports preserve separate artwork and movement while Weapon matches Attack");
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        System.Console.WriteLine("PASS Weapon uses Attack: distinct Use source, normal/back/front layers, composite preview and male/female PNG exports");
    }
    private static bool Equal(Bitmap a, Bitmap b)
    {
        if (a.Size != b.Size) return false;
        for (var x = 0; x < a.Width; x++) for (var y = 0; y < a.Height; y++)
            if (a.GetPixel(x,y).ToArgb() != b.GetPixel(x,y).ToArgb()) return false;
        return true;
    }
}
