#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.MiniGames;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Progression;

namespace Intersect.Server.MiniGames.Blackjack;

public sealed record BlackjackKey(Guid Map,Guid Instance,string Name);
public sealed record BlackjackSettings(BlackjackRules Rules,int Npcs,bool Auto,Guid Currency,long Reserve,
    Guid DealAnimation,Guid VictoryAnimation,bool Announce,int NpcBack,PokerMotionSet? Motion=null,
    PokerLevelRewardSet? LevelRewards=null)
{
    public PokerMotionSet MotionSettings => Motion ?? PokerMotionSet.Default;
    public PokerLevelRewardSet RewardSettings => LevelRewards ?? PokerLevelRewardSet.Empty;
}
public sealed record BlackjackWin(string Name,long Net);
public sealed record BlackjackQuestNotice(Guid PlayerId, BlackjackQuestUpdate Update);
public sealed record BlackjackLevelRewardNotice(Guid PlayerId, int Level, PokerLevelReward[] Rewards);
public sealed record BlackjackLevelNotice(Guid PlayerId, int Level);

/// <summary>Serialized by runtime gate. Financial callbacks never acquire any Player lock here.</summary>
public sealed class BlackjackSessionTable
{
    public Guid Id {get;}=Guid.NewGuid();
    public BlackjackKey Key {get;}
    public BlackjackSettings Settings {get;}
    private long EffectiveReserve => Math.Max(
        Settings.Reserve,
        Math.Max(Settings.Rules.BuyIn, Settings.Rules.MaximumBet * 4L * Settings.Rules.MaxPlayers)
    );

    public string House
    {
        get
        {
            // A changed funding/rules configuration gets a distinct persistent dealer bank.
            // This avoids reusing a legacy house that was seeded with only a few Aureons,
            // while the same configuration still keeps its finite balance across sessions.
            var config = $"{EffectiveReserve}:{Settings.Rules.BuyIn}:{Settings.Rules.MinimumBet}:" +
                         $"{Settings.Rules.MaximumBet}:{Settings.Rules.MaxPlayers}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config)))[..12];
            return "blackjack:"+Key.Map.ToString("N")+":"+Key.Name+":"+Settings.Currency.ToString("N")+":cfg:"+hash;
        }
    }
    public BlackjackTable Core {get;}
    public bool Pending {get;private set;}
    public long Version=>Core.Revision+_version;
    public bool Empty=>_bankReleased;
    private readonly PokerMoneyLedger? _money;
    private readonly IMiniGameProgressStore _test;
    private readonly MoneySeat? _bank;
    private readonly Dictionary<Guid,MoneySeat> _funds=new();
    private readonly Dictionary<Guid,MiniGameProgress> _profiles=new();
    private readonly Dictionary<Guid,int> _backs=new();
    private readonly Queue<BlackjackWin> _wins=new();
    private readonly Queue<BlackjackQuestNotice> _quests=new();
    private readonly Queue<BlackjackLevelRewardNotice> _levelRewards=new();
    private readonly Queue<BlackjackLevelNotice> _levels=new();
    private long _version,_settled;
    private DateTimeOffset? _next;
    private DateTimeOffset _npcAt,_retry;
    private bool _bankReleased;
    public BlackjackSessionTable(BlackjackKey key,BlackjackSettings settings,PokerMoneyLedger? money,IMiniGameProgressStore test)
    {
        if(!settings.Rules.IsValid || settings.Npcs<0 || settings.Npcs>=settings.Rules.MaxPlayers ||
            settings.Reserve is <0 or >1_000_000_000 || !MiniGameProgression.IsBack(settings.NpcBack) ||
            !settings.MotionSettings.IsValid || (settings.Currency!=Guid.Empty)!=(money!=null))
            throw new ArgumentException("Invalid blackjack settings.");
        Key=key;Settings=settings;_money=money;_test=test;
        var bankroll=Math.Max(10000,settings.Rules.MaximumBet*4*settings.Rules.MaxPlayers);
        if(money!=null)
        {
            var available=money.HouseAvailable(House);
            var effectiveReserve=EffectiveReserve;
            var stake=Math.Min(available>0?available:effectiveReserve,bankroll);
            if(stake<settings.Rules.MinimumBet*4) throw new MoneyRuleException("BankTooLow");
            _bank=money.OpenNpc(Guid.NewGuid(),Id,settings.Currency,House,effectiveReserve,stake)
                ??throw new MoneyRuleException("BankTooLow");
            bankroll=_bank.Amount;
        }
        Core=new(settings.Rules,bankroll);
    }
    public bool Contains(Guid id)=>_profiles.ContainsKey(id);
    public bool Leaving(Guid id)=>Core.Snapshot(id).Seats.FirstOrDefault(s=>s.PlayerId==id)?.Leaving ?? true;
    public MiniGameProgress Profile(Guid id)=>_money?.BlackjackProfile(id) ?? _test.Load(id,"blackjack");
    public bool CanJoin()
    {
        if(Pending || Empty) return false;
        var state=Core.Snapshot(Guid.Empty);
        if(state.Seats.Length<Settings.Rules.MaxPlayers) return true;
        if(Core.Busy) return false;
        var npc=state.Seats.FirstOrDefault(s=>s.Npc);
        if(npc==null)return false;
        Remove(npc.PlayerId);return true;
    }
    public void Join(Guid player,string name,MiniGameProgress profile,MoneySeat? funds)
    {
        if(!profile.IsValid || Contains(player) || (_money!=null && (funds==null || funds.Npc || funds.Character!=player ||
            funds.Table!=Id || funds.Currency!=Settings.Currency || funds.Amount!=Settings.Rules.BuyIn)))
            throw new MoneyRuleException("Invalid blackjack admission.");
        var error=Core.Join(player,name,Settings.Rules.BuyIn);
        if(error!=BlackjackError.None)throw new MoneyRuleException(error.ToString());
        if(funds!=null)_funds.Add(player,funds);
        _profiles.Add(player,profile);_backs[player]=profile.SelectedBack;++_version;
    }
    private void Remove(Guid player)
    {
        if(Core.Busy)throw new InvalidOperationException("Cannot cash out an unfinished hand.");
        if(_funds.TryGetValue(player,out var fund)){_money!.Release(fund.Id);_funds.Remove(player);}
        Core.Remove(player);_profiles.Remove(player);_backs.Remove(player);++_version;
    }
    private void PrepareNpcs()
    {
        if(Core.Busy)return;
        var state=Core.Snapshot(Guid.Empty);
        foreach(var seat in state.Seats.Where(s=>s.Npc && s.Chips<Settings.Rules.MinimumBet).ToArray())Remove(seat.PlayerId);
        state=Core.Snapshot(Guid.Empty);
        var desired=Math.Min(Settings.Npcs,Settings.Rules.MaxPlayers-_profiles.Count);
        while(state.Seats.Count(s=>s.Npc)<desired)
        {
            var index=Enumerable.Range(1,5).First(i=>state.Seats.All(s=>s.Name!=PokerTableTheme.GuestName(i)));
            var npc=Guid.NewGuid();
            var fund=_money?.OpenNpc(npc,Id,Settings.Currency,House,Settings.Reserve,Settings.Rules.BuyIn);
            if(_money!=null && fund==null)break;
            var error=Core.Join(npc,PokerTableTheme.GuestName(index),Settings.Rules.BuyIn,true);
            if(error!=BlackjackError.None){if(fund!=null)_money!.Release(fund.Id);break;}
            if(fund!=null)_funds[npc]=fund;_backs[npc]=Settings.NpcBack;
            state=Core.Snapshot(Guid.Empty);
        }
    }
    public BlackjackError Start(Guid player,long revision,DateTimeOffset now)
    {
        if(!Contains(player) || Leaving(player))return BlackjackError.NotSeated;
        if(Pending)return BlackjackError.Busy;
        if(Core.Revision!=revision)return BlackjackError.StaleState;
        PrepareNpcs();
        var result=Core.Begin(Core.Revision,now);
        if(result==BlackjackError.None)
        {
            foreach(var pair in _profiles)_backs[pair.Key]=pair.Value.SelectedBack;
            _next=null;_npcAt=now.AddMilliseconds(1200);
        }
        return result;
    }
    public BlackjackError Bet(Guid player,long hand,long revision,long amount,DateTimeOffset now)
    { if(Pending)return BlackjackError.Busy;var r=Core.Bet(player,hand,revision,amount,now);Complete();return r; }
    public BlackjackError Act(Guid player,long hand,long revision,BlackjackAction action,DateTimeOffset now)
    { if(Pending)return BlackjackError.Busy;var r=Core.Act(player,hand,revision,action,now);Complete();return r; }
    public void SelectBack(Guid player,int back)
    {
        if(!Contains(player) || Leaving(player) || Pending)throw new MoneyRuleException("Busy");
        _profiles[player]=_money?.SelectBlackjackBack(player,back) ?? _test.SelectBack(player,"blackjack",back);
        if(!Core.Busy)_backs[player]=back;++_version;
    }
    private void Complete()
    {
        if(Core.Stage!=BlackjackStage.Finished || Core.HandId<=_settled)return;
        var state=Core.Snapshot(Guid.Empty);
        var beforeProfiles=_profiles.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(_money!=null)
        {
            var closing=state.Seats.ToDictionary(s=>_funds[s.PlayerId].Id,s=>s.Chips);
            closing[_bank!.Id]=state.Bank;
            _money.SettleBlackjack(Id,_bank.Id,state.HandId,closing);
        }
        else
        {
            foreach(var seat in state.Seats.Where(s=>!s.Npc && s.Hands.Sum(h=>h.Net)>0))
                _test.AwardWin(seat.PlayerId,"blackjack",Id,state.HandId);
        }
        // Reload before publishing success, including after a retry of an already committed receipt.
        foreach(var id in _profiles.Keys.ToArray())_profiles[id]=Profile(id);
        foreach(var seat in state.Seats.Where(s=>!s.Npc))
        {
            var net=seat.Hands.Sum(h=>h.Net);
            var before=beforeProfiles.GetValueOrDefault(seat.PlayerId) ?? new MiniGameProgress();
            var after=_profiles[seat.PlayerId];
            if(after.Level>before.Level)
            {
                _levels.Enqueue(new(seat.PlayerId,after.Level));
                var rewards=Settings.RewardSettings.Items
                    .Where(reward=>reward.Level>before.Level && reward.Level<=after.Level).ToArray();
                if(rewards.Length>0)_levelRewards.Enqueue(new(seat.PlayerId,after.Level,rewards));
            }
            if(net>0)_wins.Enqueue(new(seat.Name,net));
            _quests.Enqueue(new(seat.PlayerId,new BlackjackQuestUpdate(
                seat.Hands.Length>0,net,MiniGameProgression.Level(after.Experience))));
        }
        _settled=state.HandId;++_version;
    }
    public void Leave(Guid player,DateTimeOffset now)
    { Core.Leave(player,now);if(!Pending){Complete();Cleanup();} }
    private void Cleanup()
    {
        if(Core.Busy)return;
        foreach(var seat in Core.Snapshot(Guid.Empty).Seats.Where(s=>s.Leaving).ToArray())Remove(seat.PlayerId);
        if(_profiles.Count>0)return;
        foreach(var npc in Core.Snapshot(Guid.Empty).Seats.ToArray())Remove(npc.PlayerId);
        if(_bank!=null)_money!.Release(_bank.Id);
        _bankReleased=true;++_version;
    }
    public void AbortEmpty() { if(_profiles.Count==0)Cleanup(); }
    public void Suspend(DateTimeOffset now) {Pending=true;_retry=now.AddSeconds(2);++_version;}
    public void Tick(DateTimeOffset now)
    {
        if(Empty || Pending && now<_retry)return;
        Complete();Cleanup();if(Empty)return;
        if(Pending){Pending=false;++_version;}
        Core.Tick(now);Complete();Cleanup();if(Empty)return;
        if(!Core.Busy)
        {
            if(!Settings.Auto)return;
            var human=Core.Snapshot(Guid.Empty).Seats.FirstOrDefault(s=>!s.Npc && !s.Leaving && s.Chips>=Settings.Rules.MinimumBet);
            if(human==null){_next=null;return;}
            _next??=now.AddSeconds(5);
            if(now>=_next.Value){Start(human.PlayerId,Core.Revision,now);_next=now.AddSeconds(5);}return;
        }
        _next=null;
        if(now<_npcAt)return;
        var state=Core.Snapshot(Guid.Empty);
        if(state.Stage==BlackjackStage.Betting)
        {
            // Humans select first. NPCs never consume all bank exposure before a human can bet.
            if(!state.Seats.Any(s=>!s.Npc && s.Hands.Length>0))return;
            var npc=state.Seats.FirstOrDefault(s=>s.Npc && s.InRound && s.Hands.Length==0 && !s.Leaving);
            if(npc!=null){Core.Bet(npc.PlayerId,Core.HandId,Core.Revision,Settings.Rules.MinimumBet,now);_npcAt=now.AddMilliseconds(900);}
        }
        else if(state.Stage==BlackjackStage.Players)
        {
            var actor=state.Seats.FirstOrDefault(s=>s.Seat==state.ActingSeat && s.Npc);
            if(actor!=null)
            {
                var v=Core.Snapshot(actor.PlayerId);var h=actor.Hands[state.ActingHand];
                var action=v.CanSplit && h.Cards[0]%13 is 6 or 12?BlackjackAction.Split:
                    v.CanDouble && h.Total==11?BlackjackAction.Double:h.Total<17?BlackjackAction.Hit:BlackjackAction.Stand;
                Core.Act(actor.PlayerId,Core.HandId,Core.Revision,action,now);_npcAt=now.AddMilliseconds(1200);
            }
            else _npcAt=now.AddMilliseconds(700);
        }
        Complete();Cleanup();
    }
    public BlackjackWin[] CollectWins(){var r=_wins.ToArray();_wins.Clear();return r;}
    public BlackjackQuestNotice[] CollectQuestUpdates(){var r=_quests.ToArray();_quests.Clear();return r;}
    public BlackjackLevelRewardNotice[] CollectLevelRewards(){var r=_levelRewards.ToArray();_levelRewards.Clear();return r;}
    public BlackjackLevelNotice[] CollectLevels(){var r=_levels.ToArray();_levels.Clear();return r;}
    public BlackjackTableState Project(Guid player)
    {
        var s=Core.Snapshot(player);var p=_profiles[player];
        var net=!Pending && s.Stage==BlackjackStage.Finished && _settled==s.HandId?Math.Max(0,s.Seats.Single(x=>x.PlayerId==player).Hands.Sum(h=>h.Net)):0;
        return new BlackjackTableState
        {
            HandId=s.HandId,Revision=s.Revision,Stage=s.Stage,Bank=s.Bank,ActingSeat=s.ActingSeat,ActingHand=s.ActingHand,
            DeadlineUnixMs=s.Deadline==default?0:s.Deadline.ToUnixTimeMilliseconds(),DealerCards=s.DealerCards,DealerHoleHidden=s.DealerHoleHidden,
            DealerTotal=s.DealerTotal,DealerSoft=s.DealerSoft,
            Seats=s.Seats.Select(x=>new BlackjackPlayerState{Seat=x.Seat,PlayerId=x.PlayerId,Name=x.Name,Npc=x.Npc,Chips=x.Chips,
                Leaving=x.Leaving,InRound=x.InRound,LastAction=x.LastAction,CardBackId=_backs.GetValueOrDefault(x.PlayerId,Settings.NpcBack),
                SelectedBackId=_profiles.GetValueOrDefault(x.PlayerId)?.SelectedBack??Settings.NpcBack,
                Hands=x.Hands.Select(h=>new BlackjackHandState{Cards=h.Cards,Bet=h.Bet,Total=h.Total,Soft=h.Soft,Natural=h.Natural,
                    Done=h.Done,Outcome=h.Outcome,Net=h.Net}).ToArray()}).ToArray(),
            CanBet=!Pending && s.CanBet,MinimumBet=Settings.Rules.MinimumBet,MaximumBet=s.MaximumBet,
            CanHit=!Pending && s.CanHit,CanStand=!Pending && s.CanStand,CanDouble=!Pending && s.CanDouble,CanSplit=!Pending && s.CanSplit,
            CurrencyItemId=Settings.Currency,Experience=p.Experience,Wins=p.Wins,Pending=Pending,AutoStart=Settings.Auto,
            DealAnimationId=Settings.DealAnimation,VictoryAnimationId=net>0?Settings.VictoryAnimation:Guid.Empty,NetWin=net,
            DealerBackId=Settings.NpcBack,HitSoft17=Settings.Rules.HitSoft17,
            ProceduralAnimationSpeed=Settings.MotionSettings.Speed,AnimateDealCards=Settings.MotionSettings.DealCards,
            AnimateBoardCards=Settings.MotionSettings.BoardCards,AnimateChips=Settings.MotionSettings.Chips,
            AnimateShowdown=Settings.MotionSettings.Showdown,AnimateShuffle=Settings.MotionSettings.Shuffle,
            AnimateAllIn=Settings.MotionSettings.AllIn,
        };
    }
}
