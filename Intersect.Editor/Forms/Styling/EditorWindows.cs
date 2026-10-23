using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.QualityOfLife;
using WeifenLuo.WinFormsUI.Docking;

namespace Intersect.Editor.Forms.Styling;

internal static class EditorWindows
{
    private sealed class State
    {
        internal bool Fullscreen;
        internal Rectangle Bounds;
        internal FormBorderStyle Border;
        internal FormWindowState WindowState;
    }

    private const int FullscreenCommand = 0x1FE0;
    private static readonly ConditionalWeakTable<Form, State> States = new();
    private static bool _filterInstalled;

    internal static void Show(Form window, Form owner)
    {
        if (window.IsDisposed) return;
        if (window != owner && window.Owner == null) window.Owner = owner;
        if (!window.Visible)
        {
            if (window.StartPosition is FormStartPosition.WindowsDefaultLocation or FormStartPosition.WindowsDefaultBounds or FormStartPosition.CenterScreen)
                window.StartPosition = FormStartPosition.CenterParent;
            EditorTheme.Apply(window);
            ConstrainToScreen(window, owner);
            window.Show(owner);
        }
        if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
        window.BringToFront();
        window.Activate();
    }

    internal static void Attach(Form window)
    {
        if (!window.TopLevel || window is DockContent || States.TryGetValue(window, out _)) return;
        States.Add(window, new State());
        if (!_filterInstalled)
        {
            Application.AddMessageFilter(new WindowKeys());
            _filterInstalled = true;
        }
        window.HandleCreated += (_, _) => AddFullscreenCommand(window);
        window.Shown += (_, _) =>
        {
            ConstrainToScreen(window, window.Owner);
            AddFullscreenCommand(window);
        };
        if (window.IsHandleCreated) AddFullscreenCommand(window);
    }

    internal static bool SupportsFullscreen(Form window) => window is not FrmEvent && window is not DockContent &&
        window.TopLevel && window.MaximizeBox &&
        window.FormBorderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;

    internal static bool IsFullscreen(Form window) => States.TryGetValue(window, out var state) && state.Fullscreen;

    internal static void ToggleFullscreen(Form window)
    {
        Attach(window);
        if (!States.TryGetValue(window, out var state)) return;
        if (state.Fullscreen)
        {
            state.Fullscreen = false;
            window.WindowState = FormWindowState.Normal;
            window.FormBorderStyle = state.Border;
            window.Bounds = state.Bounds;
            window.WindowState = state.WindowState;
            ConstrainToScreen(window, window.Owner);
        }
        else if (SupportsFullscreen(window))
        {
            state.Bounds = window.WindowState == FormWindowState.Normal ? window.Bounds : window.RestoreBounds;
            state.WindowState = window.WindowState;
            state.Border = window.FormBorderStyle;
            var screen = Screen.FromControl(window).Bounds;
            state.Fullscreen = true;
            window.WindowState = FormWindowState.Normal;
            window.FormBorderStyle = FormBorderStyle.None;
            window.Bounds = screen;
        }
    }

    internal static void ConstrainToScreen(Form window, Form? owner)
    {
        if (!window.TopLevel || window is DockContent || IsFullscreen(window) || window.WindowState != FormWindowState.Normal) return;
        var area = Screen.FromControl(owner ?? window).WorkingArea;
        // A legacy minimum larger than the monitor must not hide the window's actions.
        window.MinimumSize = new Size(Math.Min(window.MinimumSize.Width, area.Width), Math.Min(window.MinimumSize.Height, area.Height));
        var bounds = window.Bounds;
        bounds.Size = new Size(Math.Min(bounds.Width, area.Width), Math.Min(bounds.Height, area.Height));
        bounds.Location = new System.Drawing.Point(Math.Clamp(bounds.X, area.Left, area.Right - bounds.Width),
            Math.Clamp(bounds.Y, area.Top, area.Bottom - bounds.Height));
        window.Bounds = bounds;
    }

    private static void AddFullscreenCommand(Form window)
    {
        if (!OperatingSystem.IsWindows() || !window.IsHandleCreated || window.IsDisposed) return;
        var menu = GetSystemMenu(window.Handle, false);
        if (menu == IntPtr.Zero) return;
        RemoveMenu(menu, FullscreenCommand, 0);
        if (SupportsFullscreen(window) || IsFullscreen(window))
            AppendMenu(menu, 0, FullscreenCommand, QualityOfLifeText.Get("Full screen (F11)",
                System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
    }

    private sealed class WindowKeys : IMessageFilter
    {
        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg is not (0x100 or 0x104 or 0x112)) return false;
            var window = Control.FromHandle(message.HWnd)?.FindForm();
            if (window == null || window.IsDisposed || !States.TryGetValue(window, out _)) return false;
            if (message.Msg == 0x112)
            {
                if (((long)message.WParam & 0xFFF0) != FullscreenCommand) return false;
            }
            else
            {
                var key = (Keys)(int)message.WParam;
                if (key == Keys.Escape && IsFullscreen(window)) { ToggleFullscreen(window); return true; }
                if (key != Keys.F11 || Control.ModifierKeys != Keys.None ||
                    (!SupportsFullscreen(window) && !IsFullscreen(window))) return false;
                // Holding F11 must not repeatedly enter and exit fullscreen.
                if (((long)message.LParam & (1L << 30)) != 0) return true;
            }
            ToggleFullscreen(window);
            return true;
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr window, bool revert);
    [DllImport("user32.dll")] private static extern bool RemoveMenu(IntPtr menu, uint position, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, nuint id, string text);
}
