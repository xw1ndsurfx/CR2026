using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
internal static class EventToolsUiTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin) { Size = new Point(1920, 1080) };
        var previous = ApplicationContext.Context.Value;
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
        try
        {
            foreach (var width in new[] { 0, 1, 5, 16, 32, 160, 600 })
            {
                var text = new RichLabel(canvas) { Font = skin.DefaultFont, FontSize = 11, Padding = new Padding(8, 8, 8, 8) };
                text.SetSize(width, 20);
                var source = "    " + new string('W', 400) + " " + string.Join(" ", Enumerable.Repeat("word", 500));
                text.AddText(source, Color.White);
                text.ForceImmediateRebuild();
                if (width > 16)
                {
                    var emitted = string.Concat(text.FormattedLabels.Select(l => l.Text)).Replace(" ", "");
                    Check(emitted == source.Replace(" ", ""), "Wrapping must preserve every non-whitespace character at width " + width);
                    Check(text.FormattedLabels.All(l => l.X >= 8), "Wrapped lines honor left padding");
                }
                else Check(text.FormattedLabels.Count == 0, "Unresolved layout widths defer text until usable bounds exist");
                text.SetSize(220, 40); text.ForceImmediateRebuild();
                Check(text.FormattedLabels.Count > 0, "Deferred text must rebuild after resize");
            }
            var unicode = new RichLabel(canvas) { Font = skin.DefaultFont, FontSize = 11 };
            unicode.SetSize(1, 20); unicode.AddText("A😀e\u0301B", Color.White); unicode.ForceImmediateRebuild();
            Check(unicode.FormattedLabels.Select(l => l.Text).SequenceEqual(new[] { "A", "😀", "e\u0301", "B" }), "Narrow wrapping preserves grapheme clusters");
            var combo = new ComboBox(canvas) { Font = skin.DefaultFont, FontSize = 10 };
            combo.AddItem("Old"); combo.AddItem("Previous"); combo.ClearItems(); combo.AddItem("New");
            var menu = (Menu)typeof(ComboBox).GetField("_menu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;
            Check(menu.Children.OfType<MenuItem>().Count() == 1, "Refreshing emotes must remove old menu entries");
            // Exercise the actual portrait layout against an oversized source texture.
            var content = new TestContentManager();
            var oldContent = Intersect.Client.General.Globals.ContentManager;
            Intersect.Client.General.Globals.ContentManager = content;
            try
            {
                content.AddFaceTexture("large.png", DispatchProxy.Create<Intersect.Client.Framework.Graphics.IGameTexture, LargeDialogueFace>());
                var eventWindow = (EventWindow)RuntimeHelpers.GetUninitializedObject(typeof(EventWindow)); GC.SuppressFinalize(eventWindow);
                var face = new ImagePanel(canvas); var prompt = new Panel(canvas); prompt.SetBounds(10, 180, 616, 180);
                var dialog = new Intersect.Client.Entities.Events.Dialog { Face = "large.png" };
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(EventWindow).GetField("_faceImage", flags)!.SetValue(eventWindow, face);
                typeof(EventWindow).GetField("_promptPanel", flags)!.SetValue(eventWindow, prompt);
                typeof(EventWindow).GetField("_dialog", flags)!.SetValue(eventWindow, dialog);
                typeof(EventWindow).GetMethod("ApplyFace", flags)!.Invoke(eventWindow, null);
                Check(face.Width <= 96 && face.Height <= 96 && face.Bottom + 8 <= prompt.Y && face.MaintainAspectRatio,
                    "Oversized dialogue portraits keep their aspect ratio above the text");
            }
            finally { Intersect.Client.General.Globals.ContentManager = oldContent; }
            var descriptor = new ItemDescriptor(Guid.NewGuid()) { Name = "Test marketplace sword" }; ItemDescriptor.Lookup[descriptor.Id] = descriptor;
            try
            {
                var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.MarketplaceWindow", true)!;
                var window = (Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(type, [canvas])!;
                var state = new MarketplaceStatePacket { Listings = [new MarketplaceListingState { ItemId = descriptor.Id, SellerName = "Alice" }, new MarketplaceListingState { SellerName = "Bob" }] };
                type.GetMethod("Apply")!.Invoke(window, [state]);
                var search = (TextBox)type.GetField("_search", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                var count = (Label)type.GetField("_listingCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                search.Text = "SWORD"; Check(count.Text.Contains("1 / 2"), "Marketplace searches item names ignoring case");
                search.Text = "Bob"; Check(count.Text.Contains("1 / 2"), "Marketplace searches seller names");
                search.Text = "missing"; Check(count.Text.Contains("0 / 2"), "Marketplace renders no-results state");
                type.GetMethod("Apply")!.Invoke(window, [state]); Check(search.Text == "missing", "Marketplace refresh preserves the search");
            }
            finally { ItemDescriptor.Lookup.DeleteAt(descriptor.Id); }
        }
        finally { ApplicationContext.Context.Value = previous; }
        System.Console.WriteLine("PASS crash regression: narrow rich text, leading spaces, long words, Unicode, resize; emote refresh and Marketplace filtering");
    }
}

public class LargeDialogueFace : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    { "get_Width" => 640, "get_Height" => 1024, _ => method?.ReturnType == typeof(bool) ? false : null };
}
