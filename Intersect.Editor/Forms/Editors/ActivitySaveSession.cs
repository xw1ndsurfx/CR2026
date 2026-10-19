using Intersect.Network.Packets.WorldEvents;
using static Intersect.Editor.Localization.EditorSafetyText;

namespace Intersect.Editor.Forms.Editors;

// Keep the draft and its window until this particular request has been persisted by the server.
internal sealed class ActivitySaveSession
{
    private static readonly Dictionary<Guid, ActivitySaveSession> Pending = [];
    private readonly Form _owner;
    private readonly System.Windows.Forms.Timer _timeout = new() { Interval = 15000 };
    private Guid _request;
    private string _title = "";
    private readonly Dictionary<Control, bool> _enabled = [];
    private readonly Action<SaveWorldActivitiesPacket> _send;
    private readonly Action<string> _showError;
    internal ActivitySaveSession(Form owner, Action<SaveWorldActivitiesPacket>? send = null, Action<string>? showError = null)
    {
        _owner = owner;
        _send = send ?? (packet => Intersect.Editor.Networking.Network.SendPacket(packet));
        _showError = showError ?? (error => MessageBox.Show(_owner, Get("Save refused: {0}", Get(error)), Get("Save protection")));
        _timeout.Tick += (_, _) => Finish(false, Get("The server has not confirmed the save. Your edits remain in this window."));
        owner.FormClosed += (_, _) => { Pending.Remove(_request); _timeout.Dispose(); };
        owner.FormClosing += (_, e) => { if (_request != Guid.Empty) e.Cancel = true; };
    }

    internal void Send(SaveWorldActivitiesPacket packet)
    {
        if (_request != Guid.Empty) return;
        _request = packet.RequestId = Guid.NewGuid();
        _title = _owner.Text;
        _owner.Text = Get("Waiting for the server to confirm the save...");
        Pending.Add(_request, this);
        foreach (Control control in _owner.Controls) { _enabled[control] = control.Enabled; control.Enabled = false; }
        _timeout.Start();
        try { _send(packet); }
        catch (Exception e) { Finish(false, e.Message); }
    }

    internal static void Receive(WorldActivitiesPacket packet)
    {
        if (Pending.TryGetValue(packet.RequestId, out var session)) session.Finish(packet.Saved, packet.Error);
    }

    private void Finish(bool saved, string error)
    {
        Pending.Remove(_request);
        _request = Guid.Empty;
        _timeout.Stop();
        _owner.Text = _title;
        foreach (var (control, enabled) in _enabled) if (!control.IsDisposed) control.Enabled = enabled;
        _enabled.Clear();
        if (saved) { _owner.Close(); return; }
        _showError(error);
    }
}
