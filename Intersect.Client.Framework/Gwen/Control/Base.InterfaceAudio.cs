using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Framework.Gwen.Control.Layout;

namespace Intersect.Client.Framework.Gwen.Control;

public partial class Base
{
    /// <summary>Opt out for rhythm controls, custom sound sequences, or decorative controls.</summary>
    public bool DefaultInterfaceSounds { get; set; } = true;

    public UiWindowSounds? InterfaceWindowSounds { get; set; }

    internal bool AllowsDefaultInterfaceSounds
    {
        get
        {
            for (Base? control = this; control != null; control = control.Parent)
                if (!control.DefaultInterfaceSounds) return false;
            return true;
        }
    }

    protected virtual bool HasDefaultClickSound => DefaultInterfaceSounds &&
        (Clicked != null || DoubleClicked != null || this is Button or TableRow);

    protected virtual bool HasDefaultHoverSound => HasDefaultClickSound;

    protected void QueueInterfaceSound(UiCue cue)
    {
        if (IsDisabledByTree || !IsVisibleInTree) return;
        if (!AllowsDefaultInterfaceSounds) return;
        Canvas?.RequestInterfaceSound(cue);
    }

    private UiCue DefaultClickCue => this switch
    {
        Checkbox check => check.IsChecked ? UiCue.ToggleOn : UiCue.ToggleOff,
        TabButton => UiCue.Tab,
        TableRow or ImagePanel => UiCue.Select,
        _ when Name != null && (Name.Contains("Close", StringComparison.OrdinalIgnoreCase) ||
            Name.Contains("Cancel", StringComparison.OrdinalIgnoreCase) || Name.Contains("Back", StringComparison.OrdinalIgnoreCase) ||
            Name.Contains("Exit", StringComparison.OrdinalIgnoreCase)) => UiCue.Back,
        _ => UiCue.Click,
    };
}
