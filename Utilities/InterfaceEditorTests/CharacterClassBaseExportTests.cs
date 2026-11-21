using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Editor.Forms;
using Intersect.Framework.Core.QualityOfLife;

internal static class CharacterClassBaseExportTests
{
    internal static void Run()
    {
        var type = typeof(FrmCharacterGenerator);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var root = Path.Combine(Path.GetTempPath(), "cr-class-bases-" + Guid.NewGuid().ToString("N"));
        var sources = Path.Combine(root, "Base");
        var output = Path.Combine(root, "entities");
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        try
        {
            foreach (var sex in new[] { "Male", "Female" })
            foreach (var code in new[] { "Mov", "Mel", "Mag", "Idl", "Ran", "Use" })
            {
                var folder = Path.Combine(sources, sex); Directory.CreateDirectory(folder);
                using var bitmap = new Bitmap(32, 32);
                bitmap.SetPixel(12, 12, sex == "Male" ? Color.Red : Color.Blue);
                bitmap.Save(Path.Combine(folder, code + "_Body.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            var rawFamilies = type.GetMethod("DiscoverFamilies", flags)!.Invoke(null, [sources])!;
            var families = ((IEnumerable)rawFamilies).Cast<object>().ToArray();
            string Name(object family) => (string)family.GetType().GetProperty("Name")!.GetValue(family)!;
            var male = families.Single(f => Name(f) == "Male_Body");
            var female = families.Single(f => Name(f) == "Female_Body");
            var bodyFolder = Path.Combine(root, "Body");
            var legacyFolder = Path.Combine(root, "Female");
            Directory.CreateDirectory(bodyFolder); Directory.CreateDirectory(legacyFolder);
            using (var bitmap = new Bitmap(32, 32))
            {
                bitmap.SetPixel(12, 12, Color.Pink);
                bitmap.Save(Path.Combine(bodyFolder, "M_Mov_Body_Ape.png"), System.Drawing.Imaging.ImageFormat.Png);
                bitmap.Save(Path.Combine(legacyFolder, "F_Mov_Body_Demon.png"), System.Drawing.Imaging.ImageFormat.Png);
                bitmap.Save(Path.Combine(legacyFolder, "F_Mov_Chest_Cotton.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            var generator = (FrmCharacterGenerator)RuntimeHelpers.GetUninitializedObject(type); GC.SuppressFinalize(generator);
            var field = type.GetField("_partsByCategory", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var catalog = (IDictionary)Activator.CreateInstance(field.FieldType)!;
            catalog.Add("Base", rawFamilies);
            catalog.Add("Body", type.GetMethod("DiscoverFamilies", flags)!.Invoke(null, [bodyFolder])!);
            catalog.Add("Female", type.GetMethod("DiscoverFamilies", flags)!.Invoke(null, [legacyFolder])!);
            field.SetValue(generator, catalog);
            var choices = ((IEnumerable)type.GetMethod("GetClassBaseFamilies", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(generator, null)!).Cast<object>().Select(Name).ToArray();
            Check(choices.Any(n => n.Contains("Body_Ape")) && choices.Any(n => n.Contains("Body_Demon"))
                && !choices.Any(n => n.Contains("Chest")), "Body variants and legacy female bodies are exportable without including clothing.");
            var writer = type.GetMethod("WriteClassBaseSprites", flags)!;
            writer.Invoke(null, [male, female, output, "mage_base", false]);
            Check(Directory.GetFiles(output).Length == 12, "Both sexes export all six animations.");
            foreach (var sex in new[] { "male", "female" })
            foreach (var suffix in new[] { "", "_attack", "_cast", "_idle", "_shoot", "_weapon" })
            {
                using var bitmap = new Bitmap(Path.Combine(output, "mage_base_" + sex + suffix + ".png"));
                Check(bitmap.Size == new Size(32, 32) && bitmap.GetPixel(0, 0).A == 0,
                    "Class bases preserve source dimensions and transparent background.");
                Check(bitmap.GetPixel(12, 12).ToArgb() == (sex == "male" ? Color.Red : Color.Blue).ToArgb(),
                    "The female body must not be a duplicate of the male artwork.");
            }
            try { writer.Invoke(null, [male, female, output, "mage_base", false]); throw new Exception("Overwrite accepted without approval."); }
            catch (TargetInvocationException error) when (error.InnerException is IOException) { }
            var missing = Activator.CreateInstance(male.GetType())!;
            male.GetType().GetProperty("Name")!.SetValue(missing, "Female_Missing");
            var failedOutput = Path.Combine(root, "failed");
            try { writer.Invoke(null, [male, missing, failedOutput, "base", false]); throw new Exception("Incomplete pair accepted."); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { }
            Check(!Directory.Exists(failedOutput), "Missing female artwork must not leave a male-only export.");
            foreach (var locale in new[] { "fr", "es" })
                Check(QualityOfLifeText.Get("Export both bases", locale) != "Export both bases", "Export controls are localized.");
        }
        finally
        {
            var full = Path.GetFullPath(root);
            if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(full).StartsWith("cr-class-bases-", StringComparison.Ordinal) && Directory.Exists(full))
                Directory.Delete(full, true);
        }
        System.Console.WriteLine("PASS class bases: both real sexes, six animations, transparency, no silent overwrite, no partial missing-sex export, EN/FR/ES.");
    }
}
