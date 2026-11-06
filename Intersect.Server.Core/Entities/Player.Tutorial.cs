using Intersect.Framework.Core.Tutorials;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Network.Packets.Tutorials;
using Intersect.Server.Tutorials;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public partial class Player
{
    internal static readonly Guid TutorialProgressId = new("c1026029-0000-4000-8000-999999999999");
    private TutorialProgress? _tutorial;
    private bool _tutorialSent, _tutorialMoved, _tutorialCombatReady;
    private Guid _tutorialStep, _tutorialMap;
    private string _tutorialRevision = "";
    private int _tutorialX, _tutorialY;
    private long _tutorialPoll, _tutorialRequest;
    private TutorialProgress Tutorial
    {
        get
        {
            if (_tutorial != null) return _tutorial;
            var json = GetVariable(TutorialProgressId)?.Value.String;
            try { _tutorial = string.IsNullOrEmpty(json) ? new() : JsonConvert.DeserializeObject<TutorialProgress>(json) ?? new(); }
            catch (JsonException) { _tutorial = new(); }
            _tutorial.Completed ??= [];
            return _tutorial;
        }
    }
    internal void ResetTutorialSession()
    {
        _tutorialSent = false; _tutorialStep = Guid.Empty; _tutorialMoved = false; _tutorialCombatReady = false;
        _tutorialPoll = 0; _tutorialRequest = 0; _tutorialRevision = "";
    }
    /// <summary>
    /// Called only by validated server combat outcomes; client packets cannot
    /// mark a combat exercise complete. Only the active step can gain credit.
    /// </summary>
    private void RecordAdvancedCombatTutorialAction(TutorialObjective action)
    {
        if (!AdvancedCombatTutorialLessons.IsCombatObjective(action) ||
            !InGame || IsDead || HasPendingDeath ||
            !Options.Instance.Combat.EnableAdvancedCombat ||
            !TutorialRuntime.Current.Enabled || Tutorial.Paused || Tutorial.Finished)
            return;

        var current = Tutorial.Current(TutorialRuntime.Current);
        if (current == null || current.Objective != action)
            return;

        if (!AdvancedCombatTutorialLessons.IsAvailable(
            action, Options.Instance.Combat.EnableAdvancedCombat,
            Options.Instance.Combat.AdvancedPlayerCombosEnabled))
            return;

        // Actions are processed by the game server's serialized player lock.
        // Do not rely on _tutorialStep: this may be the player's first event
        // before their next 1-second tutorial refresh.
        if (current.Id != _tutorialStep)
            return;

        _tutorialCombatReady = true;
        Tutorial.Completed.Add(current.Id);
        if (Tutorial.Current(TutorialRuntime.Current) == null)
            Tutorial.Finished = true;
        SaveTutorial();
        SendTutorial();
    }

    private void SaveTutorial() => SetVariableValue(TutorialProgressId, JsonConvert.SerializeObject(Tutorial));
    private bool TutorialReady(TutorialStep step) => step.Objective switch
    {
        TutorialObjective.Read => true,
        TutorialObjective.Move => _tutorialMoved,
        TutorialObjective.ReachMap => MapId == step.MapId,
        // Authoritative check: arrival is measured by the server's player position,
        // never by a client-reported tutorial or marker packet.
        TutorialObjective.ReachTile => MapId == step.MapId &&
            Math.Abs((int)X - step.TargetX) <= step.TargetRadius &&
            Math.Abs((int)Y - step.TargetY) <= step.TargetRadius,
        TutorialObjective.Variable => GetVariable(step.VariableId)?.Value.Integer == step.VariableValue,
        TutorialObjective.CombatDodge or TutorialObjective.CombatPerfectParry
            or TutorialObjective.CombatCounterattack or TutorialObjective.CombatFinisher =>
            _tutorialCombatReady || !AdvancedCombatTutorialLessons.IsAvailable(
                step.Objective, Options.Instance.Combat.EnableAdvancedCombat,
                Options.Instance.Combat.AdvancedPlayerCombosEnabled),
        _ => false,
    };
    private void SendTutorial()
    {
        var config = TutorialRuntime.Current;
        var step = Tutorial.Current(config);
        if (step?.Id != _tutorialStep)
        {
            _tutorialStep = step?.Id ?? Guid.Empty; _tutorialMap = MapId; _tutorialX = X; _tutorialY = Y; _tutorialMoved = false; _tutorialCombatReady = false;
        }
        SendPacket(new TutorialStatePacket { ConfigurationJson = TutorialRuntime.Json, Revision = TutorialRuntime.Revision,
            StepId = step?.Id ?? Guid.Empty, Finished = Tutorial.Finished || step == null,
            Open = config.Enabled && !Tutorial.Paused && step != null, CanAdvance = step != null && TutorialReady(step) });
        _tutorialRevision = TutorialRuntime.Revision;
        _tutorialSent = true;
    }
    private void UpdateTutorial()
    {
        if (Environment.TickCount64 < _tutorialPoll) return;
        _tutorialPoll = Environment.TickCount64 + 1000;
        if (!_tutorialSent)
        {
            if (IsDead || IsBusy() || EventLookup.Values.Any(e => e.HoldingPlayer)) return;
            if (!TutorialRuntime.Current.AutoStart && GetVariable(TutorialProgressId) == null) { Tutorial.Paused = true; SaveTutorial(); }
            SendTutorial(); return;
        }
        if (_tutorialRevision != TutorialRuntime.Revision) { SendTutorial(); return; }
        if (Tutorial.Paused || Tutorial.Finished || !TutorialRuntime.Current.Enabled) return;
        var step = Tutorial.Current(TutorialRuntime.Current);
        if (step == null) { Tutorial.Finished = true; SaveTutorial(); SendTutorial(); return; }
        if (step.Id != _tutorialStep) { SendTutorial(); return; }
        if (step.Objective == TutorialObjective.Move && (MapId != _tutorialMap || X != _tutorialX || Y != _tutorialY)) _tutorialMoved = true;
        if (step.Objective != TutorialObjective.Read && TutorialReady(step) && !IsBusy() && !EventLookup.Values.Any(e => e.HoldingPlayer))
        { Tutorial.Completed.Add(step.Id); SaveTutorial(); SendTutorial(); }
    }
    internal void HandleTutorial(TutorialRequestPacket packet)
    {
        lock (EntityLock)
        {
            if (!InGame || !Enum.IsDefined(packet.Action) || Environment.TickCount64 < _tutorialRequest) return;
            _tutorialRequest = Environment.TickCount64 + 250;
            if (!TutorialRuntime.Current.Enabled) { SendTutorial(); return; }
            if (packet.Action == TutorialAction.Pause) { Tutorial.Paused = true; SaveTutorial(); SendTutorial(); return; }
            if (IsDead || IsBusy() || EventLookup.Values.Any(e => e.HoldingPlayer)) return;
            if (packet.Action == TutorialAction.Resume) { Tutorial.Paused = false; SaveTutorial(); SendTutorial(); return; }
            if (packet.Action == TutorialAction.Restart) { _tutorial = new(); _tutorialStep = Guid.Empty; _tutorialCombatReady = false; SaveTutorial(); SendTutorial(); return; }
            var step = Tutorial.Current(TutorialRuntime.Current);
            if (Tutorial.Paused || step == null || packet.StepId != step.Id || packet.Revision != TutorialRuntime.Revision) { SendTutorial(); return; }
            if (packet.Action == TutorialAction.PlayCinematic && step.CinematicEventId != Guid.Empty)
            { if (EventDescriptor.Get(step.CinematicEventId) is { CommonEvent: true } scene) TryRunMenuEvent(scene); return; }
            if (packet.Action == TutorialAction.Next && TutorialReady(step))
            { Tutorial.Completed.Add(step.Id); if (Tutorial.Current(TutorialRuntime.Current) == null) Tutorial.Finished = true; SaveTutorial(); SendTutorial(); }
        }
    }
}
