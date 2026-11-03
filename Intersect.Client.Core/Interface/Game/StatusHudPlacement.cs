using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;

namespace Intersect.Client.Interface.Game;

// Keep the preferred coordinates separate from the clamped coordinates so a temporary
// resolution change or expansion does not lose the player's chosen position.
internal sealed class StatusHudPlacement
{
    private readonly Base _panel;
    private readonly DragHandle _handle;
    private readonly string _key;
    private int _x, _y;
    private bool _positioned;

    internal StatusHudPlacement(Base panel, Base title)
    {
        _panel = panel;
        _key = panel.Name + "Position";
        _x = Globals.Database?.LoadPreference(_key + "X", -1) ?? -1;
        _y = Globals.Database?.LoadPreference(_key + "Y", -1) ?? -1;
        _positioned = _x >= 0 && _y >= 0;
        _handle = new DragHandle(panel, SavePosition);
        _handle.Dragged += (_, _) =>
        {
            Clamp();
            _positioned = true;
            _x = _panel.X;
            _y = _panel.Y;
        };
        title.MouseInputEnabled = false;
    }

    internal void Arrange(int defaultX, int defaultY)
    {
        _handle.SetBounds(0, 0, Math.Max(0, _panel.Width - 34), 36);
        _panel.SetPosition(_positioned ? _x : defaultX, _positioned ? _y : defaultY);
        Clamp();
    }

    private void Clamp()
    {
        _panel.SetPosition(
            Math.Clamp(_panel.X, 0, Math.Max(0, (_panel.Parent?.Width ?? _panel.Width) - _panel.Width)),
            Math.Clamp(_panel.Y, 0, Math.Max(0, (_panel.Parent?.Height ?? _panel.Height) - _panel.Height)));
    }

    private void SavePosition()
    {
        Clamp();
        _positioned = true;
        _x = _panel.X;
        _y = _panel.Y;
        Globals.Database?.SavePreference(_key + "X", _x);
        Globals.Database?.SavePreference(_key + "Y", _y);
    }

    private sealed class DragHandle : Dragger
    {
        private readonly Action _save;
        internal DragHandle(Base panel, Action save) : base(panel, "HudDragHandle")
        {
            _target = panel;
            _save = save;
        }

        protected override void OnMouseUp(MouseButton mouseButton, Point mousePosition, bool userAction = true)
        {
            base.OnMouseUp(mouseButton, mousePosition, userAction);
            _save();
        }
    }
}
