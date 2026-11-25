using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Newtonsoft.Json;
using static Intersect.Client.Interface.Game.NavalManagementWindow;

namespace Intersect.Client.Interface.Game;

internal sealed class NavalExpeditionWindow : Window
{
    private readonly ScrollControl _list;
    private readonly Label _message, _clock;
    private readonly Button _ready, _start, _leave, _claim;
    private NavalExpeditionView _state = new();
    private NavalExpeditionDefinition[] _available = [];
    private string _signature = "";
    private bool _confirmLeave;
    private readonly Action<NavalExpeditionRequestPacket> _send;

    internal NavalExpeditionWindow(Canvas parent, Action<NavalExpeditionRequestPacket>? send = null) : base(parent, T("Naval expeditions"), false, nameof(NavalExpeditionWindow))
    {
        _send = send ?? (packet => Networking.Network.SendPacket(packet));
        SetSize(Math.Min(740, parent.Width - 24), Math.Min(660, parent.Height - 32));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        var frame = new MarketplaceWindow.MarketChrome(this, "ExpeditionFrame", MarketplaceWindow.FrameStyle.Window);
        frame.SetBounds(12, 8, Width - 24, Height - 35);
        _message = Text(this, "ExpeditionMessage", "", 24, 34, Width - 48, 48, 11);
        _message.WrappingBehavior = WrappingBehavior.Wrapped;
        _clock = Text(this, "ExpeditionTime", "", 24, 84, Width - 48, 28);
        _list = new ScrollControl(this, "ExpeditionList") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = false };
        _list.SetBounds(24, 118, Width - 48, Height - 255);
        Shop.ShopWindow.ConfigureNativeScrollbar(_list);
        var half = (Width - 58) / 2;
        _ready = Button(this, "ExpeditionReady", "Ready for departure", 24, Height - 126, half, 34, () => Request(NavalExpeditionAction.Ready, _state.RunId));
        _start = Button(this, "ExpeditionStart", "Depart together", 34 + half, Height - 126, half, 34, () => Request(NavalExpeditionAction.Start, _state.RunId));
        _claim = Button(this, "ExpeditionClaim", "Claim my reward", 24, Height - 126, Width - 48, 34, () => Request(NavalExpeditionAction.Claim, _state.RunId));
        _leave = Button(this, "ExpeditionLeave", "Abandon expedition", 24, Height - 82, half, 34, () =>
        {
            if (_state.Status == NavalExpeditionStatus.Failed || _confirmLeave) { Request(NavalExpeditionAction.Leave, _state.RunId); _confirmLeave = false; }
            else { _confirmLeave = true; RefreshActions(); }
        });
        Button(this, "CloseExpedition", "Close", 34 + half, Height - 82, half, 34, () => { _confirmLeave = false; Hide(); });
        Closed += (_, _) => { _confirmLeave = false; RefreshActions(); };
        Hide(); RefreshActions();
    }

    protected override void EnsureInitialized() { }
    private void Request(NavalExpeditionAction action, Guid id) => _send(new() { Action = action, Id = id });
    internal static void Send(NavalExpeditionAction action, Guid id = default) => Networking.Network.SendPacket(new NavalExpeditionRequestPacket { Action = action, Id = id });
    internal static string Content(string source) => ClientLanguages.ContentText(source);
    internal void Apply(NavalExpeditionView view, bool open)
    {
        if (_state.RunId != view.RunId || _state.Status != view.Status) _confirmLeave = false;
        _state = view;
        if (open) _available = view.Available;
        _clock.Text = view.RunId == Guid.Empty ? T("Choose an expedition for your group.") :
            T(view.Status.ToString()) + " · " + string.Format(T("Time remaining: {0}:{1}"), view.RemainingSeconds / 60, (view.RemainingSeconds % 60).ToString("00"));
        if (open || !string.IsNullOrEmpty(view.Message)) _message.Text = T(view.Message);
        if (string.IsNullOrEmpty(_message.Text)) _message.Text = T("Gather your party at a port. Everyone confirms readiness before the leader departs.");
        // Timers do not rebuild the scrollable roster or steal a pending click.
        var signature = ClientLanguages.Current + JsonConvert.SerializeObject(new { view.RunId, view.Definition, view.Status, view.Members, view.StageIndex, view.Progress, view.Required, view.StageMapName, view.StageTargetName, view.RewardItemName, _available });
        if (signature != _signature) { _signature = signature; Rebuild(); }
        RefreshActions();
        if (open) { Show(); BringToFront(); }
    }

    private void RefreshActions()
    {
        var preparing = _state.RunId != Guid.Empty && _state.Status == NavalExpeditionStatus.Preparing;
        _ready.IsHidden = _start.IsHidden = !preparing;
        _ready.Text = T(_state.Members.Any(m => m.PlayerId == Globals.Me?.Id && m.Ready) ? "Not ready" : "Ready for departure");
        _start.IsDisabled = !_state.CanStart;
        _claim.IsHidden = !_state.CanClaim;
        _leave.IsHidden = _state.RunId == Guid.Empty || _state.Status == NavalExpeditionStatus.Completed;
        _leave.Text = T(_state.Status == NavalExpeditionStatus.Failed ? "Dismiss result" : _confirmLeave ? "Confirm abandon for everyone" : "Abandon expedition");
    }

    private void Rebuild()
    {
        var scroll = _list.VerticalScrollBar.ScrollAmount;
        _list.DeleteAllChildren(); var y = 4; var width = _list.Width - 30;
        void Line(string id, string text, int height = 30, int size = 12)
        {
            var label = Text(_list, id, text, 6, y, width, height, size);
            var lines = text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(Skin.Renderer.MeasureText(label.Font, size, line).X / (double)Math.Max(1, width - 20))));
            height = Math.Max(height, (lines + 1) * (size + 5)); label.SetSize(width, height);
            label.WrappingBehavior = WrappingBehavior.Wrapped; y += height + 6;
        }
        if (_state.RunId == Guid.Empty)
        {
            if (_available.Length == 0) Line("NoExpeditions", T("No expeditions are enabled. Configure them in the Naval System Editor."), 60);
            foreach (var definition in _available)
            {
                Line("Name" + definition.Id, Content(definition.Name), 32, 15);
                if (!string.IsNullOrWhiteSpace(definition.Description)) Line("Description" + definition.Id, Content(definition.Description), 80);
                Line("Limits" + definition.Id, Limits(definition), 58);
                Line("Reward" + definition.Id, string.Format(T("Reward per participant: {0} Sailing EXP"), definition.SailingExperienceReward) +
                    (definition.RewardItemId == Guid.Empty ? "" : " · " + Content(Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(definition.RewardItemId)?.Name ?? "") + " ×" + definition.RewardQuantity), 55);
                Button(_list, "Propose" + definition.Id, "Propose to my group", 6, y, width, 34, () => Request(NavalExpeditionAction.Propose, definition.Id)); y += 54;
            }
        }
        else if (_state.Definition is {} definition)
        {
            Line("RunName", Content(definition.Name), 36, 16);
            Line("RunLimits", Limits(definition), 58);
            if (_state.Status == NavalExpeditionStatus.Active && definition.Stages.ElementAtOrDefault(_state.StageIndex) is {} stage)
            {
                Line("CurrentObjective", string.Format(T("Stage {0}/{1}: {2} · {3}/{4}"), _state.StageIndex + 1, definition.Stages.Length, T(stage.Objective.ToString()), _state.Progress, _state.Required), 54, 14);
                Line("Destination", stage.Objective is NavalExpeditionObjective.ReachPoint or NavalExpeditionObjective.ReturnToPort
                    ? string.Format(T("Destination: {0} · ({1}, {2}) · radius {3}"), Content(_state.StageMapName), stage.X, stage.Y, stage.Radius)
                    : Content(_state.StageMapName) + " · " + Content(_state.StageTargetName), 45);
                if (!string.IsNullOrEmpty(stage.Description)) Line("ObjectiveDescription", Content(stage.Description), 80);
            }
            Line("RunReward", string.Format(T("Reward per participant: {0} Sailing EXP"), definition.SailingExperienceReward) +
                (definition.RewardItemId == Guid.Empty ? "" : " · " + Content(_state.RewardItemName) + " ×" + definition.RewardQuantity), 55);
            Line("CrewHeading", T("Expedition participants"), 30, 14);
            var ships = _state.Members.Select(m => m.ShipId).Distinct().ToArray();
            foreach (var member in _state.Members)
                Line("Member" + member.PlayerId, member.Name + " · " + string.Format(T("Ship {0}"), Array.IndexOf(ships, member.ShipId) + 1) + " · " +
                    T(member.Claimed ? "Reward received" : !member.Present ? "Rejoining group" : member.Ready ? "Ready" : "Not ready"), 36);
            Line("RouteHeading", T("Expedition stages"), 30, 14);
            for (var i = 0; i < definition.Stages.Length; i++)
                Line("Route" + i, $"{i + 1}. " + T(definition.Stages[i].Objective.ToString()) +
                    (i < _state.StageIndex ? " · " + T("Completed") : ""), 36);
            Line("CoopHelp", T("Stay together: all registered ships must reach each destination and remain near naval victories. A missing participant pauses progress until they return."), 100);
        }
        _list.UpdateScrollBars(); _list.VerticalScrollBar.ScrollAmount = scroll;
    }

    private static string Limits(NavalExpeditionDefinition definition) => T(definition.Mode.ToString()) + " · " +
        string.Format(T("{0}–{1} players · up to {2} ships · Sailing {3} · {4} min"), definition.MinimumPlayers, definition.MaximumPlayers,
            definition.Mode == NavalExpeditionMode.SingleShip ? 1 : definition.MaximumShips, definition.RequiredSailingLevel, definition.TimeLimitMinutes);
}
