using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Input;

internal static class MobileFullscreenWindowTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var oldContext = Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        try
        {
            using var renderer = new MetricsRenderer();
            using var skin = new TestSkin(renderer);
            skin.Colors.Button.Normal = skin.Colors.Button.Hover = skin.Colors.Button.Active = skin.Colors.Button.Disabled = Color.White;
            using var canvas = new Canvas(skin) { Size = new Point(1080, 540), Scale = 2f };
            var window = new WindowControl(canvas, "Shop") { MinimumSize = new Point(780, 620) };
            window.SetSize(780, 620);
            window.TitleLabel.Font = skin.DefaultFont;
            var bottom = new Button(window, "BottomAction") { Font = skin.DefaultFont, Text = "Buy" };
            bottom.SetBounds(24, 558, 140, 44);
            var bounds = new Rectangle(32, 8, 1000, 520);
            var layout = typeof(Intersect.Client.Framework.Gwen.Control.Base).GetMethod("RecurseLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Layout() { for (var i = 0; i < 3; i++) layout.Invoke(canvas, [skin]); }
            window.PresentFullscreen(bounds); Layout();
            var viewport = window.FullscreenViewport!;
            Check(window.Bounds == bounds, "Desktop minimum size must not overflow the safe mobile screen");
            Check(window.Children.Contains(bottom) && ReferenceEquals(bottom.Parent, window), "Fullscreen must preserve logical owners and existing children");
            Check(window.TitleBarBounds.Height >= 48 && window.TitleBarBounds.Bottom <= bounds.Height, "Close header must remain on screen");
            var close = window.Titlebar.Children.OfType<Button>().Single(button => button.Name == "MobileFullscreenClose");
            Check(close.Width >= 48 && close.Height >= 48, "Close hit target must be finger sized");
            var initialPosition = bottom.ToCanvas(new Point(10, 10));
            var gesture = new TouchScrollGesture();
            gesture.Begin(viewport, new Point(100, 450));
            gesture.Move(new Point(100, 240));
            Check(gesture.IsDragging && !gesture.End(), "Swiping must suppress click activation");
            Layout();
            Check(viewport.VerticalScrollBar.ScrollAmount > 0 && bottom.ToCanvas(new Point(10, 10)).Y < initialPosition.Y,
                "Dragging must reveal the previously off-screen bottom action");
            var center = new Point(bottom.Width / 2, bottom.Height / 2);
            var point = bottom.ToCanvas(center);
            Check(bottom.CanvasPosToLocal(point) == center && canvas.GetComponentAt(point) == bottom,
                "Hit tests and local/canvas coordinates must agree after scrolling nested content");
            var clicks = 0; bottom.Clicked += (_, _) => clicks++;
            canvas.Input_MouseMoved(point.X * 2, point.Y * 2, 0, 0);
            canvas.Input_MouseButton(Intersect.Client.Framework.Input.MouseButton.Left, true);
            canvas.Input_MouseButton(Intersect.Client.Framework.Input.MouseButton.Left, false);
            Check(clicks == 1, "A real scaled tap must reach the visible bottom action after scrolling");
            Check(window.TitleBarBounds.Y == 0, "Scrolling must not move the close header");
            gesture.Begin(viewport, new Point(100, 100)); gesture.Move(new Point(102, 102));
            Check(gesture.End(), "A steady tap must still activate its control");
            var extra = new Button(window, "DynamicAction") { Font = skin.DefaultFont }; extra.SetBounds(20, 700, 100, 44);
            window.PresentFullscreen(bounds); Layout();
            Check(viewport.VerticalScrollBar.ContentSize >= extra.Bottom, "Dynamically created content must remain reachable");
            window.PresentFullscreen(new Rectangle(20, 0, 700, 400)); Layout();
            Check(window.Width == 700 && viewport.HorizontalScrollBar.IsVisibleInTree, "Narrow screens must scroll wide legacy layouts");
            var inventory = new WindowControl(canvas, "Inventory"); inventory.TitleLabel.Font = skin.DefaultFont;
            inventory.SetSize(400, 500); inventory.PresentFullscreen(bounds);
            window.SetFullscreenPeers([window, inventory]); inventory.SetFullscreenPeers([window, inventory]); Layout();
            var picker = window.Titlebar.Children.OfType<ComboBox>().Single();
            picker.SelectedItem = picker.Items.Single(item => ReferenceEquals(item.UserData, inventory));
            Check(ReferenceEquals(canvas.Children.Last(), inventory) && !window.IsHidden, "Switching to a linked inventory must preserve the other open window");
            window.SetFullscreenPeers([window]);
            Check(picker.IsHidden, "A sole open window must return to its ordinary title");
            var closed = 0; window.Closed += (_, _) => closed++;
            window.Close(); Check(closed == 1 && window.IsHidden, "Fullscreen close must retain the window's close callbacks");

            // Reproduce the inventory's actual transaction state and desktop skin:
            // hidden titlebar and no native close icon must not trap the player.
            var locked = new WindowControl(canvas, "Inventory") { IsClosable = false };
            locked.SetSize(225, 327); locked.Titlebar.Hide();
            var endedSession = 0;
            locked.PresentFullscreen(bounds, "Fermer", () => { endedSession++; locked.Hide(); }); Layout();
            var exit = locked.Titlebar.Children.OfType<Button>().Single(button => button.Name == "MobileFullscreenClose");
            Check(locked.Titlebar.IsVisibleInTree && exit.IsVisibleInTree && !locked.IsClosable && exit.Text == "Fermer",
                "Mobile exit must remain visible when the desktop header and native close are disabled");
            point = exit.ToCanvas(new Point(exit.Width / 2, exit.Height / 2));
            Check(canvas.GetComponentAt(point) == exit, "Mobile exit must be reachable at its actual rendered position");
            canvas.Input_MouseMoved(point.X * 2, point.Y * 2, 0, 0);
            canvas.Input_MouseButton(Intersect.Client.Framework.Input.MouseButton.Left, true);
            canvas.Input_MouseButton(Intersect.Client.Framework.Input.MouseButton.Left, false);
            Check(endedSession == 1 && locked.IsHidden, "Mobile exit must invoke session cleanup exactly once before dismissing locked inventory");

            foreach (var safe in new[] { new Rectangle(58, 0, 1070, 540), new Rectangle(24, 12, 776, 430), new Rectangle(0, 0, 640, 360) })
            {
                var action = new Rectangle(safe.Right - 150, safe.Y + safe.Height / 2, 140, 110);
                var block = new Rectangle(safe.Right - 260, action.Y + 32, 100, 72);
                var desired = new Rectangle(action.X + 4, action.Y + 6, 136, 36);
                var placed = Intersect.Client.Core.Controls.MobileHudLayout.AvoidControls(desired, safe, [action, block]);
                Check(placed.X >= safe.X && placed.Y >= safe.Y && placed.Right <= safe.Right && placed.Bottom <= safe.Bottom,
                    "Emote picker must fit the safe mobile viewport");
                foreach (var obstacle in new[] { action, block })
                    Check(!(placed.X < obstacle.Right && placed.Right > obstacle.X && placed.Y < obstacle.Bottom && placed.Bottom > obstacle.Y),
                        "Emote picker must not overlap Interact or Block touch targets");
            }
            System.Console.WriteLine("PASS mobile fullscreen windows: safe bounds, fixed header, preserved owners, swipe vs tap, dynamic content, narrow-screen overflow and close callbacks");
        }
        catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { Intersect.Core.ApplicationContext.Context.Value = oldContext; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
