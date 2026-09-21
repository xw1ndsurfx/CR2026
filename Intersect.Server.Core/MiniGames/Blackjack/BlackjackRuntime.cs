#nullable enable
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using Intersect.Server.Networking;

namespace Intersect.Server.MiniGames.Blackjack;

/// <summary>EntityLock -> User.Save gate (admission only) -> runtime -> shared ledger. Send outside runtime gate.</summary>
internal static class BlackjackRuntime
{
    private sealed class View(Player player,Client client,PokerPresence presence,BlackjackSessionTable table)
    {
        public readonly Player Player=player;public readonly Client Client=client;
        public readonly PokerPresence Presence=presence;public readonly DateTime? Login=player.LoginTime;
        public readonly BlackjackSessionTable Table=table;
        public Guid Id=Guid.NewGuid();public BlackjackRequestGuard Guard=null!;
        public bool Closed;public long Published=-1;
        public void Renew(){Id=Guid.NewGuid();Guard=new(Table.Id,Id);Closed=false;Published=-1;}
    }
    private sealed record Delivery(View? View,BlackjackStatePacket? Packet,string? Announcement=null,BlackjackQuestUpdate? Quest=null);
    private static readonly object Gate=new();
    private static readonly Dictionary<BlackjackKey,BlackjackSessionTable> Tables=new();
    private static readonly Dictionary<Guid,View> Views=new();
    private static readonly IMiniGameProgressStore TestProgress=new SqliteMiniGameProgressStore(Path.Combine("resources","minigames-test.db"));
    internal static bool Contains(Guid player){lock(Gate)return Views.ContainsKey(player);}
    internal static PokerRegistryResult Join(Player player,StartMiniGameCommand command)
    {
        if(command.Game!=MiniGameType.Blackjack || !command.HasValidSettings() || player.User==null)return new(PokerRegistryError.InvalidRules);
        List<Delivery> output=[];var changed=false;PokerRegistryResult result;
        try
        {
            lock(player.EntityLock)
            lock(player.User.PokerSaveGate)
            {
                if(PokerRuntime.HasSeat(player.Id))return new(PokerRegistryError.AlreadyAtAnotherTable);
                if(player.Client is not {IsEditor:false} client || !player.TryCapturePokerPresence(out var presence))return new(PokerRegistryError.InvalidPresence);
                if(command.CurrencyItemId!=Guid.Empty && !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(command.CurrencyItemId)))
                    throw new MoneyRuleException("Invalid table currency.");
                var settings=new BlackjackSettings(new BlackjackRules(command.MaxPlayers-1,command.StartingChips,
                    command.BlackjackMinimumBet,command.BlackjackMaximumBet,command.TurnSeconds,command.BlackjackHitSoft17),
                    command.NpcPlayers,command.AutoStart,command.CurrencyItemId,command.NpcReserve,
                    command.DealAnimationId,command.VictoryAnimationId,command.AnnounceWins,command.NpcCardBackId,command.CreateMotionSet());
                var key=new BlackjackKey(presence.MapId,presence.MapInstanceId,command.TableId);
                var money=command.CurrencyItemId==Guid.Empty?null:PokerInventoryBridge.Ledger;
                lock(Gate)
                {
                    if(Views.TryGetValue(player.Id,out var old))
                    {
                        if(old.Presence!=presence || !ReferenceEquals(old.Client,client))return new(PokerRegistryError.SessionChanged);
                        if(old.Closed || old.Table.Leaving(player.Id))return new(PokerRegistryError.Leaving);
                        if(old.Table.Key!=key)return new(PokerRegistryError.AlreadyAtAnotherTable);
                        if(old.Table.Settings!=settings)return new(PokerRegistryError.RulesConflict);
                        old.Renew();Queue(output,old);result=new(PokerRegistryError.None,old.Table.Id);
                    }
                    else
                    {
                        if(!Tables.TryGetValue(key,out var table))
                        {
                            if(Tables.Count>=256)return new(PokerRegistryError.Capacity);
                            table=new(key,settings,money,TestProgress);Tables.Add(key,table);
                        }
                        if(table.Settings!=settings)return new(PokerRegistryError.RulesConflict);
                        if(!table.CanJoin())return new(PokerRegistryError.Capacity);
                        var profile=table.Profile(player.Id);
                        MoneySeat? seat=null;
                        if(money!=null)
                        {seat=PokerInventoryBridge.BuyIn(player,Guid.NewGuid(),table.Id,command.CurrencyItemId,table.House,command.StartingChips);changed=true;}
                        try{table.Join(player.Id,player.Name,profile,seat);}
                        catch{if(seat!=null)money!.Release(seat.Id);throw;}
                        var view=new View(player,client,presence,table);view.Renew();Views[player.Id]=view;
                        Queue(output,view);Collect(output);result=new(PokerRegistryError.None,table.Id);
                    }
                }
            }
        }
        catch(Exception error)
        {
            PokerInventoryBridge.Log(error);
            PacketSender.SendChatMsg(player,"[Blackjack] "+(error is MoneyRuleException or NotSupportedException?error.Message:
                "Storage unavailable. Pending inventory claims are retained."),ChatMessageType.Error,Color.White);
            result=new(PokerRegistryError.PokerRejected);
        }
        if(changed)PokerInventoryBridge.NotifyInventory(player);
        Send(output);return result;
    }
    internal static bool Leave(Player player)
    {
        List<Delivery> output=[];
        lock(player.EntityLock)
        lock(Gate)
        {
            if(!Views.TryGetValue(player.Id,out var v) || !ReferenceEquals(v.Player,player))return false;
            Close(v,output,0);Collect(output);
        }
        Send(output);
        try{PokerInventoryBridge.Recover(player);}catch(Exception error){PokerInventoryBridge.Log(error);}return true;
    }
    internal static void Handle(Client client,BlackjackRequestPacket p)
    {
        if(client.IsEditor || client.Entity is not {} player || !p.IsValid)return;
        List<Delivery> output=[];
        lock(player.EntityLock)
        lock(Gate)
        {
            if(!Views.TryGetValue(player.Id,out var v) || v.Closed || !ReferenceEquals(v.Client,client) ||
                !ReferenceEquals(v.Player,player) || !v.Guard.Accept(p,Environment.TickCount64))return;
            if(!player.TryCapturePokerPresence(out var presence) || presence!=v.Presence || p.Kind==BlackjackRequestKind.Leave)
                Close(v,output,p.RequestId);
            else
            {
                var code="";var now=DateTimeOffset.UtcNow;
                try
                {
                    var r=BlackjackError.None;
                    switch(p.Kind)
                    {
                        case BlackjackRequestKind.Start:r=v.Table.Start(player.Id,p.Revision,now);break;
                        case BlackjackRequestKind.Bet:r=v.Table.Bet(player.Id,p.HandId,p.Revision,p.Amount,now);break;
                        case BlackjackRequestKind.Hit:r=v.Table.Act(player.Id,p.HandId,p.Revision,BlackjackAction.Hit,now);break;
                        case BlackjackRequestKind.Stand:r=v.Table.Act(player.Id,p.HandId,p.Revision,BlackjackAction.Stand,now);break;
                        case BlackjackRequestKind.Double:r=v.Table.Act(player.Id,p.HandId,p.Revision,BlackjackAction.Double,now);break;
                        case BlackjackRequestKind.Split:r=v.Table.Act(player.Id,p.HandId,p.Revision,BlackjackAction.Split,now);break;
                        case BlackjackRequestKind.SelectBack:v.Table.SelectBack(player.Id,(int)p.Amount);break;
                    }
                    if(r!=BlackjackError.None)code=r.ToString();
                }
                catch(Exception error) when(error is ArgumentOutOfRangeException || error is MoneyRuleException {Message:"CardBackLocked" or "Busy"})
                {code=error is ArgumentOutOfRangeException?"CardBackLocked":error.Message;}
                catch(Exception error){v.Table.Suspend(now);PokerInventoryBridge.Log(error);code="FundingPending";}
                if(v.Table.Contains(player.Id))Queue(output,v,p.RequestId,error:code);
            }
            Collect(output);
        }
        Send(output);
    }
    private static void Close(View v,List<Delivery> output,long request)
    {
        try{v.Table.Leave(v.Player.Id,DateTimeOffset.UtcNow);}
        catch(Exception error){v.Table.Suspend(DateTimeOffset.UtcNow);PokerInventoryBridge.Log(error);}
        Queue(output,v,request,closed:true);v.Closed=true;
    }
    internal static void Sweep()
    {
        View[] observed;lock(Gate)observed=Views.Values.Where(v=>!v.Closed).ToArray();
        var absent=observed.Where(v=>!ReferenceEquals(v.Player.Client,v.Client) || v.Player.LoginTime!=v.Login ||
            !Player.IsPokerPresenceCurrent(v.Presence)).ToArray();
        List<Delivery> output=[];
        lock(Gate)
        {
            foreach(var v in absent)if(ReferenceEquals(Views.GetValueOrDefault(v.Player.Id),v))Close(v,output,0);
            foreach(var t in Tables.Values.ToArray())
            {try{t.Tick(DateTimeOffset.UtcNow);}catch(Exception error){t.Suspend(DateTimeOffset.UtcNow);PokerInventoryBridge.Log(error);}}
            Collect(output);
        }
        Send(output);
    }
    private static void Collect(List<Delivery> output)
    {
        foreach(var v in Views.Values.ToArray())
        {
            if(!v.Table.Contains(v.Player.Id)){if(!v.Closed)Queue(output,v,closed:true);Views.Remove(v.Player.Id);}
            else if(!v.Closed && v.Published!=v.Table.Version)Queue(output,v);
        }
        foreach(var pair in Tables.ToArray())
        {
            foreach(var win in pair.Value.CollectWins())
                if(pair.Value.Settings.Announce)output.Add(new(null,null,$"[Blackjack] {Clean(win.Name)} wins {win.Net} "+
                    (pair.Value.Settings.Currency==Guid.Empty?"test chips":Clean(ItemDescriptor.GetName(pair.Value.Settings.Currency)))+" (net gain)."));
            foreach(var quest in pair.Value.CollectQuestUpdates())
                if(Views.TryGetValue(quest.PlayerId,out var questView) && ReferenceEquals(questView.Table,pair.Value))
                    output.Add(new(questView,null,Quest:quest.Update));
            if(pair.Value.Empty)Tables.Remove(pair.Key);
        }
    }
    private static string Clean(string text)=>new(text.Where(c=>!char.IsControl(c)).ToArray());
    private static void Queue(List<Delivery> output,View v,long request=0,bool closed=false,string error="")
    {
        output.Add(new(v,new BlackjackStatePacket{TableInstanceId=v.Table.Id,ViewId=v.Id,PlayerId=v.Player.Id,
            Sequence=PokerRuntime.NextSequence(),RequestId=request,Closed=closed,ErrorCode=error,TableName=v.Table.Key.Name,
            ServerUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),State=closed?null:v.Table.Project(v.Player.Id)}));
        v.Published=v.Table.Version;
    }
    private static void Send(List<Delivery> output)
    {
        foreach(var d in output)
        try
        {
            if(d.Announcement!=null)PacketSender.SendGlobalMsg(d.Announcement,Color.White);
            else if(d.Quest is {} quest && d.View is {} questView &&
                ReferenceEquals(questView.Client.Entity,questView.Player) && questView.Player.LoginTime==questView.Login)
                questView.Player.UpdateBlackjackQuestTasks(quest);
            else if(d.View is {} v && d.Packet is {} p && ReferenceEquals(v.Client.Entity,v.Player) && v.Player.LoginTime==v.Login)v.Client.Send(p);
        }
        catch(Exception error){PokerInventoryBridge.Log(error);}
    }
}
