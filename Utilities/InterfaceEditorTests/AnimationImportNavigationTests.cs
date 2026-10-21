using System.Collections;
using System.Reflection;
using Intersect.Editor.Forms;

internal static class AnimationImportNavigationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(FrmAnimationImport form, string name) =>
        (T)typeof(FrmAnimationImport).GetField(name, Private)!.GetValue(form)!;

    internal static void Run()
    {
        var previousDirectory = Environment.CurrentDirectory;
        var fixture = Path.Combine(Path.GetTempPath(), "animation-navigation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "resources"));
        try
        {
            Environment.CurrentDirectory = fixture;
            using var form = new FrmAnimationImport { Opacity = 0, ShowInTaskbar = false };
            form.Show();
            PumpUntil(() => !Field<bool>(form, "_libraryLoading"));
            var imagePath = Path.Combine(fixture, "fixture.png");
            using (var bitmap = new Bitmap(16, 16)) bitmap.Save(imagePath);
            var assetType = typeof(FrmAnimationImport).GetNestedType("AnimationAsset", BindingFlags.NonPublic)!;
            var assets = Field<IList>(form, "_assets");
            for (var index = 0; index < 122; index++)
            {
                var asset = Activator.CreateInstance(assetType)!;
                foreach (var (key, value) in new (string, object)[]
                {
                    ("FilePath", imagePath), ("FileName", $"Fixture{index:D3}.png"),
                    ("SuggestedName", $"Fixture{index:D3}"), ("Category", "Misc"),
                    ("MetadataLoaded", true), ("LastWriteTimeUtc", DateTime.UtcNow),
                }) assetType.GetProperty(key)!.SetValue(asset, value);
                assets.Add(asset);
            }
            typeof(FrmAnimationImport).GetMethod("PopulateAssetList", Private)!.Invoke(form, null);
            Application.DoEvents();
            var list = Field<ListView>(form, "_assetList");
            var previous = Field<Button>(form, "_previousAnimationButton");
            var next = Field<Button>(form, "_nextAnimationButton");
            Check(!previous.Enabled && next.Enabled, "First animation boundaries");
            list.SelectedItems.Clear(); list.Items[119].Selected = true;
            next.PerformClick();
            Check(Field<int>(form, "_assetPage") == 1 && list.SelectedIndices[0] == 0,
                "Next animation must cross to the first animation of the next page");
            previous.PerformClick();
            Check(Field<int>(form, "_assetPage") == 0 && list.SelectedIndices[0] == 119,
                "Previous animation must cross to the last animation of the previous page");
            next.PerformClick(); next.PerformClick();
            Check(previous.Enabled && !next.Enabled && list.SelectedItems[0].Text == "Fixture121",
                "Last animation boundaries and rapid navigation");
            PumpUntil(() =>
            {
                var info = Field<Label>(form, "_assetInfo").Text;
                return info.Contains("Fixture121.png") && !info.StartsWith("Loading");
            });
            Field<TextBox>(form, "_assetSearch").Text = "Fixture042";
            Check(list.Items.Count == 1 && !previous.Enabled && !next.Enabled,
                "Navigation must respect a single filtered result");
            Field<TextBox>(form, "_assetSearch").Text = "No matching animation";
            Check(list.Items.Count == 0 && !previous.Enabled && !next.Enabled,
                "Empty filters must disable navigation");
            form.Close(); Application.DoEvents();
            System.Console.WriteLine("PASS animation preview navigation: page crossing both ways, rapid selection, boundaries and filters");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            // The fixture is isolated in the temporary directory; cancelled image workers may still be releasing files.
        }
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        do
        {
            Application.DoEvents();
            if (condition()) return;
            Thread.Sleep(10);
        } while (DateTime.UtcNow < deadline);
        throw new TimeoutException("Animation preview did not finish loading.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
