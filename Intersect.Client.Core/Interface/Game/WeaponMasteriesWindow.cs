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
    private readonly Label _experienceHeader;
    private readonly MasteryExperienceBar _experienceBar;
    private readonly Label _status;
    private readonly Label _detail;
    private readonly Button _confirm;
    private readonly Button _cancel;
    private readonly Button _respec;
    private readonly Button _clearQuickbarSlot;
    private readonly Label _quickbarHint;
    private readonly List<Button> _quickbarSlots = [];
    private Dictionary<string, string[]> _loadouts = [];
    private int _selectedSlot;
    private Guid _slotSaveRequest;
    private bool _readOnly, _pendingRespec;
    private string _owner = "";
    private int _quotedCost;
    private Guid _quotedCurrency;
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
        _header = NavalManagementWindow.Text(this, "MasteryHeader", "", 16, 38, 720, 26, 11);
        _experienceHeader = NavalManagementWindow.Text(this, "MasteryExperienceHeader", "", 16, 68, 720, 26, 11);
        _experienceBar = new MasteryExperienceBar(this); _experienceBar.SetBounds(16,98,720,12);
        _categories = Scroll(16, 120, 180, 280); _talents = Scroll(208, 120, 528, 248);
        for (var index = 0; index < WeaponSkillLoadout.SlotCount; index++)
        {
            var slot = index;
            _quickbarSlots.Add(NavalManagementWindow.Button(this, "WeaponQuickbarSlot" + index,
                (index + 1).ToString(), 208 + index * 52, 376, 48, 32, () => { _selectedSlot = slot; DrawTalents(); }));
        }
        _quickbarHint = NavalManagementWindow.Text(this, "WeaponQuickbarHint", T("Choose a slot, then add a learned skill."), 208, 414, 528, 32, 10);
        _clearQuickbarSlot = NavalManagementWindow.Button(this, "ClearWeaponQuickbarSlot", T("Clear selected slot"),
            16, 414, 180, 32, () => SaveQuickbarSlot(""));
        _status = NavalManagementWindow.Text(this, "MasteryStatus", "", 16, 454, 720, 24, 10);
        _detail = NavalManagementWindow.Text(this, "MasteryConfirmationDetail", "", 16, 480, 720, 22, 10);
        _confirm = NavalManagementWindow.Button(this, "ConfirmTalent", T("Confirm"), 208, 510, 252, 30, Confirm);
        _cancel = NavalManagementWindow.Button(this, "CancelTalent", T("Cancel"), 472, 510, 264, 30, Cancel);
        _respec = NavalManagementWindow.Button(this, "ResetMastery", T("Reset talents"), 16, 510, 180, 30, () => {
            if (_readOnly || !_config.RespecEnabled) return;
            _pendingCategory = _selected; _pendingTalent = ""; _pendingRespec = true;
            _quotedCost = _config.RespecCost; _quotedCurrency = _config.RespecCurrencyId;
            _status.Text = ClientLanguages.Format("Reset cost: {0} {1}", _quotedCost, Name(Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.GetName(_quotedCurrency)));
            _detail.Text = ClientLanguages.Format("Reset {0}? Experience is preserved.", Name(_config.Masteries.First(c => c.Key == _selected).Name));
            _confirm.Show(); _cancel.Show();
        });
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
        if (_readOnly && !packet.Open && !packet.MasteriesReadOnly) return;
        Cancel(); _readOnly = packet.MasteriesReadOnly; _owner = packet.MasteriesOwner;
        _slotSaveRequest = Guid.Empty;
        if (packet.MasteriesJson.Length == 0)
        {
            foreach (var slot in _quickbarSlots) slot.Hide();
            _clearQuickbarSlot.Hide(); _quickbarHint.Hide();
            foreach (var row in _categoryRows.Concat(_talentRows)) { row.Parent = null; row.Dispose(); }
            _categoryRows.Clear(); _talentRows.Clear(); _respec.Hide(); _header.Text = _experienceHeader.Text = ""; _experienceBar.IsHidden=true; _status.Text = T(packet.Message);
            if (packet.Open) { Show(); BringToFront(); } return;
        }
        _config = RoyalCombatConfiguration.FromJson(packet.ConfigurationJson);
        if (!_readOnly && packet.WeaponSkillSlotsJson.Length > 0)
            _loadouts = JsonSerializer.Deserialize<Dictionary<string, string[]>>(packet.WeaponSkillSlotsJson) ?? [];
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
        var slots = WeaponSkillLoadout.Resolve(category, total, ranks, _loadouts.GetValueOrDefault(category.Key));
        for (var index = 0; index < _quickbarSlots.Count; index++)
        {
            var button = _quickbarSlots[index];
            button.IsHidden = _readOnly;
            button.IsDisabled = _readOnly || _slotSaveRequest != Guid.Empty;
            button.TextColorOverride = index == _selectedSlot ? new Color(255, 255, 218, 89) : Color.White;
            button.TooltipText = ClientLanguages.Format("Weapon skill slot {0}", index + 1) + " · " +
                (category.Talents.FirstOrDefault(t => t.Key == slots[index]) is { } assigned ? Name(assigned.Name) : T("Empty slot"));
        }
        _clearQuickbarSlot.IsDisabled = _readOnly || _slotSaveRequest != Guid.Empty || slots[_selectedSlot].Length == 0;
        _clearQuickbarSlot.IsHidden = _quickbarHint.IsHidden = _readOnly;
        var points = Math.Max(0, MasteryRules.Points(category, total) - MasteryRules.Spent(category, ranks));
        _respec.IsHidden = _readOnly || !_config.RespecEnabled;
        _respec.IsDisabled = MasteryRules.Spent(category, ranks) <= 0;
        if (_readOnly) _status.Text = ClientLanguages.Format("Masteries of {0} · Read only", _owner);
        var xp = category.WithinLevel(total); var required = level == category.Cap ? 0 : category.Required(level);
        _header.Text = ClientLanguages.Format("{0} · Level {1}/{2} · Points {3}", Name(category.Name), level, category.Cap, points);
        _experienceHeader.Text = ClientLanguages.Format("XP {0}/{1} · {2}% · Remaining {3}", xp, required, required == 0 ? 100 : (int)(xp * 100 / required), Math.Max(0, required - xp));
        _experienceBar.IsHidden=false; _experienceBar.Ratio = required==0?1f:Math.Clamp(xp/(float)required,0,1);
        foreach (var row in _talentRows) { row.Parent = null; row.Dispose(); } _talentRows.Clear();
        foreach (var talent in category.Talents)
        {
            var rank = Math.Clamp(ranks.GetValueOrDefault(talent.Key), 0, talent.MaxRank);
            var row = new Base(_talents, "Talent" + talent.Key); row.SetBounds(0, _talentRows.Count * 114, 502, 108); _talentRows.Add(row);
            NavalManagementWindow.Text(row, "TalentName", ClientLanguages.Format("{0} · Rank {1}/{2} · Level {3}", Name(talent.Name), rank, talent.MaxRank, talent.RequiredLevel), 0, 0, 500, 26, 11);
            var effect = ClientLanguages.Format("Damage +{0}% · Speed +{1}% · Critical +{2}", talent.DamagePercentPerRank, talent.AttackSpeedPercentPerRank, talent.CritChancePerRank);
            if(talent.Skill.Enabled)effect=ClientLanguages.Format("Weapon skill · Damage {0}% · Range {1} · Cooldown {2}s",
                talent.Skill.Power(Math.Max(1,rank)),talent.Skill.Range,talent.Skill.CooldownMilliseconds/1000f);
            if (talent.RankSpellIds.Length > 0) effect = Name(SpellDescriptor.GetName(talent.RankSpellIds[Math.Min(rank, talent.RankSpellIds.Length - 1)]));
            if (!talent.Configured) effect = T("Talent unavailable.");
            if (talent.Prerequisite.Length > 0) effect += " · " + ClientLanguages.Format("Requires {0} rank {1}",
                Name(category.Talents.First(t => t.Key == talent.Prerequisite).Name), talent.PrerequisiteRank);
            NavalManagementWindow.Text(row, "TalentEffect", effect, 0, 27, 500, 34, 10);
            var buy = NavalManagementWindow.Button(row, "LearnTalent", T("Spend 1 mastery point"), 0, 68, 246, 30, () => {
                _pendingCategory = category.Key; _pendingTalent = talent.Key;
                _status.Text = ClientLanguages.Format("Spend 1 point on {0}? This choice is saved on your character.", Name(talent.Name));
                _confirm.Show(); _cancel.Show();
            });
            buy.IsDisabled = _readOnly || !MasteryRules.CanBuy(category, talent, total, ranks);
            if (talent.Skill.Enabled)
            {
                var assign = NavalManagementWindow.Button(row, "AssignWeaponSkill", ClientLanguages.Format("Add to slot {0}", _selectedSlot + 1),
                    254, 68, 246, 30, () => SaveQuickbarSlot(talent.Key));
                assign.IsDisabled = _readOnly || _slotSaveRequest != Guid.Empty || !WeaponSkillLoadout.Available(category, talent, total, ranks);
            }
        }
    }
    private void SaveQuickbarSlot(string talent)
    {
        if (_readOnly || _slotSaveRequest != Guid.Empty) return;
        _slotSaveRequest = Guid.NewGuid();
        Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.SetWeaponSkillSlot,
            CategoryKey = _selected, TalentKey = talent, WeaponSkillSlot = _selectedSlot, RequestId = _slotSaveRequest });
        DrawTalents();
    }

    internal bool ApplyQuickbarError(ArcheryStatePacket packet)
    {
        if (_slotSaveRequest == Guid.Empty || packet.RequestId != _slotSaveRequest) return false;
        _slotSaveRequest = Guid.Empty; _status.Text = T(packet.Message); DrawTalents(); return true;
    }
    private void Confirm()
    {
        if (_readOnly) return;
        if (_pendingRespec)
        {
            Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Respec, RequestId = Guid.NewGuid(),
                CategoryKey = _pendingCategory, ExpectedCost = _quotedCost, ItemId = _quotedCurrency }); Cancel(); return;
        }
        if (_pendingTalent.Length == 0) return;
        Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.BuyTalent, RequestId = Guid.NewGuid(),
            CategoryKey = _pendingCategory, TalentKey = _pendingTalent });
        Cancel();
    }
    private void Cancel() { _pendingRespec = false; _pendingTalent = _pendingCategory = ""; _confirm.Hide(); _cancel.Hide(); _status.Text = _detail.Text = ""; }
}

internal sealed class MasteryExperienceBar(Base parent) : Base(parent,"MasteryExperienceBar")
{
    internal float Ratio { get; set; }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        skin.Renderer.DrawColor=new Color(255,35,29,24);skin.Renderer.DrawFilledRect(RenderBounds);
        skin.Renderer.DrawColor=new Color(255,200,159,70);
        skin.Renderer.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(2,2,(int)((Width-4)*Math.Clamp(Ratio,0,1)),Height-4));
        skin.Renderer.DrawColor=new Color(255,234,204,135);skin.Renderer.DrawLinedRect(RenderBounds);
    }
}
