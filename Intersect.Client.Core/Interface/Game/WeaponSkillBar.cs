using System.Text.Json;
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
    private readonly Dictionary<Label, (string Text, int Width, IFont Font, string Family, int Size, float Scale, int Ink)> _textFits = [];
    private string _messageSource = "Weapon skills";
    private long _noticeUntil;
    private readonly List<(Button Button,MasteryCategory Category,MasteryTalent Talent)> _buttons=[];
    private RoyalCombatConfiguration _config=new();
    private Dictionary<string,long> _experience=[];
    private Dictionary<string,Dictionary<string,int>> _ranks=[];
    private readonly Dictionary<string,long> _cooldowns=[];
    private Guid _weapon;
    private long _pendingUntil;
    internal bool HasMasteryState { get; private set; }

    internal WeaponSkillBar(Canvas canvas, Func<bool> canShow):base(canvas,nameof(WeaponSkillBar))
    {
        _canShow = canShow;
        ShouldDrawBackground = false;
        _message = new InkAlignedLabel(this, "WeaponSkillMessage") { Font = Skin.DefaultFont,
            Text = ClientLanguages.Text("Weapon skills"), AutoSizeToContents = false,
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
        foreach(var entry in _buttons){entry.Button.Parent=null;entry.Button.Dispose();}_buttons.Clear();
        var category=_config.Masteries.FirstOrDefault(c=>c.WeaponIds.Contains(weapon));
        if(category!=null)
        {
            var ranks=_ranks.GetValueOrDefault(category.Key)??[];
            foreach(var talent in category.Talents.Where(t=>t.Skill.Enabled && ranks.GetValueOrDefault(t.Key)>0 &&
                MasteryRules.Eligible(category,t,_experience.GetValueOrDefault(category.Key),ranks)))
            {
                var button = new InkAlignedButton(this, "WeaponSkill_" + talent.Key)
                { Font = Skin.DefaultFont, ShouldDrawBackground = false, TextAlign = Pos.Center };
                button.Clicked += (_, _) => Use(category, talent);
                button.SetToolTipText(ClientLanguages.Format("Weapon skill · Damage {0}% · Range {1} · Cooldown {2}s",
                    talent.Skill.Power(ranks.GetValueOrDefault(talent.Key)),talent.Skill.Range,talent.Skill.CooldownMilliseconds/1000f));
                _buttons.Add((button,category,talent));
            }
        }
        IsHidden=_buttons.Count==0;
    }
    private void Use(MasteryCategory category,MasteryTalent talent)
    {
        if (!_canShow()) return;
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
        IsHidden = _buttons.Count == 0 || !_canShow();
        if (IsHidden) return;
        var layout = LayoutFor(Parent?.Width ?? 800, Parent?.Height ?? 600, _buttons.Count);
        SetBounds(layout.Frame.X, layout.Frame.Y, layout.Frame.Width, layout.Frame.Height);
        _message.SetBounds(40, 7, Math.Max(1, Width - 48), 24);
        if (Environment.TickCount64 >= _noticeUntil) _messageSource = "Weapon skills";
        FitHudText(_message, ClientLanguages.Text(_messageSource), 12);
        for(var i=0;i<_buttons.Count;i++)
        {
            var (button,category,talent)=_buttons[i];
            button.SetBounds(8 + i % layout.Columns * 190, 36 + i / layout.Columns * 40, 182, 34);
            var remaining=Math.Max(0,_cooldowns.GetValueOrDefault(category.Key+"/"+talent.Key)-Environment.TickCount64);
            var name=ClientLanguages.ContentText(ClientLanguages.Text(talent.Name));
            FitHudText(button, remaining > 0 ? ClientLanguages.Format("{0} ({1}s)", name, (remaining + 999) / 1000) : name, 11);
            button.IsDisabled=remaining>0 || Environment.TickCount64<_pendingUntil;
            button.TextColorOverride = button.IsDisabled ? new Color(255, 169, 158, 140) : new Color(255, 255, 231, 183);
        }
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

    internal static (Rect Frame, int Columns) LayoutFor(int canvasWidth, int canvasHeight, int count)
    {
        var columns = Math.Min(Math.Max(1, count), Math.Min(3, Math.Max(1, (canvasWidth - 40) / 190)));
        var width = columns * 190 + 16;
        var height = 42 + (Math.Max(1, count) + columns - 1) / columns * 40;
        return (new Rect(Math.Max(8, (canvasWidth - width) / 2), Math.Max(8, canvasHeight - height - 108), width, height), columns);
    }

    protected override void Render(SkinBase skin)
    {
        void Fill(int x, int y, int width, int height, Color color)
        {
            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(new Rect(x, y, Math.Max(1, width), Math.Max(1, height)));
        }
        Fill(0, 0, Width, Height, new Color(230, 174, 127, 61));
        Fill(2, 2, Width - 4, Height - 4, new Color(240, 35, 24, 18));
        Fill(8, 31, Width - 16, 1, new Color(255, 107, 77, 40));
        if (ItemDescriptor.TryGet(_weapon, out var weapon) && GameContentManager.Current.GetTexture(TextureType.Item, weapon.Icon) is { } icon)
        {
            skin.Renderer.DrawColor = Color.White;
            skin.Renderer.DrawTexturedRect(icon, new Rect(10, 6, 24, 24), Color.White);
        }
        foreach (var (button, category, talent) in _buttons)
        {
            Fill(button.X, button.Y, button.Width, button.Height, button.IsHovered
                ? new Color(255, 193, 147, 77) : new Color(255, 115, 81, 42));
            Fill(button.X + 2, button.Y + 2, button.Width - 4, button.Height - 4,
                button.IsDisabled ? new Color(255, 38, 35, 31) : new Color(255, 68, 43, 25));
            var remaining = Math.Max(0, _cooldowns.GetValueOrDefault(category.Key + "/" + talent.Key) - Environment.TickCount64);
            var ready = 1 - Math.Clamp(remaining / (double)Math.Max(1, talent.Skill.CooldownMilliseconds), 0, 1);
            if (ready > 0) Fill(button.X + 3, button.Bottom - 4, (int)((button.Width - 6) * ready), 2,
                new Color(255, 182, 151, 81));
        }
        base.Render(skin);
    }

}
