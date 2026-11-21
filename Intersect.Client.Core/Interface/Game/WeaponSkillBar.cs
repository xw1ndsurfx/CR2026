using System.Text.Json;
using Intersect.Client.Core.Controls;
using Intersect.Client.Framework.Input;
using Keys = Intersect.Client.Framework.GenericClasses.Keys;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Configuration;
using Intersect.Client.Framework.Gwen;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

/// <summary>Combat techniques have their own controls; they never occupy spellbook slots.</summary>
internal sealed class WeaponSkillBar : Base
{
    private readonly Label _message;
    private readonly Func<bool> _canShow;
    private readonly Func<Rect>? _hotbarBounds;
    private readonly Dictionary<Label, (string Text, int Width, IFont Font, string Family, int Size, float Scale, int Ink)> _textFits = [];
    private string _messageSource = "Weapon skills";
    private long _noticeUntil;
    private readonly List<(Button Button,MasteryCategory Category,MasteryTalent? Talent)> _buttons=[];
    private RoyalCombatConfiguration _config=new();
    private Dictionary<string,long> _experience=[];
    private Dictionary<string,Dictionary<string,int>> _ranks=[];
    private readonly Dictionary<string,long> _cooldowns=[];
    private Dictionary<string, string[]> _loadouts = [];
    private readonly Dictionary<Button, Label> _keyLabels = [];
    private Guid _weapon;
    private long _pendingUntil;
    internal bool HasMasteryState { get; private set; }

    internal WeaponSkillBar(Canvas canvas, Func<bool> canShow, Func<Rect>? hotbarBounds = null):base(canvas,nameof(WeaponSkillBar))
    {
        _canShow = canShow;
        _hotbarBounds = hotbarBounds;
        ShouldDrawBackground = false;
        _message = new InkAlignedLabel(this, "WeaponSkillMessage") { Font = Skin.DefaultFont,
            Text = ClientLanguages.Text("Weapon skills"), AutoSizeToContents = false, IsHidden = true,
            MouseInputEnabled = false, KeyboardInputEnabled = false, TextAlign = Pos.Left | Pos.CenterV };
        _message.TextColorOverride = new Color(255, 255, 222, 134);
        _message.FontSize = 14;
        Hide();
    }
    internal void Apply(ArcheryStatePacket state)
    {
        if(state.MasteriesReadOnly)return;
        if(state.MasteriesJson.Length>0)
        {
            HasMasteryState=true;
            _config=RoyalCombatConfiguration.FromJson(state.ConfigurationJson);
            _experience=JsonSerializer.Deserialize<Dictionary<string,long>>(state.MasteriesJson)??[];
            _ranks=JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,int>>>(state.MasteryRanksJson)??[];
            if (state.WeaponSkillSlotsJson.Length > 0)
                _loadouts = JsonSerializer.Deserialize<Dictionary<string, string[]>>(state.WeaponSkillSlotsJson) ?? [];
            Rebuild(EquippedWeapon());
        }
        if(state.WeaponSkillCooldownsJson.Length>0)
        {
            _cooldowns.Clear();
            foreach(var entry in JsonSerializer.Deserialize<Dictionary<string,long>>(state.WeaponSkillCooldownsJson)??[])
                _cooldowns[entry.Key]=Environment.TickCount64+Math.Clamp(entry.Value,0,120000);
            _pendingUntil=0;
            _messageSource = state.Message.Length > 0 ? state.Message : "Weapon skills";
            _noticeUntil = Environment.TickCount64 + 4000;
        }
        Update();
    }
    private static Guid EquippedWeapon()
    {
        var player=Globals.Me;var slot=Options.Instance.Equipment.WeaponSlot;
        if(player==null || slot<0 || slot>=player.MyEquipment.Length)return Guid.Empty;
        var index=player.MyEquipment[slot];
        return index>=0 && index<player.Inventory.Count()?player.Inventory[index]?.ItemId??Guid.Empty:Guid.Empty;
    }
    private void Rebuild(Guid weapon)
    {
        _weapon=weapon;
        _textFits.Clear();
        _keyLabels.Clear();
        foreach(var entry in _buttons){entry.Button.Parent=null;entry.Button.Dispose();}_buttons.Clear();
        var category=_config.Masteries.FirstOrDefault(c=>c.WeaponIds.Contains(weapon));
        if(category!=null)
        {
            var ranks=_ranks.GetValueOrDefault(category.Key)??[];
            var slots = WeaponSkillLoadout.Resolve(category, _experience.GetValueOrDefault(category.Key), ranks, _loadouts.GetValueOrDefault(category.Key));
            for (var index = 0; index < WeaponSkillLoadout.SlotCount; index++)
            {
                var talent = category.Talents.FirstOrDefault(t => t.Key == slots[index]);
                var button = new InkAlignedButton(this, "WeaponSkillSlot" + index)
                { Font = Skin.DefaultFont, ShouldDrawBackground = false, TextAlign = Pos.Center };
                button.Clicked += (_, _) => { if (talent != null) Use(category, talent); };
                _buttons.Add((button, category, talent));
                _keyLabels[button] = new InkAlignedLabel(button, "WeaponSkillKey")
                { Font = Skin.DefaultFont, FontSize = 10, AutoSizeToContents = false, MouseInputEnabled = false,
                    KeyboardInputEnabled = false, TextAlign = Pos.Right | Pos.Bottom };
            }
        }
        IsHidden=_buttons.Count==0;
    }
    private void Use(MasteryCategory category,MasteryTalent talent)
    {
        if (!_canShow() || !_config.WeaponSkillBar.Enabled || Environment.TickCount64 < _pendingUntil ||
            _cooldowns.GetValueOrDefault(category.Key + "/" + talent.Key) > Environment.TickCount64) return;
        if(Globals.Me is not {} player || player.TargetId==Guid.Empty)
        { _messageSource = "Select a hostile target first."; _noticeUntil = Environment.TickCount64 + 4000; return; }
        _pendingUntil=Environment.TickCount64+1000;
        Networking.Network.SendPacket(new ArcheryRequestPacket { Action=ArcheryAction.UseWeaponSkill,
            CategoryKey=category.Key,TalentKey=talent.Key,TargetId=player.TargetId,RequestId=Guid.NewGuid() });
        Update();
    }
    internal void Update()
    {
        var weapon=EquippedWeapon();if(weapon!=_weapon)Rebuild(weapon);
        IsHidden = _buttons.Count == 0 || !_canShow() || !_config.WeaponSkillBar.Enabled;
        if (IsHidden) return;
        var settings = _config.WeaponSkillBar;
        var layout = LayoutFor(Parent?.Width ?? 800, Parent?.Height ?? 600, _buttons.Count, settings, _hotbarBounds?.Invoke());
        SetBounds(layout.Frame.X, layout.Frame.Y, layout.Frame.Width, layout.Frame.Height);
        _message.SetBounds(0, 0, Width, 20);
        if (Environment.TickCount64 >= _noticeUntil) _messageSource = "Weapon skills";
        // Status belongs in a tooltip, so combat feedback never creates another panel.
        TooltipText = ClientLanguages.Text(_messageSource);
        for(var i=0;i<_buttons.Count;i++)
        {
            var (button,category,talent)=_buttons[i];
            var slot = Math.Min(settings.SlotSize, Math.Max(24, (Parent?.Width ?? 800) - 8));
            button.SetBounds(2 + i % layout.Columns * (slot + settings.Gap),
                2 + i / layout.Columns * (slot + settings.Gap), slot, slot);
            var key = _keyLabels[button];
            key.SetBounds(1, Math.Max(0, slot - 13), slot - 3, 12);
            FitHudText(key, KeyCaption(i), 7);
            key.TextColorOverride = Color.White;
            if (talent == null)
            {
                button.Text = ""; button.IsDisabled = true;
                button.TooltipText = ClientLanguages.Format("Weapon skill slot {0}", i + 1) + " · " + ClientLanguages.Text("Choose a skill in Weapon Masteries.");
                continue;
            }
            var remaining=Math.Max(0,_cooldowns.GetValueOrDefault(category.Key+"/"+talent.Key)-Environment.TickCount64);
            FitHudText(button, remaining > 0 ? ((remaining + 999) / 1000).ToString() : "", 12);
            button.TooltipText = ClientLanguages.ContentText(talent.Name) + "\n" +
                ClientLanguages.Format("Weapon skill · Damage {0}% · Range {1} · Cooldown {2}s",
                    talent.Skill.Power((_ranks.GetValueOrDefault(category.Key) ?? []).GetValueOrDefault(talent.Key)),
                    talent.Skill.Range, talent.Skill.CooldownMilliseconds / 1000f) +
                (_messageSource == "Weapon skills" ? "" : "\n" + ClientLanguages.Text(_messageSource));
            button.IsDisabled=remaining>0 || Environment.TickCount64<_pendingUntil;
            button.TextColorOverride = button.IsDisabled ? new Color(255, 169, 158, 140) : new Color(255, 255, 231, 183);
        }
    }
    private static string KeyCaption(int slot)
    {
        try
        {
            var control = Control.WeaponSkill1 + slot;
            if (Controls.ActiveControls.TryGetMappingFor(control, out var mapping))
                return Hotbar.HotBarWindow.NavalKeyCaption(mapping.Bindings.FirstOrDefault(binding => binding.Key != Keys.None));
        }
        catch (InvalidOperationException) { /* Input can be unavailable during UI initialization. */ }
        return "-";
    }

    // Called from the player's existing keyboard path, after its text-input guard.
    // Consuming a held skill binding also prevents Alt+1 from activating normal slot 1.
    internal bool TryHandleHotkey()
    {
        if (!_canShow() || !_config.WeaponSkillBar.Enabled || _buttons.Count == 0) return false;
        var hits = Enumerable.Range(0, WeaponSkillLoadout.SlotCount)
            .Select(slot => Controls.IsControlPressed(Control.WeaponSkill1 + slot, out _, out var binding)
                ? (Slot: slot, Binding: binding) : (Slot: -1, Binding: (ControlBinding?)null))
            .Where(hit => hit.Slot >= 0).OrderByDescending(hit => hit.Binding!.Modifier).ToArray();
        if (hits.Length == 0) return false;
        var hit = hits[0];
        if (Controls.IsControlJustPressed(Control.WeaponSkill1 + hit.Slot) && hit.Slot < _buttons.Count)
        {
            var entry = _buttons[hit.Slot];
            if (!entry.Button.IsDisabled && entry.Talent != null) Use(entry.Category, entry.Talent);
        }
        return true;
    }

    private void FitHudText(Label label, string text, int inkHeight)
    {
        label.Text = text; label.TooltipText = text; label.FontSize = 14;
        var font = label.Font ?? Skin.DefaultFont;
        if (font == null) return;
        var request = (text, label.Width, font, ClientConfiguration.Instance.GlobalFontFamily ?? "", ClientConfiguration.Instance.GlobalFontSize, Skin.Renderer.Scale, inkHeight);
        if (_textFits.TryGetValue(label, out var previous) && previous == request) return;
        var natural = Skin.Renderer.MeasureText(font, 14, "Hg", 1).Y;
        var metrics = font is IFontInkMetrics ink ? ink.InkMetrics : new FontInkMetrics(0, 1);
        var scale = font is IFontInkMetrics ? Math.Clamp(inkHeight / Math.Max(1f, natural * metrics.HeightFraction), .5f, 3f) : 1f;
        var width = Skin.Renderer.MeasureText(font, 14, text, scale).X;
        if (width > label.Width - 10) scale *= Math.Max(1, label.Width - 10) / (float)width;
        label.SetTextScale(scale);
        _textFits[label] = request;
        if (label is IInkAlignedText aligned)
            aligned.InkOffsetY = font is IFontInkMetrics
                ? (int)Math.Round(natural * scale * (.5f - metrics.TopFraction - metrics.HeightFraction / 2)) : 0;
    }

    internal static (Rect Frame, int Columns) LayoutFor(int canvasWidth, int canvasHeight, int count,
        WeaponSkillBarSettings? settings = null, Rect? hotbar = null)
    {
        settings ??= new();
        var columns = Math.Min(Math.Max(1, count), Math.Min(settings.MaximumColumns,
            Math.Max(1, (canvasWidth - 8 + settings.Gap) / (settings.SlotSize + settings.Gap))));
        var width = columns * (settings.SlotSize + settings.Gap) - settings.Gap + 4;
        var rows = (Math.Max(1, count) + columns - 1) / columns;
        var height = rows * (settings.SlotSize + settings.Gap) - settings.Gap + 4;
        var center = hotbar is { Width: > 0 } bar ? bar.X + bar.Width / 2 : canvasWidth / 2;
        var bottom = hotbar is { Height: > 0 } anchor ? anchor.Y - settings.HotbarSpacing : canvasHeight - 100;
        return (new Rect(Math.Clamp(center - width / 2, 0, Math.Max(0, canvasWidth - width)),
            Math.Max(0, bottom - height), width, height), columns);
    }

    protected override void Render(SkinBase skin)
    {
        void Fill(int x, int y, int width, int height, Color color)
        {
            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(new Rect(x, y, Math.Max(1, width), Math.Max(1, height)));
        }
        foreach (var (button, category, talent) in _buttons)
        {
            Fill(button.X, button.Y, button.Width, button.Height, button.IsHovered
                ? new Color(255, 193, 147, 77) : new Color(255, 115, 81, 42));
            Fill(button.X + 2, button.Y + 2, button.Width - 4, button.Height - 4,
                button.IsDisabled ? new Color(255, 38, 35, 31) : new Color(255, 68, 43, 25));
            if (talent == null) continue;
            var icon = string.IsNullOrEmpty(talent.Skill.Icon) ? null
                : GameContentManager.Current.GetTexture(TextureType.Spell, talent.Skill.Icon);
            if (icon is not { IsMissingOrCorrupt: false } && ItemDescriptor.TryGet(_weapon, out var weapon))
                icon = GameContentManager.Current.GetTexture(TextureType.Item, weapon.Icon);
            if (icon is { IsMissingOrCorrupt: false })
            {
                skin.Renderer.DrawColor = Color.White;
                var scale = Math.Min((button.Width - 6f) / Math.Max(1, icon.Width), (button.Height - 6f) / Math.Max(1, icon.Height));
                var w = Math.Max(1, (int)(icon.Width * scale)); var h = Math.Max(1, (int)(icon.Height * scale));
                skin.Renderer.DrawTexturedRect(icon, new Rect(button.X + (button.Width - w) / 2,
                    button.Y + (button.Height - h) / 2, w, h), Color.White);
            }
            var remaining = Math.Max(0, _cooldowns.GetValueOrDefault(category.Key + "/" + talent.Key) - Environment.TickCount64);
            if (remaining > 0)
                Fill(button.X + 2, button.Y + 2, button.Width - 4, button.Height - 4, new Color(150, 0, 0, 0));
            var ready = 1 - Math.Clamp(remaining / (double)Math.Max(1, talent.Skill.CooldownMilliseconds), 0, 1);
            if (ready > 0) Fill(button.X + 3, button.Bottom - 4, (int)((button.Width - 6) * ready), 2,
                new Color(255, 182, 151, 81));
        }
        base.Render(skin);
    }

}
