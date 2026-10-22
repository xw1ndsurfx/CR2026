using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;
internal sealed class EventCommandPlayerConvenience:UserControl
{
    internal EventCommandPlayerConvenience(EventCommand command,FrmEvent editor)
    {
        string T(string text)=>QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Size=new(520,230);
        var save=new Button {Text=T("Save"),Left=260,Top=170,Width=110};
        var cancel=new Button {Text=T("Cancel"),Left=380,Top=170,Width=110};
        if(command is ForceRainCommand rain)
        {
            var seconds=new NumericUpDown {Name="RainDuration",Left=250,Top=30,Width=230,Minimum=1,Maximum=604800,Value=Math.Clamp(rain.DurationSeconds,1,604800)};
            Controls.AddRange([new Label {Text=T("Duration (seconds)"),Left=20,Top=30,Width=220},seconds,
                new Label {Text=T("Starts rain on all configured outdoor maps. Indoor maps stay dry."),Left=20,Top=80,Width=460,Height=60}]);
            save.Click+=(_,_)=>{rain.DurationSeconds=(int)seconds.Value;editor.FinishCommandEdit();};
        }
        else if(command is PlayerBonusCommand bonus)
        {
            var percent=new NumericUpDown {Left=250,Top=30,Width=230,Minimum=1,Maximum=1000,Value=Math.Clamp(bonus.Percent,1,1000)};
            var seconds=new NumericUpDown {Left=250,Top=80,Width=230,Minimum=1,Maximum=604800,Value=Math.Clamp(bonus.DurationSeconds,1,604800)};
            Controls.AddRange([new Label {Text=T("Bonus (%)"),Left=20,Top=30,Width=220},percent,new Label {Text=T("Duration (seconds)"),Left=20,Top=80,Width=220},seconds]);
            save.Click+=(_,_)=>{bonus.Percent=(int)percent.Value;bonus.DurationSeconds=(int)seconds.Value;editor.FinishCommandEdit();};
        }
        else if(command is OpenAdventureLobbyCommand lobby)
        {
            var filter=new ComboBox {Left=250,Top=30,Width=230,DropDownStyle=ComboBoxStyle.DropDownList};
            filter.Items.Add(T("All activities"));
            foreach(var activity in Enum.GetValues<LobbyActivity>())filter.Items.Add(T(activity.ToString()));
            filter.SelectedIndex=Math.Clamp(lobby.ActivityFilter+1,0,filter.Items.Count-1);
            var prompt=new CheckBox {Left=20,Top=80,Width=460,Text=T("Prompt room creation"),Checked=lobby.PromptCreation};
            Controls.AddRange([new Label {Text=T("Filter activity"),Left=20,Top=30,Width=220},filter,prompt]);
            save.Click+=(_,_)=>{lobby.ActivityFilter=filter.SelectedIndex-1;lobby.PromptCreation=prompt.Checked;editor.FinishCommandEdit();};
        }
        cancel.Click+=(_,_)=>editor.CancelCommandEdit();Controls.AddRange([save,cancel]);
    }
}
