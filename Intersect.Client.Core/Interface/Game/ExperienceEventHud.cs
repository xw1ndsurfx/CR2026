using Intersect.Framework.Core;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Localization;
using Intersect.Client.WorldEvents;
using Intersect.Network.Packets.Server;
using Intersect.Client.General;
namespace Intersect.Client.Interface.Game;
internal sealed class ExperienceEventHud : Label
{
    internal ExperienceEventHud(Base parent, string name) : base(parent, name)
    {
        AutoSizeToContents = false;
        Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont;
        FontSize = 11;
        TextColorOverride = new Color(255, 245, 214, 120);
        MouseInputEnabled = KeyboardInputEnabled = false;
        IsHidden = true;
    }
    protected override void RenderUnder(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        skin.Renderer.DrawColor = new Color(200, 28, 22, 18);
        skin.Renderer.DrawFilledRect(RenderBounds);
    }
    internal static string FormatStatus(int bonus, long remainingMs)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0L, remainingMs) / 1000d);
        var duration = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        return ClientLanguages.Format("EXP event +{0}% - Remaining: {1}", bonus, duration);
    }
    internal static string FormatPremium(long remaining)
    {
        var seconds=(long)Math.Ceiling(Math.Max(0,remaining)/1000d);
        var time=$"{seconds/3600%24:00}:{seconds/60%60:00}:{seconds%60:00}";
        var duration=seconds>=86400?ClientLanguages.Format("{0}d {1}",seconds/86400,time):time;
        return ClientLanguages.Format("Premium - Remaining: {0}",duration);
    }
    internal void ShowLine(string text, int y)
    {
        Text = text;
        SetBounds(8, y, Math.Max(120, Math.Min(620, Parent.Width - 16)), 26);
        IsHidden = false;
        BringToFront();
    }
}
public partial class GameInterface
{
    private ExperienceEventHud? _experienceEventHud, _experienceGainHud, _potionHud, _lootHud, _premiumHud;
    private readonly Queue<ActivityExperienceGainPacket> _experienceGains = new();
    private long _experienceGainUntil;
    public void ShowExperienceEventGain(ActivityExperienceGainPacket packet)
    {
        if (packet.TotalExperience > 0 && packet.EventBonusPercent > 0 && _experienceGains.Count < 32) _experienceGains.Enqueue(packet);
    }
    private static string Duration(long ms){var seconds=(long)Math.Ceiling(ms/1000d);return $"{seconds/3600:00}:{seconds/60%60:00}:{seconds%60:00}";}
    private void UpdateExperienceEventHud()
    {
        _experienceEventHud ??= new ExperienceEventHud(GameCanvas, "ExperienceEventStatus");
        _experienceGainHud ??= new ExperienceEventHud(GameCanvas, "ExperienceEventReward");
        _experienceEventHud.IsHidden = Globals.Me == null || ExperienceEventState.Bonus <= 0;
        if (!_experienceEventHud.IsHidden)
            _experienceEventHud.ShowLine(ExperienceEventHud.FormatStatus(ExperienceEventState.Bonus, ExperienceEventState.RemainingMilliseconds), 56);
        _potionHud??=new ExperienceEventHud(GameCanvas,"ExperiencePotionStatus");
        _lootHud??=new ExperienceEventHud(GameCanvas,"LootBoosterStatus");
        _potionHud.IsHidden=Globals.Me==null || BoosterState.Potion<=0;
        _lootHud.IsHidden=Globals.Me==null || BoosterState.Loot<=0;
        var nextY=ExperienceEventState.Bonus>0?84:56;
        if(!_potionHud.IsHidden){_potionHud.ShowLine(ClientLanguages.Format("EXP potion +{0}% - Remaining: {1}",BoosterState.Potion,Duration(BoosterState.PotionRemaining)),nextY);nextY+=28;}
        if(!_lootHud.IsHidden){_lootHud.ShowLine(ClientLanguages.Format("Loot event +{0}% - Remaining: {1}",BoosterState.Loot,Duration(BoosterState.LootRemaining)),nextY);nextY+=28;}
        _premiumHud??=new ExperienceEventHud(GameCanvas,"PremiumStatus");
        _premiumHud.IsHidden=Globals.Me==null || PremiumState.Remaining<=0;
        if(!_premiumHud.IsHidden){_premiumHud.ShowLine(ExperienceEventHud.FormatPremium(PremiumState.Remaining),nextY);nextY+=28;}
        if(!_experienceGainHud.IsHidden)_experienceGainHud.Y=nextY;
        if (Timing.Global.Milliseconds >= _experienceGainUntil)
        {
            _experienceGainHud.IsHidden = true;
            if (_experienceGains.TryDequeue(out var gain))
            {
                var activity = gain.Activity switch { "poker" => ClientLanguages.Text("Poker"), "blackjack" => ClientLanguages.Text("Blackjack"), "roulette" => ClientLanguages.Text("Roulette"), "potions" => ClientLanguages.Text("Alchemy"), _ => ClientLanguages.ContentText(gain.Activity) };
                _experienceGainHud.ShowLine(ClientLanguages.Format("{0}: +{1} EXP (bonus +{2}%: +{3})", activity, gain.TotalExperience.ToString("N0"), gain.EventBonusPercent, gain.EventBonusExperience.ToString("N0")), nextY);
                _experienceGainUntil = Timing.Global.Milliseconds + 4000;
            }
        }
    }
}
