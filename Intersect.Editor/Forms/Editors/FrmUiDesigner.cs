using DarkUI.Forms;
using Intersect.Framework.Core.QualityOfLife;
namespace Intersect.Editor.Forms.Editors;
internal sealed class FrmUiDesigner:DarkForm
{
    internal FrmUiDesigner()
    {
        string T(string text)=>QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Text=T("UI Editor");Width=760;Height=390;StartPosition=FormStartPosition.CenterParent;
        var form=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        var folder=new TextBox{Width=690,Text=Path.GetFullPath("resources"),ReadOnly=true};
        form.Controls.Add(new Label{Text=T("Client resources folder"),AutoSize=true,ForeColor=System.Drawing.Color.Gainsboro});form.Controls.Add(folder);
        var browse=new Button{Text=T("Choose folder"),Width=220,Height=32};browse.Click+=(_,_)=>
        {using var picker=new FolderBrowserDialog();if(picker.ShowDialog(this)==DialogResult.OK)folder.Text=picker.SelectedPath;};form.Controls.Add(browse);
        var instructions=new Label{Text=T("Edit directly in the running client: open any interface, select its controls, drag or resize them, change properties and sounds, then save."),
            Width=690,Height=100,ForeColor=System.Drawing.Color.Gainsboro};form.Controls.Add(instructions);
        void Toggle(bool enabled)
        {
            var root=Directory.Exists(Path.Combine(folder.Text,"gui"))?folder.Text:Path.Combine(folder.Text,"resources");
            if(!Directory.Exists(Path.Combine(root,"gui"))){MessageBox.Show(this,T("Choose the client resources folder containing gui."));return;}
            var flag=Path.Combine(root,"gui","ui-designer.enabled");
            try{if(enabled)File.WriteAllText(flag,"enabled");else if(File.Exists(flag))File.Delete(flag);}
            catch(IOException){MessageBox.Show(this,T("Changes could not be saved."));}
        }
        var enable=new Button{Text=T("Enable live editing"),Width=300,Height=34};enable.Click+=(_,_)=>Toggle(true);
        var disable=new Button{Text=T("Finish editing"),Width=300,Height=34};disable.Click+=(_,_)=>Toggle(false);
        form.Controls.Add(enable);form.Controls.Add(disable);Controls.Add(form);MiniGameEditorTheme.Apply(this);
    }
}
