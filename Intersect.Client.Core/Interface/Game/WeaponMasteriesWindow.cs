using System.Text.Json;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Archery;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;
internal sealed class WeaponMasteriesWindow : Window
{
    private readonly ScrollControl _categories;
    private readonly ScrollControl _talents;
    private readonly Label _header;
    private readonly Label _status;
    private readonly Button _confirm;
    private readonly Button _cancel;
    private readonly List<Base> _categoryRows = [], _talentRows = [];
    private RoyalCombatConfiguration _config = new();
    private Dictionary<string, long> _experience = [];
    private Dictionary<string, Dictionary<string, int>> _ranks = [];
    private string _selected = "", _pendingCategory = "", _pendingTalent = "";
    private static string T(string text) => ClientLanguages.Text(text);
    private static string Name(string name) => ClientLanguages.ContentText(T(name));
    internal WeaponMasteriesWindow(Canvas canvas) : base(canvas, T("Weapon Masteries"), false, nameof(WeaponMasteriesWindow))
    {
        SetSize(760, 590); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _header = NavalManagementWindow.Text(this, "MasteryHeader", "", 16, 38, 720, 56, 11);
        _categories = Scroll(16, 104, 180, 340); _talents = Scroll(208, 104, 528, 340);
        _status = NavalManagementWindow.Text(this, "MasteryStatus", "", 16, 454, 720, 44, 10);
        _confirm = NavalManagementWindow.Button(this, "ConfirmTalent", T("Confirm"), 208, 510, 252, 30, Confirm);
        _cancel = NavalManagementWindow.Button(this, "CancelTalent", T("Cancel"), 472, 510, 264, 30, Cancel);
        Cancel(); Hide();
    }
    protected override void EnsureInitialized() { }
    private ScrollControl Scroll(int x, int y, int width, int height)
    {
        var scroll = new ScrollControl(this) { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        scroll.SetBounds(x, y, width, height); return scroll;
    }
    internal void Apply(ArcheryStatePacket packet)
    {
        _config = RoyalCombatConfiguration.FromJson(packet.ConfigurationJson);
        _experience = JsonSerializer.Deserialize<Dictionary<string, long>>(packet.MasteriesJson) ?? [];
        _ranks = packet.MasteryRanksJson.Length == 0 ? [] : JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(packet.MasteryRanksJson) ?? [];
        if (!_config.Masteries.Any(c => c.Key == _selected)) _selected = _config.Masteries[0].Key;
        foreach (var row in _categoryRows) { row.Parent = null; row.Dispose(); } _categoryRows.Clear();
        foreach (var category in _config.Masteries)
        {
            var button = NavalManagementWindow.Button(_categories, "MasteryCategory" + category.Key, Name(category.Name), 0,
                _categoryRows.Count * 38, 156, 32, () => { _selected = category.Key; Cancel(); DrawTalents(); });
            _categoryRows.Add(button);
        }
        DrawTalents();
        if (packet.Message.Length > 0) { Cancel(); _status.Text = T(packet.Message); }
        if (packet.Open) { Show(); BringToFront(); }
    }
    private void DrawTalents()
    {
        var category = _config.Masteries.First(c => c.Key == _selected);
        var total = _experience.GetValueOrDefault(category.Key); var level = category.Level(total);
        var ranks = _ranks.GetValueOrDefault(category.Key) ?? new();
        var points = Math.Max(0, MasteryRules.Points(category, total) - MasteryRules.Spent(category, ranks));
        var xp = category.WithinLevel(total); var required = level == category.Cap ? 0 : category.Required(level);
        _header.Text = ClientLanguages.Format("{0} · Level {1}/{2} · Points {3}", Name(category.Name), level, category.Cap, points) + "\n" +
            ClientLanguages.Format("XP {0}/{1} · {2}% · Remaining {3}", xp, required, required == 0 ? 100 : (int)(xp * 100 / required), Math.Max(0, required - xp));
        foreach (var row in _talentRows) { row.Parent = null; row.Dispose(); } _talentRows.Clear();
        foreach (var talent in category.Talents)
        {
            var rank = Math.Clamp(ranks.GetValueOrDefault(talent.Key), 0, talent.MaxRank);
            var row = new Base(_talents, "Talent" + talent.Key); row.SetBounds(0, _talentRows.Count * 114, 502, 108); _talentRows.Add(row);
            NavalManagementWindow.Text(row, "TalentName", ClientLanguages.Format("{0} · Rank {1}/{2} · Level {3}", Name(talent.Name), rank, talent.MaxRank, talent.RequiredLevel), 0, 0, 500, 26, 11);
            var effect = ClientLanguages.Format("Damage +{0}% · Speed +{1}% · Critical +{2}", talent.DamagePercentPerRank, talent.AttackSpeedPercentPerRank, talent.CritChancePerRank);
            if (talent.RankSpellIds.Length > 0) effect = Name(SpellDescriptor.GetName(talent.RankSpellIds[Math.Min(rank, talent.RankSpellIds.Length - 1)]));
            if (!talent.Configured) effect = T("Map an existing spell in the editor to enable this talent.");
            if (talent.Prerequisite.Length > 0) effect += " · " + ClientLanguages.Format("Requires {0} rank {1}",
                Name(category.Talents.First(t => t.Key == talent.Prerequisite).Name), talent.PrerequisiteRank);
            NavalManagementWindow.Text(row, "TalentEffect", effect, 0, 27, 500, 34, 10);
            var buy = NavalManagementWindow.Button(row, "LearnTalent", T("Spend 1 mastery point"), 0, 68, 500, 30, () => {
                _pendingCategory = category.Key; _pendingTalent = talent.Key;
                _status.Text = ClientLanguages.Format("Spend 1 point on {0}? This choice is saved on your character.", Name(talent.Name));
                _confirm.Show(); _cancel.Show();
            });
            buy.IsDisabled = !MasteryRules.CanBuy(category, talent, total, ranks);
        }
    }
    private void Confirm()
    {
        if (_pendingTalent.Length == 0) return;
        Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.BuyTalent, RequestId = Guid.NewGuid(),
            CategoryKey = _pendingCategory, TalentKey = _pendingTalent });
        Cancel();
    }
    private void Cancel() { _pendingTalent = _pendingCategory = ""; _confirm.Hide(); _cancel.Hide(); _status.Text = ""; }
}
