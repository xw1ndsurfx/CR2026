using Intersect.Editor.Content;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Utilities;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;


public partial class EventCommandText : UserControl
{

    private readonly FrmEvent mEventEditor;

    private ShowTextCommand mMyCommand;

    public EventCommandText(ShowTextCommand refCommand, FrmEvent editor)
    {
        InitializeComponent();
        mMyCommand = refCommand;
        mEventEditor = editor;
        InitLocalization();
        string T(string text)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var colour=new Intersect.Editor.Forms.Styling.LogiklikButton{Name="SelectionColor",Text=T("Text colour"),Left=48,Top=18,Width=102,Height=20,Enabled=false};
        grpShowText.Controls.Add(colour);
        txtShowText.HideSelection=false;
        txtShowText.MouseUp+=(_,_)=>colour.Enabled=txtShowText.SelectionLength>0;
        txtShowText.KeyUp+=(_,_)=>colour.Enabled=txtShowText.SelectionLength>0;
        colour.Click+=(_,_)=>
        {
            var start=txtShowText.SelectionStart;var length=txtShowText.SelectionLength;
            if(length==0)return;
            using var dialog=new ColorDialog{FullOpen=true};
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            ApplySelectionColor(start,length,dialog.Color);
        };
        txtShowText.Text = mMyCommand.Text;
        cmbFace.Items.Clear();
        cmbFace.Items.Add(Strings.General.None);
        Intersect.Editor.Forms.Styling.SearchableChoices.AddTextureNames(cmbFace, GameContentManager.TextureType.Face);
        cmbFace.SelectedIndex = Math.Max(0, cmbFace.Items.IndexOf(TextUtils.NullToNone(mMyCommand.Face)));

        UpdateFacePreview();
    }

    private void ApplySelectionColor(int start,int length,System.Drawing.Color color)
    {
        txtShowText.Text=Intersect.Framework.Core.GameObjects.Events.EventTextColorMarkup.ApplySelection(txtShowText.Text,start,length,new Intersect.Color(255,color.R,color.G,color.B));
        txtShowText.SelectionStart=Math.Min(start,txtShowText.TextLength);txtShowText.SelectionLength=0;txtShowText.Focus();
    }

    private void InitLocalization()
    {
        grpShowText.Text = Strings.EventShowText.title;
        lblText.Text = Strings.EventShowText.text;
        lblFace.Text = Strings.EventShowText.face;
        lblCommands.Text = Strings.EventShowText.commands;
        btnSave.Text = Strings.EventShowText.okay;
        btnCancel.Text = Strings.EventShowText.cancel;
    }

    private void UpdateFacePreview()
    {
        if (File.Exists("resources/faces/" + cmbFace.Text))
        {
            pnlFace.BackgroundImage = new Bitmap("resources/faces/" + cmbFace.Text);
        }
        else
        {
            pnlFace.BackgroundImage = null;
        }
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        mMyCommand.Text = txtShowText.Text;
        mMyCommand.Face = TextUtils.SanitizeNone(cmbFace?.Text);
        mEventEditor.FinishCommandEdit();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        mEventEditor.CancelCommandEdit();
    }

    private void cmbFace_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateFacePreview();
    }

    private void lblCommands_Click(object sender, EventArgs e)
    {
        BrowserUtils.Open("http://www.ascensiongamedev.com/community/topic/749-event-text-variables/");
    }

}
