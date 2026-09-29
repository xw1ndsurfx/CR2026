using Intersect.Client.General;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private InvasionStatusWindow? _invasionStatusWindow;
    private InvasionResultWindow? _invasionResultWindow;
    private bool _targetBoxMovedForInvasion;
    private int _targetBoxOriginalX;
    private int _targetBoxOriginalY;

    public void UpdateInvasionStatus(InvasionStatusPacket packet)
    {
        _invasionStatusWindow ??= new InvasionStatusWindow(GameCanvas);
        _invasionStatusWindow.Apply(packet);

        if (packet.Active)
            RepositionTargetBoxBelowInvasion();
        else
            RestoreTargetBoxPosition();
    }

    public bool TryShowInvasionAnnouncement(string announcementText, long displayTime)
    {
        if (string.IsNullOrWhiteSpace(announcementText) ||
            !announcementText.TrimStart().StartsWith("INVASION IN ", StringComparison.OrdinalIgnoreCase))
            return false;

        _invasionStatusWindow ??= new InvasionStatusWindow(GameCanvas);
        _invasionStatusWindow.ShowReminder(announcementText, displayTime);
        RepositionTargetBoxBelowInvasion();
        return true;
    }

    private void UpdateInvasionUi()
    {
        _invasionStatusWindow?.Update();

        if (_invasionStatusWindow?.IsVisibleInTree == true)
            RepositionTargetBoxBelowInvasion();
        else
            RestoreTargetBoxPosition();
    }

    private void RepositionTargetBoxBelowInvasion()
    {
        var targetWindow = Globals.Me?.TargetBox?.EntityWindow;
        if (targetWindow == null || _invasionStatusWindow?.IsVisibleInTree != true)
            return;

        if (!_targetBoxMovedForInvasion)
        {
            _targetBoxOriginalX = targetWindow.X;
            _targetBoxOriginalY = targetWindow.Y;
            _targetBoxMovedForInvasion = true;
        }

        var desiredX = _invasionStatusWindow.X + (_invasionStatusWindow.Width - targetWindow.Width) / 2;
        var desiredY = _invasionStatusWindow.Y + _invasionStatusWindow.Height + 8;
        var maxX = Math.Max(8, GameCanvas.Width - targetWindow.Width - 8);
        var maxY = Math.Max(8, GameCanvas.Height - targetWindow.Height - 8);

        targetWindow.SetPosition(
            Math.Clamp(desiredX, 8, maxX),
            Math.Clamp(desiredY, 8, maxY)
        );
        targetWindow.BringToFront();
    }

    private void RestoreTargetBoxPosition()
    {
        if (!_targetBoxMovedForInvasion)
            return;

        var targetWindow = Globals.Me?.TargetBox?.EntityWindow;
        targetWindow?.SetPosition(_targetBoxOriginalX, _targetBoxOriginalY);
        _targetBoxMovedForInvasion = false;
    }

    public void ShowInvasionResult(InvasionResultPacket packet)
    {
        _invasionResultWindow ??= new InvasionResultWindow(GameCanvas);
        GameMenu?.HideWindows();
        _invasionResultWindow.Apply(packet);
    }
}
