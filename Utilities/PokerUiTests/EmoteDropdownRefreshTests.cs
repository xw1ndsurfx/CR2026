using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Input;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Network.Packets.ChatEmotes;

internal static class EmoteDropdownRefreshTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var oldContext = Intersect.Core.ApplicationContext.Context.Value;
        var hovered = InputHandler.HoveredControl;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        try
        {
            foreach (var scale in new[] { 1f, 2f })
            {
                using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
                skin.Colors.Button.Normal = skin.Colors.Button.Hover = skin.Colors.Button.Active = skin.Colors.Button.Disabled = Color.White;
                using var canvas = new Canvas(skin) { Size = new Point(1000, 540), Scale = scale };
                var panel = new ImagePanel(canvas); panel.SetBounds(500, 330, 350, 180);
                var messages = new ListBox(panel); messages.SetBounds(0, 0, 340, 120);
                var input = new TextBox(panel) { Font = skin.DefaultFont }; input.SetBounds(0, 140, 300, 30);
                var combo = new ComboBox(canvas) { Font = skin.DefaultFont, FontSize = 12, AutoSizeToContents = false, OpenMenuAbove = true };
                var chat = (Chatbox)RuntimeHelpers.GetUninitializedObject(typeof(Chatbox));
                void Set(string field, object value) => typeof(Chatbox).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(chat, value);
                Set("mChatboxWindow", panel); Set("mChatboxMessages", messages); Set("mChatboxInput", input); Set("_emotes", combo);
                var wave = Guid.NewGuid(); var bow = Guid.NewGuid();
                var packet = new ChatEmoteMenuPacket { Entries = [new() { Id = wave, Name = "Wave" }, new() { Id = bow, Name = "Bow" }] };
                var userSelections = 0; var refreshRequests = 0;
                combo.ItemSelected += (_, args) => { if (!args.Automated) userSelections++; };
                combo.Clicked += (_, _) => refreshRequests++;
                var layout = typeof(Intersect.Client.Framework.Gwen.Control.Base).GetMethod("RecurseLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;
                void Layout() { for (var i = 0; i < 3; i++) layout.Invoke(canvas, [skin]); }
                void Tap(Intersect.Client.Framework.Gwen.Control.Base control)
                {
                    var point = control.ToCanvas(new Point(control.Width / 2, control.Height / 2));
                    canvas.Input_MouseMoved((int)(point.X * scale), (int)(point.Y * scale), 0, 0);
                    canvas.Input_MouseButton(MouseButton.Left, true); canvas.Input_MouseButton(MouseButton.Left, false);
                }
                chat.ApplyEmotes(packet); Layout();
                Check(!combo.IsOpen, "Initial unsolicited emote data must not open a closed dropdown");
                Tap(combo);
                Check(combo.IsOpen && refreshRequests == 1, "The first real click/tap must open and request refresh once");
                chat.ApplyEmotes(packet); Layout();
                Check(combo.IsOpen && userSelections == 0, "A delayed emote refresh must preserve the open menu without executing an emote");
                Tap(combo.Items.Single(item => Equals(item.UserData, bow)));
                Check(!combo.IsOpen && userSelections == 1, "Selecting a refreshed row must execute exactly once and close the menu");
                combo.Open(); canvas.CloseMenus(); chat.ApplyEmotes(packet);
                Check(!combo.IsOpen, "A late reply must not reopen a menu the player dismissed");
                combo.Open(); chat.ApplyEmotes(new ChatEmoteMenuPacket()); Layout();
                Check(combo.IsOpen && combo.Items.Count == 1 && Equals(combo.Items[0].UserData, Guid.Empty) && userSelections == 1,
                    "An empty refresh must keep the open placeholder without firing an emote");
                canvas.CloseMenus();
            }
            System.Console.WriteLine("PASS emote dropdown refresh: actual PC/2x taps, first-click persistence, one-shot selection, dismissed/empty responses");
        }
        catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { InputHandler.HoveredControl = hovered; Intersect.Core.ApplicationContext.Context.Value = oldContext; }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
