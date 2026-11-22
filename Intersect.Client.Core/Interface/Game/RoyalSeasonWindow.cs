using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network.Packets.RoyalSeasons;
using static Intersect.Client.Interface.Game.NavalManagementWindow;

namespace Intersect.Client.Interface.Game;

internal sealed class RoyalSeasonWindow : Window
{
    private readonly ScrollControl _body;
    private readonly Label _status;
    private readonly Action<RoyalSeasonRequestPacket> _send;
    private readonly Button _previous, _next;
    private RoyalSeasonView _view = new();
    private Guid _selected;
    private long _pendingUntil, _refreshAt;
    internal static new string T(string text) => RoyalSeasonText.Get(text, ClientLanguages.Current);
    internal RoyalSeasonWindow(Canvas canvas, Action<RoyalSeasonRequestPacket>? send = null) : base(canvas, T("Royal seasons"), false, nameof(RoyalSeasonWindow))
    {
        _send = send ?? (packet => Networking.Network.SendPacket(packet));
        SetSize(Math.Min(900, canvas.Width - 24), Math.Min(700, canvas.Height - 36));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        var chrome = new MarketplaceWindow.MarketChrome(this, "SeasonsFrame", MarketplaceWindow.FrameStyle.Window) { DrawHeaderDivider = false };
        chrome.SetBounds(12, 8, Width - 24, Height - 35);
        _status = Text(this, "SeasonStatus", "", 24, 36, Width - 48, 48, 13);
        _status.WrappingBehavior = WrappingBehavior.Wrapped;
        _previous = Button(this, "PreviousSeason", T("Previous season"), 24, 90, (Width - 58) / 2, 32, () => Select(-1));
        _next = Button(this, "NextSeason", T("Next season"), 34 + (Width - 58) / 2, 90, (Width - 58) / 2, 32, () => Select(1));
        _body = new ScrollControl(this, "SeasonDetails") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = false };
        _body.SetBounds(24, 130, Width - 48, Math.Max(100, Height - 224));
        Shop.ShopWindow.ConfigureNativeScrollbar(_body);
        Button(this, "RefreshSeasons", T("Refresh"), 24, Height - 76, 150, 34, () => Send(SeasonAction.Refresh));
        Button(this, "CloseSeasons", T("Close"), Width - 174, Height - 76, 150, 34, Hide);
        Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Apply(RoyalSeasonView view, bool open)
    {
        _view = view; _pendingUntil = 0; _refreshAt = Environment.TickCount64 + 30000;
        if (!view.Seasons.Any(s => s.Id == _selected)) _selected = view.Seasons.FirstOrDefault(s => s.IsActive(view.Now))?.Id ?? view.Seasons.FirstOrDefault()?.Id ?? Guid.Empty;
        RenderState(); if (open) { Show(); BringToFront(); }
    }
    internal void UpdateState()
    {
        if (!IsVisibleInTree) return;
        if (_pendingUntil > 0 && Environment.TickCount64 >= _pendingUntil)
        { _pendingUntil = 0; _view.Message = "No response. Refresh before trying again."; RenderState(); }
        if (_pendingUntil == 0 && Environment.TickCount64 >= _refreshAt)
        { _refreshAt = Environment.TickCount64 + 30000; Send(SeasonAction.Refresh); }
    }
    private void Select(int delta)
    {
        var index = Array.FindIndex(_view.Seasons, s => s.Id == _selected);
        index += delta;
        if (index >= 0 && index < _view.Seasons.Length) { _selected = _view.Seasons[index].Id; RenderState(); }
    }
    private void Send(SeasonAction action, Guid tier = default)
    {
        if (_pendingUntil > Environment.TickCount64) return;
        _pendingUntil = Environment.TickCount64 + 15000;
        _send(new() { Action = action, SeasonId = _selected, TierId = tier });
        RenderState();
    }
    private void RenderState()
    {
        var scroll = _body.VerticalScrollBar.ScrollAmount;
        _body.DeleteAllChildren();
        _status.Text = _pendingUntil > 0 ? T("Waiting for the server...") : !_view.Enabled ? T("Royal seasons are paused.") :
            string.IsNullOrEmpty(_view.Message) ? T("Complete objectives to unlock free rewards and Premium bonuses.") : T(_view.Message);
        var index = Array.FindIndex(_view.Seasons, s => s.Id == _selected);
        _previous.IsDisabled = index <= 0; _next.IsDisabled = index < 0 || index >= _view.Seasons.Length - 1;
        if (index < 0) { Text(_body, "NoSeason", T("No season is currently available."), 4, 4, _body.Width - 28, 52, 14); return; }
        var season = _view.Seasons[index];
        var progress = _view.Profile.Seasons.GetValueOrDefault(season.Id) ?? new();
        var width = _body.Width - 30; var y = 4;
        void Line(string name, string text, int height = 36, int font = 13)
        {
            var label = Text(_body, name, text, 4, y, width, height, font);
            var lines = text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(Skin.Renderer.MeasureText(label.Font, font, line).X / (double)Math.Max(1, width - 16))));
            height = Math.Max(height, lines * (font + 6) + 8);
            label.SetSize(width, height); label.WrappingBehavior = WrappingBehavior.Wrapped; y += height + 6;
        }
        Line("SeasonName", ClientLanguages.ContentText(season.Name), 42, 20);
        Line("SeasonDescription", ClientLanguages.ContentText(season.Description), 60);
        Line("SeasonDates", $"{T("Season dates (UTC)")}: {season.StartsAt.UtcDateTime:yyyy-MM-dd HH:mm} — {season.EndsAt.UtcDateTime:yyyy-MM-dd HH:mm}", 48);
        Line("ClaimDeadline", $"{T("Claim rewards before (UTC)")}: {season.EndsAt.AddDays(season.ClaimGraceDays).UtcDateTime:yyyy-MM-dd HH:mm}", 36);
        Line("SeasonPoints", $"{T("Season points")}: {RoyalSeasonRules.Points(season, progress)} | {T(_view.Premium ? "Premium active" : "Free track")}");
        Line("ObjectivesHeading", T("Objectives"), 34, 16);
        foreach (var objective in season.Objectives)
            Line("Objective" + objective.Id, $"{ClientLanguages.ContentText(objective.Name)}: {Math.Clamp(progress.Objectives.GetValueOrDefault(objective.Id), 0, objective.Required)}/{objective.Required} (+{objective.Points} {T("points")})", 48);
        Line("RewardsHeading", T("Rewards"), 34, 16);
        foreach (var tier in season.Tiers)
        {
            Line("Tier" + tier.Id, $"{ClientLanguages.ContentText(tier.Name)} — {tier.Points} {T("points")}", 40, 15);
            foreach (var premium in new[] { false, true })
            {
                var reward = premium ? tier.Premium : tier.Free;
                if (reward.ItemId == Guid.Empty) continue;
                var claimed = (premium ? progress.ClaimedPremium : progress.ClaimedFree).Contains(tier.Id);
                Line("Reward" + tier.Id + premium, $"{T(premium ? "Premium bonus" : "Free reward")}: {ClientLanguages.ContentText(_view.ItemNames.GetValueOrDefault(reward.ItemId) ?? "")} ×{reward.Quantity}", 42);
                var can = _view.Enabled && RoyalSeasonRules.CanClaim(season, progress, tier, premium, _view.Premium, _view.Now);
                var caption = claimed ? "Received" : premium && !_view.Premium ? "Premium required" : can ? "Claim reward" : "Locked";
                var button = Button(_body, "Claim" + tier.Id + premium, T(caption), 4, y, width, 32,
                    () => Send(premium ? SeasonAction.ClaimPremium : SeasonAction.ClaimFree, tier.Id));
                button.IsDisabled = !can || _pendingUntil > 0; y += 44;
            }
        }
        _body.UpdateScrollBars();
        _body.VerticalScrollBar.ScrollAmount = scroll;
    }
}
