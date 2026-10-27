using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game.Character;
using Intersect.Client.Localization;
using Base = Intersect.Client.Framework.Gwen.Control.Base;

internal static class CharacterActionsTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var previous = ClientLanguages.Current;
        try
        {
            foreach (var language in new[] { "en", "fr", "es" })
            foreach (var resolution in new[] { new Point(640, 480), new Point(1600, 900) })
            {
                typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, language);
                _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
                skin.Colors.Button.Normal = skin.Colors.Button.Disabled = skin.Colors.Button.Active = skin.Colors.Button.Hover = Color.White;
                using var canvas = new Canvas(skin) { Size = resolution };
                var character = (CharacterWindow)RuntimeHelpers.GetUninitializedObject(typeof(CharacterWindow));
                var window = new WindowControl(canvas, "Character", name: "CharacterWindow"); window.SetSize(351, 454);
                void Set(string name, object value) => typeof(CharacterWindow).GetField(name, hidden)!.SetValue(character, value);
                Set("mCharacterWindow", window);
                // Bounds from the user's 351x454 legacy layout, including the
                // stat rows that were obscured when the background was enlarged.
                var names = new[] { "mAttackLabel", "mDefenseLabel", "mSpeedLabel", "mAbilityPwrLabel", "mMagicRstLabel" };
                var ys = new[] { 259, 277, 294, 313, 330 };
                for (var i = 0; i < names.Length; i++)
                { var label = new Label(window, names[i]) { Font = skin.DefaultFont, AutoSizeToContents = false }; label.SetBounds(30, ys[i], 180, 16); Set(names[i], label); }
                var details = new Button(window, "_detailsButton") { Font = skin.DefaultFont }; details.SetBounds(120, 370, 110, 24); Set("_detailsButton", details);
                typeof(CharacterWindow).GetMethod("BuildCharacterActions", hidden)!.Invoke(character, null);
                var layout = typeof(Base).GetMethod("RecurseLayout", hidden)!;
                for (var i = 0; i < 3; i++) layout.Invoke(canvas, [skin]);
                void Check(bool condition, string message) { if (!condition) throw new Exception(language + ": " + message); }
                var actions = (ScrollControl)window.Children.Single(c => c.Name == "CharacterActions");
                Check(window.Height == 454 && window.Width == 351, "Action additions stretched the baked character background");
                Check(actions.Y >= 350 && actions.Bottom <= window.Height - window.Titlebar.Height, "Actions overlap stats or extend below the inner panel");
                var buttons = Descendants(actions).OfType<Button>().Where(b => b.Name is "_detailsButton" or "_achievementsButton" or "_petsButton" or "CharacterMounts" or "CharacterJoust" or "CharacterArchery" or "CharacterMasteries").ToArray();
                Check(buttons.Length == 7, "A character action was lost");
                foreach (var button in buttons)
                {
                    var label = button.Children.OfType<Label>().Single(l => l.Name == button.Name + "Caption");
                    Check(label.Font != null && !string.IsNullOrWhiteSpace(label.Text), "Caption has no font or text");
                    var measured = renderer.MeasureText(label.Font, label.FontSize, label.Text);
                    Check(measured.X <= label.Width && measured.Y <= label.Height, "Caption collapses or clips: " + label.Text);
                    Check(button.X >= 0 && button.Right <= actions.Width && button.Bottom <= actions.Height, "Button is clipped: " + button.Name);
                    foreach (var other in buttons.Where(b => b != button))
                        Check(button.Right <= other.X || other.Right <= button.X || button.Bottom <= other.Y || other.Bottom <= button.Y, "Action buttons overlap");
                }
                foreach (var (name, y) in names.Zip(ys)) Check(((Label)typeof(CharacterWindow).GetField(name, hidden)!.GetValue(character)!).Y == y, "Stat row moved away from its background");
            }
            System.Console.WriteLine("PASS character actions: seven readable captions, three rows, preserved 351x454 background and stats; en/fr/es at 640x480 and 1600x900");
        }
        catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
        finally { typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, previous); }
    }
    private static IEnumerable<Base> Descendants(Base parent) => parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));
}
