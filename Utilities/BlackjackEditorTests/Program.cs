using System.Windows.Forms;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Quests;
using Newtonsoft.Json;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var passed=0;var failed=0;
        void Test(string n,Action t){try{t();++passed;Console.WriteLine("PASS blackjack editor: "+n);}catch(Exception e){++failed;Console.Error.WriteLine("FAIL blackjack editor: "+n+"\n"+e);}}
        Test("Old poker events still select poker",()=>{var c=new StartMiniGameCommand();using var d=new MiniGameCommandDialog(c);Check(Get<ComboBox>(d,"MiniGameType").SelectedIndex==0,"default changed");});
        Test("Selecting blackjack preserves its independent wager and soft-17 settings",()=>
        {
            var c=new StartMiniGameCommand{TableId="bj-test",StartingChips=200};using var d=new MiniGameCommandDialog(c);d.Show();Application.DoEvents();
            Get<ComboBox>(d,"MiniGameType").SelectedIndex=1;Get<NumericUpDown>(d,"BlackjackMinimumBet").Value=10;
            Get<NumericUpDown>(d,"BlackjackMaximumBet").Value=100;Get<CheckBox>(d,"BlackjackHitSoft17").Checked=true;
            Get<Button>(d,"Save").PerformClick();Check(d.DialogResult==DialogResult.OK && c.Game==MiniGameType.Blackjack &&
                c.BlackjackMinimumBet==10 && c.BlackjackMaximumBet==100 && c.BlackjackHitSoft17 && c.StartingChips==200,"save");
            var copy=JsonConvert.DeserializeObject<StartMiniGameCommand>(JsonConvert.SerializeObject(c))!;Check(copy.Game==MiniGameType.Blackjack && copy.HasValidSettings(),"serialization");
        });
        Test("Cancel leaves a poker command and its NPC count untouched",()=>
        {
            var c=new StartMiniGameCommand{NpcPlayers=5};var before=JsonConvert.SerializeObject(c);using var d=new MiniGameCommandDialog(c);d.Show();Application.DoEvents();
            Get<ComboBox>(d,"MiniGameType").SelectedIndex=1;Check(Get<NumericUpDown>(d,"NpcPlayers").Maximum==4,"dealer slot not reserved");
            Get<Button>(d,"Cancel").PerformClick();Check(JsonConvert.SerializeObject(c)==before,"cancel mutated event");
        });
        Test("Blackjack event reopens as blackjack",()=>{var c=new StartMiniGameCommand{Game=MiniGameType.Blackjack,BlackjackMinimumBet=20,BlackjackMaximumBet=80};using var d=new MiniGameCommandDialog(c);
            Check(Get<ComboBox>(d,"MiniGameType").SelectedIndex==1 && Get<NumericUpDown>(d,"BlackjackMinimumBet").Value==20,"reopen");});
        Test("Blackjack disables Poker-only unlimited funding and exposes motion presets",()=>
        {
            var c=new StartMiniGameCommand{Game=MiniGameType.Blackjack,UnlimitedNpcBankroll=true};
            using var d=new MiniGameCommandDialog(c);d.Show();Application.DoEvents();
            var unlimited=Get<CheckBox>(d,"UnlimitedNpcBankroll");
            Check(!unlimited.Enabled && !unlimited.Checked,"unlimited funding remained active");
            Check(Get<NumericUpDown>(d,"BlackjackMinimumBet").Enabled && Get<NumericUpDown>(d,"BlackjackMaximumBet").Enabled,"blackjack wagers disabled");
            var speed=Get<ComboBox>(d,"ProceduralAnimationSpeed");
            Check(speed.Items.Cast<object>().Select(x=>x.ToString()).SequenceEqual(new[]{"Off","Fast","Normal","Cinematic"}),"motion presets");
        });
        Test("Blackjack quest objective enum entries are available to the quest editor",()=>
        {
            Check(Enum.IsDefined(typeof(QuestObjective),QuestObjective.BlackjackWinHands),"win hands missing");
            Check(Enum.IsDefined(typeof(QuestObjective),QuestObjective.BlackjackWinAmount),"win amount missing");
            Check(Enum.IsDefined(typeof(QuestObjective),QuestObjective.BlackjackReachLevel),"reach level missing");
            Check(Enum.IsDefined(typeof(QuestObjective),QuestObjective.BlackjackPlayHands),"play hands missing");
            Check((int)QuestObjective.BlackjackWinHands==7 && (int)QuestObjective.BlackjackPlayHands==10,"quest objective order");
        });
        Console.WriteLine($"{passed}/{passed+failed} blackjack editor groups passed.");Environment.ExitCode=failed==0?0:1;
    }
    private static T Get<T>(Control p,string name)where T:Control=>Children(p).OfType<T>().Single(c=>c.Name==name);
    private static IEnumerable<Control> Children(Control p){foreach(Control c in p.Controls){yield return c;foreach(var next in Children(c))yield return next;}}
    private static void Check(bool v,string m){if(!v)throw new InvalidOperationException(m);}
}
