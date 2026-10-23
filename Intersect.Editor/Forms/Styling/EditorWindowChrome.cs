using System.Runtime.InteropServices;

namespace Intersect.Editor.Forms.Styling;

internal static class EditorWindowChrome
{
    internal static void Apply(Form form)
    {
        if (form.IsHandleCreated) SetCaption(form);
        else
        {
            form.HandleCreated -= OnHandleCreated;
            form.HandleCreated += OnHandleCreated;
        }
    }

    private static void OnHandleCreated(object? sender, EventArgs args)
    {
        if (sender is Form form) SetCaption(form);
    }

    private static void SetCaption(Form form)
    {
        // DWM keeps native move/resize, system menus and accessibility intact.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        var dark = 1;
        if (DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
