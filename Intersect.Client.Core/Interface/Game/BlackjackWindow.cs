using System.Globalization;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.MiniGames;
using SkinBase=Intersect.Client.Framework.Gwen.Skin.Base;
using Rectangle=Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game;

/// <summary>Borderless blackjack scene. Local player at the bottom; all gamblers face the dealer.</summary>
internal sealed partial class BlackjackWindow : Base
{
    private sealed record Placement(Base Control,int X,int Y,int W,int H,int Font);
    private readonly Canvas _canvas;private readonly Action<BlackjackRequestKind,long> _send;
    private readonly List<Placement> _placements=new();
    private readonly Label _title,_status,_bank,_dealerTotal,_result,_xp,_error,_rules,_backStatus;
    private readonly Label[] _names=new Label[5],_balances=new Label[5],_decisions=new Label[5];
    private readonly Label[,] _totals=new Label[5,2];
    private readonly BlackjackCardStrip[,] _cards=new BlackjackCardStrip[5,2];
    private readonly BlackjackCardStrip _dealer;
    private readonly ImagePanel[] _portraits=new ImagePanel[6],_backImages=new ImagePanel[6];
    private readonly Button _start,_bet,_hit,_stand,_double,_split,_refresh,_backToggle;
    private readonly Button[] _backButtons=new Button[6];
    private readonly TextBox _amount;
    private readonly PokerFlatPanel _tray;
    private readonly PokerScreenEffect _dealEffect,_victory;
    private BlackjackTableState? _state;private PokerSceneLayout _layout;
    private string _localError="";private float _fraction;private bool _destroyed;
    private int _localSeat,_lastLevel;private long _levelUpUntil;
    public bool ExitRequested {get;private set;}
    public BlackjackWindow(Canvas canvas,Action<BlackjackRequestKind,long> send):base(canvas,nameof(BlackjackWindow))
    {
        _canvas=canvas;_send=send;_layout=new(Math.Max(1,canvas.Width),Math.Max(1,canvas.Height));
        ShouldDrawBackground=false;MouseInputEnabled=true;KeyboardInputEnabled=false;
        _title=Label("BlackjackTitle",24,10,740,30,20);
        _status=Label("BlackjackTurn",24,46,940,30,14);
        Label("BlackjackDealer",408,81,260,27,18).Text="Marlow - Dealer";
        _bank=Label("BlackjackBank",380,109,360,26);
        _dealer=new(this,"BlackjackDealerCards");_dealerTotal=Label("BlackjackDealerTotal",380,211,340,26,14);
        _rules=Label("BlackjackRules",324,248,360,76);
        _result=Label("BlackjackResult",314,334,380,62,16);
        for(var i=0;i<6;i++)_portraits[i]=new ImagePanel(this,"BlackjackPortrait"+i){ShouldDrawBackground=false,MouseInputEnabled=false,IsHidden=true};
        for(var slot=0;slot<5;slot++)
        {
            var (x,y,w)=Panel(slot);
            _names[slot]=Label("BlackjackName"+slot,x+42,y,w-42,25,14);
            _balances[slot]=Label("BlackjackBalance"+slot,x+42,y+25,w-42,24);
            _decisions[slot]=Label("BlackjackDecision"+slot,x,y+49,w,24);
            for(var h=0;h<2;h++)
            {
                _cards[slot,h]=new(this,$"BlackjackHand{slot}_{h}");
                _totals[slot,h]=Label($"BlackjackTotal{slot}_{h}",x+h*w/2,y+(slot==0?138:128),w/2,28);
            }
        }
        Label("BlackjackBetLabel",40,582,112,28).Text="Wager (even)";
        _amount=new TextBox(this,"BlackjackBetAmount"){Font=Skin.DefaultFont,FontSize=12,Text="10"};Place(_amount,154,580,120,32,12);
        Interface.FocusComponents.Add(_amount);
        _bet=Button("BlackjackBet","Place bet",290,580,120,Bet);
        _start=Button("BlackjackStart","Start round",424,580,126,()=>Send(BlackjackRequestKind.Start));
        _error=Label("BlackjackError",564,580,400,35);
        _hit=Button("BlackjackHit","Hit",40,626,128,()=>Send(BlackjackRequestKind.Hit));
        _stand=Button("BlackjackStand","Stand",182,626,128,()=>Send(BlackjackRequestKind.Stand));
        _double=Button("BlackjackDouble","Double",324,626,128,()=>Send(BlackjackRequestKind.Double));
        _split=Button("BlackjackSplit","Split",466,626,128,()=>Send(BlackjackRequestKind.Split));
        _refresh=Button("BlackjackRefresh","Refresh",608,626,128,()=>Send(BlackjackRequestKind.Refresh));
        Button("BlackjackLeave","Leave table",750,626,214,()=>ExitRequested=true);
        _xp=Label("BlackjackExperience",40,675,570,28,14);
        _tray=new PokerFlatPanel(this,"BlackjackBackPicker"){IsHidden=true};Place(_tray,174,244,652,164);
        _backToggle=Button("BlackjackBacks","Card backs",620,675,160,()=>
        {
            if(_portraitTray!=null)_portraitTray.IsHidden=true;
            _tray.IsHidden=!_tray.IsHidden;
            if(!_tray.IsHidden)_tray.BringToFront();
        if(_portraitTray is {IsHidden:false})_portraitTray.BringToFront();
        });
        _backStatus=Label("BlackjackBackStatus",620,711,342,30);
        Label("BlackjackNotice",40,744,922,27).Text="Blackjack has its own XP and levels. Leaving after the deal stands; it does not cancel the wager.";
        for(var i=0;i<6;i++)
        {
            var id=i;
            _backImages[i]=new ImagePanel(_tray,"BlackjackBackPreview"+i){ShouldDrawBackground=false,MouseInputEnabled=false,IsHidden=true};
            var b=new Button(_tray,"BlackjackBack"+i){Font=Skin.DefaultFont,FontSize=12,Text=$"B{i+1} / Lv {MiniGameProgression.BackLevel(i)}"};
            b.Clicked+=(_,_)=>{if(_state!=null && MiniGameProgression.IsUnlocked(id,_state.Experience)){Send(BlackjackRequestKind.SelectBack,id);_tray.IsHidden=true;}};
            _backButtons[i]=b;
        }
        InitializeTableSkin();
        _dealEffect=new PokerScreenEffect(canvas);_victory=new PokerScreenEffect(canvas);ResizeToCanvas();
    }
    private static (int X,int Y,int W) Panel(int slot)=>slot switch
    {0=>(280,400,440),1=>(42,299,246),2=>(42,116,246),3=>(712,116,246),_=>(712,299,246)};
    public void ResizeToCanvas()
    {
        if(_destroyed)return;SetBounds(0,0,Math.Max(1,_canvas.Width),Math.Max(1,_canvas.Height));_layout=new(Width,Height);
        foreach(var p in _placements)
        {var r=_layout.Rect(p.X,p.Y,p.W,p.H);p.Control.SetBounds(r.X,r.Y,r.Width,r.Height);if(p.Control is Label l && p.Font>0)l.FontSize=_layout.FontSize(p.Font);}
        for(var i=0;i<6;i++)
        {var r=_layout.LocalRect(7+i*108,117,100,32);_backButtons[i].SetBounds(r.X,r.Y,r.Width,r.Height);_backButtons[i].FontSize=_layout.FontSize(12);}
        LayoutTableSkin();
    }
    public void Update(BlackjackClientModel model)
    {
        if(_destroyed || model.Current?.State is not {} s)return;
        if(Width!=_canvas.Width || Height!=_canvas.Height)ResizeToCanvas();_state=s;
        var me=s.Seats.Single(p=>p.PlayerId==model.Current.PlayerId);_localSeat=me.Seat;
        var currency=s.CurrencyItemId==Guid.Empty?"test chips":ItemDescriptor.GetName(s.CurrencyItemId);
        var now=Environment.TickCount64;var busy=s.Stage is BlackjackStage.Betting or BlackjackStage.Players or BlackjackStage.Dealer;
        _title.Text=$"BLACKJACK | {model.Current.TableName} | Round {s.HandId} | {currency}";
        _status.Text=s.Pending?"Saving balances - please wait":model.Pending?"Waiting for server":s.Stage switch
        {
            BlackjackStage.Waiting=>s.AutoStart?"Next betting window opens automatically":"Press Start round to open betting",
            BlackjackStage.Betting=>s.CanBet?$"Place an even wager: {s.MinimumBet} - {s.MaximumBet} | {model.Seconds(now)}s":
                $"Betting closes in {model.Seconds(now)}s | Waiting for other participants",
            BlackjackStage.Players=>s.ActingSeat==me.Seat?$"Your turn - hand {s.ActingHand+1} | {model.Seconds(now)}s":
                $"{s.Seats.FirstOrDefault(p=>p.Seat==s.ActingSeat)?.Name}'s turn | {model.Seconds(now)}s",
            BlackjackStage.Dealer=>s.DealerTotal<17 || s.DealerTotal==17 && s.DealerSoft && s.HitSoft17?"Marlow: Hit":"Marlow: Stand / settle",
            _=>s.AutoStart?"Round finished - next betting window shortly":"Round finished",
        };
        _bank.Text=$"Dealer bankroll: {s.Bank} {currency}";
        _rules.Text=$"Natural BLACKJACK pays 3:2\nOther wins 1:1 | {(s.HitSoft17?"Dealer hits soft 17":"Dealer stands on all 17")}\nOne split | No insurance or surrender";
        _dealer.Update(s.DealerCards,s.DealerHoleHidden,s.DealerBackId,_layout,420,168,220,70);
        _dealerTotal.Text=s.DealerCards.Length==0?"":s.DealerHoleHidden?$"Showing {s.DealerTotal} + hidden card":
            $"Total: {s.DealerTotal}"+(s.DealerTotal>21?" - BUST":s.DealerCards.Length==2 && s.DealerTotal==21?" - BLACKJACK":"");
        BlackjackCardStrip.Fit(_portraits[5],_dealer.Texture("poker_dealer.png"),_layout.Rect(392,110,54,61));
        for(var slot=0;slot<5;slot++)
        {
            var p=s.Seats.FirstOrDefault(p=>(p.Seat-me.Seat+5)%5==slot);var(x,y,w)=Panel(slot);
            _names[slot].Text=p==null?(_tableSkin?.Texture==null?"Empty seat":""):Short(p.Name+(p.PlayerId==me.PlayerId?" (you)":p.Npc?" [NPC]":""),27);
            _names[slot].TextColorOverride=p?.Seat==s.ActingSeat?Gold:Color.White;
            _balances[slot].Text=p==null?"":$"Balance: {p.Chips}";
            _decisions[slot].Text=p==null?"":p.Leaving?"Leaving":Short(p.LastAction,18);
            if(p!=null && s.Stage==BlackjackStage.Players && p.Seat==s.ActingSeat && string.IsNullOrWhiteSpace(_decisions[slot].Text))
                _decisions[slot].Text=p.PlayerId==me.PlayerId?"TURN":"ACTING";
            _decisions[slot].IsHidden=_tableSkin?.Texture!=null && string.IsNullOrWhiteSpace(_decisions[slot].Text);
            var portrait=p==null?null:p.Npc
                ?_dealer.Texture(PokerTableTheme.PortraitFile(PokerTableTheme.Portrait(p.Name,false)))
                :p.PlayerId==me.PlayerId?TablePortraitPreference.SelectedTexture():_dealer.Texture("poker_player.png");
            var portraitRect=slot==0?_layout.Rect(365,405,56,64):_layout.Rect(x+2,y+2,56,64);
            BlackjackCardStrip.Fit(_portraits[slot],portrait,portraitRect);
            for(var h=0;h<2;h++)
            {
                var hand=p!=null && p.Hands.Length>h?p.Hands[h]:null;
                var placeholder=p!=null && h==0 && (hand==null || hand.Cards.Length==0);
                if(slot==0)
                {
                    var split=p?.Hands.Length>1;
                    var cardX=split?350+h*155:420;
                    var cardW=split?140:160;
                    _cards[slot,h].Update(hand?.Cards??[],false,p?.CardBackId??0,_layout,cardX,326,cardW,58,placeholder);
                    var infoX=split?cardX:cardX-15;
                    var infoW=split?cardW:cardW+30;
                    var totalRect=_layout.Rect(infoX,386,infoW,32);
                    _totals[slot,h].SetBounds(totalRect.X,totalRect.Y,totalRect.Width,totalRect.Height);
                    _totals[slot,h].FontSize=_layout.FontSize(13);
                    _totals[slot,h].TextAlign=Pos.Center;
                }
                else
                {
                    _cards[slot,h].Update(hand?.Cards??[],false,p?.CardBackId??0,_layout,x+h*w/2,y+76,w/2-8,48,placeholder);
                }
                _totals[slot,h].Text=hand==null?"":$"TOTAL {hand.Total}   •   BET {hand.Bet}"+(hand.Outcome==BlackjackOutcome.Pending?"":$"   •   {hand.Outcome}");
                _totals[slot,h].TextColorOverride=p?.Seat==s.ActingSeat && h==s.ActingHand
                    ?Gold
                    :new Color(245,240,225);
            }
        }
        var net=me.Hands.Sum(h=>h.Net);
        _result.Text=s.Stage==BlackjackStage.Finished && !s.Pending?(net>0?$"You win +{net} {currency}":net<0?$"You lose {-net} {currency}":"Push / no wager") : "";
        _start.IsDisabled=model.Pending || s.Pending || busy || me.Leaving || me.Chips<s.MinimumBet;
        _bet.IsDisabled=_amount.IsDisabled=model.Pending || s.Pending || !s.CanBet;
        _hit.IsDisabled=model.Pending || s.Pending || !s.CanHit;_stand.IsDisabled=model.Pending || s.Pending || !s.CanStand;
        _double.IsDisabled=model.Pending || s.Pending || !s.CanDouble;_split.IsDisabled=model.Pending || s.Pending || !s.CanSplit;
        if(!_amount.HasFocus && long.TryParse(_amount.Text,out var current) && current<s.MinimumBet)_amount.Text=s.MinimumBet.ToString(CultureInfo.InvariantCulture);
        var level=MiniGameProgression.Level(s.Experience);var baseXp=MiniGameProgression.ExperienceAtLevel(level);
        var required=level<MiniGameProgression.MaximumLevel?MiniGameProgression.ExperienceAtLevel(level+1)-baseXp:1;
        _fraction=level==MiniGameProgression.MaximumLevel?1:(s.Experience-baseXp)/(float)required;
        _xp.Text=level==MiniGameProgression.MaximumLevel?$"Blackjack - Level {level} - MASTERED":$"Blackjack - Level {level} | XP {s.Experience-baseXp} / {required}";
        if(_lastLevel>0 && level>_lastLevel)_levelUpUntil=now+5000;_lastLevel=level;
        if(now<_levelUpUntil)_result.Text+=$"\nLevel up! Blackjack {level}";
        _backStatus.Text=$"B{me.SelectedBackId+1}"+(me.SelectedBackId!=me.CardBackId?" - applies next round":" - selected");
        for(var i=0;i<6;i++)
        {
            _backButtons[i].IsDisabled=model.Pending || s.Pending || !MiniGameProgression.IsUnlocked(i,s.Experience);
            _backButtons[i].TextColorOverride=i==me.SelectedBackId?Gold:Color.White;
            BlackjackCardStrip.Fit(_backImages[i],_dealer.Back(i),_layout.LocalRect(32+i*108,22,48,74));
        }
        _error.Text=_localError.Length>0?_localError:model.ErrorCode;
        UpdateTableSkin(model,s,me);
        UpdateProceduralMotions(s,me);
        if(!_tray.IsHidden)_tray.BringToFront();
        if(model.ObserveDeal())_dealEffect.Play(s.DealAnimationId);
        if(model.Victories.Observe(model.Current.TableInstanceId,me.PlayerId,s.HandId,s.Stage==BlackjackStage.Finished,s.NetWin))_victory.Play(s.VictoryAnimationId);
        _dealEffect.Update();_victory.Update();
    }
    private static readonly Color Gold=new(231,194,112);
    protected override void Render(SkinBase skin)
    {
        var r=skin.Renderer;r.DrawColor=new Color(240,14,19,17);r.DrawFilledRect(new Rectangle(0,0,Width,Height));
        RenderTableSkinBackground(r);
        RenderLocalHandInfoBackground(r);
        if(_tableSkin?.Texture==null)
        {
            Ellipse(skin,new Color(121,76,39),130,97,740,456);Ellipse(skin,new Color(37,91,59),147,114,706,423);
            foreach(var slot in Enumerable.Range(0,5))
            {
                var(x,y,w)=Panel(slot);Fill(skin,new Color(25,36,30),x-6,y-7,w+12,slot==0?177:167);
                var p=_state?.Seats.FirstOrDefault(s=>(s.Seat-_localSeat+5)%5==slot);
                if(p!=null && _portraits[slot].IsHidden)
                {Fill(skin,new Color(174,140,105),x+9,y+2,17,18);Fill(skin,new Color(82,71,50),x+2,y+22,31,22);}
                if(p!=null && p.Seat==_state?.ActingSeat)Fill(skin,Gold,x-6,y-7,w+12,2);
            }
        }
        if(_tableSkin?.Texture!=null)RenderTableActionPanel(skin.Renderer);
        else Fill(skin,new Color(21,29,25),24,570,952,164);
        Fill(skin,new Color(76,135,89),40,708,550,18);Fill(skin,new Color(14,32,21),41,709,548,16);
        var filled=(int)(546*Math.Clamp(_fraction,0,1));Fill(skin,new Color(46,190,83),42,710,filled,14);
        Fill(skin,new Color(102,230,128),42,710,filled,3);
        RenderProceduralMotions(skin.Renderer);
    }
    private void Ellipse(SkinBase skin,Color color,int x,int y,int w,int h)
    {for(var row=0;row<h;row+=3){var t=(row+1.5f-h/2f)/(h/2f);var half=(int)(w/2f*Math.Sqrt(Math.Max(0,1-t*t)));Fill(skin,color,x+w/2-half,y+row,half*2,Math.Min(3,h-row));}}
    private void Fill(SkinBase skin,Color color,int x,int y,int w,int h)
    {if(w<1 || h<1)return;var r=_layout.Rect(x,y,w,h);skin.Renderer.DrawColor=color;skin.Renderer.DrawFilledRect(new Rectangle(r.X,r.Y,r.Width,r.Height));}
    private void Bet()
    {
        if(_state==null || !long.TryParse(_amount.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var n) ||
            n<_state.MinimumBet || n>_state.MaximumBet || n%2!=0){_localError="Choose an even wager within the displayed limit.";return;}
        Send(BlackjackRequestKind.Bet,n);
    }
    private void Send(BlackjackRequestKind kind,long n=0){_localError="";_send(kind,n);}
    public void Destroy(){if(_destroyed)return;_destroyed=true;_dealEffect.Dispose();_victory.Dispose();Interface.FocusComponents.Remove(_amount);Hide();Parent?.RemoveChild(this,false);Dispose();}
    private static string Short(string s,int length)=>s.Length<=length?s:s[..(length-3)]+"...";
    private void Place(Base c,int x,int y,int w,int h,int font=0){c.Dock=Pos.None;_placements.Add(new(c,x,y,w,h,font));}
    private Label Label(string name,int x,int y,int w,int h,int font=12)
    {var l=new Label(this,name){Font=Skin.DefaultFont,FontSize=font,AutoSizeToContents=false,TextColorOverride=Color.White,MouseInputEnabled=false,KeyboardInputEnabled=false};Place(l,x,y,w,h,font);return l;}
    private Button Button(string name,string text,int x,int y,int w,Action click)
    {var b=new Button(this,name){Font=Skin.DefaultFont,FontSize=12,Text=text};Place(b,x,y,w,32,12);b.Clicked+=(_,_)=>click();return b;}
}
