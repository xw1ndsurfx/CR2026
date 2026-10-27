using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Models;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Editor.Forms.Cinematics;

// Native creation/save requests are asynchronous. Never publish a chapter until
// its personal completion variable has been acknowledged by the server.
internal sealed class CinematicChapterSaveSession
{
    private enum Phase { Ready, CreatingVariable, SavingVariable, CreatingEvent, SavingEvent, Finished, Failed }
    private readonly EventPage _page;
    private readonly EventDescriptor? _existing;
    private readonly PlayerVariableDescriptor? _newVariable;
    private readonly Action<GameObjectType, Guid> _create;
    private readonly Action<IDatabaseObject> _save;
    private readonly bool _variableAlreadyCreated;
    private bool _requestedVariable;
    private bool _requestedEvent;
    private Phase _phase;
    private EventDescriptor? _sentEvent;
    internal Guid EventId { get; }
    internal Guid CompletionId => _page.CinematicChapter!.CompletionVariableId;
    internal bool Pending => _phase is Phase.CreatingVariable or Phase.SavingVariable or Phase.CreatingEvent or Phase.SavingEvent;
    internal bool Succeeded => _phase == Phase.Finished;
    internal bool VariableCreated { get; private set; }
    internal bool EventCreated { get; private set; }
    internal string StatusKey { get; private set; } = "ChapterSavePending";
    internal event Action? Changed;

    internal CinematicChapterSaveSession(Guid eventId, EventPage compiled, EventDescriptor? existing,
        PlayerVariableDescriptor? newVariable, Action<GameObjectType, Guid> create, Action<IDatabaseObject> save,
        bool variableAlreadyCreated = false)
    {
        EventId = eventId;
        _page = compiled;
        _existing = existing == null ? null : CopyEvent(existing.Id, existing.JsonData);
        _newVariable = newVariable;
        _create = create;
        _save = save;
        _variableAlreadyCreated = variableAlreadyCreated;
    }

    internal void Start()
    {
        if (_phase != Phase.Ready) return;
        if (_newVariable != null)
        {
            _phase = _variableAlreadyCreated ? Phase.SavingVariable : Phase.CreatingVariable;
            StatusKey = "ChapterCreatingProgress";
            Changed?.Invoke();
            if (_variableAlreadyCreated) _save(_newVariable);
            else { _requestedVariable = true; _create(GameObjectType.PlayerVariable, CompletionId); }
        }
        else BeginEvent();
    }

    internal void Receive(GameObjectPacket packet)
    {
        // A delayed successful creation still belongs to this request after a
        // timeout. Remember ownership for a retry, but never resume abandoned work.
        var remembered = false;
        if (packet.CreationSucceeded && !packet.Deleted && string.IsNullOrEmpty(packet.CreationError))
        {
            if (_requestedVariable && packet.Type == GameObjectType.PlayerVariable && packet.Id == CompletionId && !VariableCreated)
            { VariableCreated = true; remembered = true; }
            if (_requestedEvent && packet.Type == GameObjectType.Event && packet.Id == EventId && !EventCreated)
            { EventCreated = true; remembered = true; }
        }
        if (!Pending) { if (remembered) Changed?.Invoke(); return; }
        var variablePhase = _phase is Phase.CreatingVariable or Phase.SavingVariable;
        if (packet.Id != (variablePhase ? CompletionId : EventId) ||
            packet.Type != (variablePhase ? GameObjectType.PlayerVariable : GameObjectType.Event)) return;
        var creating = _phase is Phase.CreatingVariable or Phase.CreatingEvent;
        if (!string.IsNullOrEmpty(packet.CreationError))
        { if (creating) Fail(packet.CreationError); return; }
        if (packet.Deleted) { Fail("ChapterSaveObjectRemoved"); return; }
        if (creating && !packet.CreationSucceeded) return;
        if (!creating && packet.CreationSucceeded) return;
        try
        {
            if (_phase == Phase.CreatingVariable)
            {
                var confirmed = new PlayerVariableDescriptor(packet.Id);
                confirmed.Load(packet.Data);
                // Keep the server's object metadata and only set our authored fields.
                confirmed.Name = _newVariable!.Name;
                confirmed.Folder = _newVariable.Folder;
                confirmed.TextId = _newVariable.TextId;
                confirmed.DataType = VariableDataType.Boolean;
                VariableCreated = true;
                _phase = Phase.SavingVariable;
                Changed?.Invoke();
                _save(confirmed);
            }
            else if (_phase == Phase.SavingVariable)
            {
                var confirmed = new PlayerVariableDescriptor(packet.Id);
                confirmed.Load(packet.Data);
                if (confirmed.DataType != VariableDataType.Boolean || confirmed.Name != _newVariable!.Name ||
                    confirmed.TextId != _newVariable.TextId || confirmed.Folder != _newVariable.Folder) return;
                BeginEvent();
            }
            else if (_phase == Phase.CreatingEvent)
            {
                var confirmed = CopyEvent(packet.Id, packet.Data);
                if (!confirmed.CommonEvent || confirmed.Global || confirmed.Pages.Any(page => page.CinematicChapter != null))
                { Fail("ChapterObjectConflict"); return; }
                EventCreated = true;
                SendEvent(confirmed);
            }
            else if (_phase == Phase.SavingEvent)
            {
                var confirmed = CopyEvent(packet.Id, packet.Data);
                if (!confirmed.CommonEvent || confirmed.Global || confirmed.Name != _sentEvent!.Name || confirmed.Pages.Count != 1 ||
                    !JToken.DeepEquals(JToken.FromObject(confirmed.Pages[0]), JToken.FromObject(_sentEvent.Pages[0]))) return;
                _phase = Phase.Finished;
                StatusKey = "ChapterSaveConfirmed";
                Changed?.Invoke();
            }
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { Fail("ChapterSaveFailed"); }
    }

    internal void Fail(string key)
    {
        if (!Pending) return;
        _phase = Phase.Failed;
        StatusKey = key;
        Changed?.Invoke();
    }

    private void BeginEvent()
    {
        StatusKey = "ChapterSavingContent";
        if (_existing != null) SendEvent(_existing);
        else
        {
            _phase = Phase.CreatingEvent;
            Changed?.Invoke();
            _requestedEvent = true;
            _create(GameObjectType.Event, EventId);
        }
    }

    private void SendEvent(EventDescriptor confirmed)
    {
        // Preserve existing page conditions, graphics and triggers when replacing
        // the generated commands. Never mutate the editor registry optimistically.
        var page = confirmed.Pages.FirstOrDefault() ?? _page;
        page.CinematicChapter = _page.CinematicChapter;
        page.Cinematic = null;
        page.CommandLists = _page.CommandLists;
        confirmed.Name = _page.CinematicChapter!.Name;
        confirmed.CommonEvent = true;
        confirmed.Global = false;
        confirmed.Pages = [page];
        _sentEvent = confirmed;
        _phase = Phase.SavingEvent;
        Changed?.Invoke();
        _save(confirmed);
    }

    private static EventDescriptor CopyEvent(Guid id, string json)
    { var result = new EventDescriptor(id); result.Load(json); return result; }
}
