using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;

// Exercise the real compact row with no GUI textures, as in an incomplete client installation.
internal static class CompactMenuIconTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        var type = typeof(MenuContainer).GetNestedType("MenuRow", BindingFlags.NonPublic)!;
        var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var configure = type.GetMethod("ConfigureIcon", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalLanguage = ClientLanguages.Current;
        var current = typeof(ClientLanguages).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            foreach (var caption in new[] { "Inventory", "Spell Book", "Character", "Quest Log", "Dungeons", "News", "Daily Reward", "LogiCoin Shop", "Friends", "Party", "Guild", "My house", "Open Menu" })
            {
                current.SetValue(null, language);
                using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
                using var canvas = new Canvas(skin) { Size = new Point(800, 600) };
                var icon = new Button(canvas, "MissingIcon");
                var row = (Button)ctor.Invoke([canvas, "MenuRowTest", icon, caption]);
                row.SetBounds(0, 0, 220, 27); icon.Parent = row;
                configure.Invoke(row, null);
                if (!icon.IsHidden || !row.Children.Any(c => c.Name.StartsWith("MenuGlyph") && !c.IsHidden))
                    throw new InvalidOperationException("Missing texture must display a built-in glyph: " + caption);
                if (row.Text != ClientLanguages.Text(caption)) throw new InvalidOperationException("Menu row locale mismatch");
                var glyph = row.Children.Single(c => c.Name.StartsWith("MenuGlyph"));
                var before = renderer.Rectangles;
                glyph.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(glyph, [skin]);
                if (renderer.Rectangles <= before) throw new InvalidOperationException("Empty fallback glyph: " + caption);
            }
            System.Console.WriteLine("PASS compact menu: 39 native rows render nonempty fallback icons with EN/FR/ES captions without GUI textures.");
        }
        finally { current.SetValue(null, originalLanguage); }
    }
}
