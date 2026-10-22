using System.Windows.Forms;
using Intersect.Framework.Core.QualityOfLife;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmResource
{
    private void ModernizeResourceEditor()
    {
        string T(string text)=>QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        MinimumSize=new Size(1000,720);Size=new Size(1180,800);
        btnSave.Anchor=btnCancel.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
        btnCancel.SetBounds(ClientSize.Width-btnCancel.Width-14,ClientSize.Height-btnCancel.Height-12,btnCancel.Width,btnCancel.Height);
        btnSave.SetBounds(btnCancel.Left-btnSave.Width-8,btnCancel.Top,btnSave.Width,btnSave.Height);
        pnlContainer.Height=Math.Max(400,btnSave.Top-pnlContainer.Top-12);
        pnlContainer.AutoScroll=false;
        pnlContainer.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        var tabs=new TabControl{Dock=DockStyle.Fill,Name="ResourceEditorSections"};
        TabPage Page(string title)
        {
            var page=new TabPage(T(title)){BackColor=System.Drawing.Color.FromArgb(36,37,41),ForeColor=System.Drawing.Color.Gainsboro,Padding=new Padding(12),AutoScroll=true};
            tabs.TabPages.Add(page);return page;
        }
        var general=Page("General");var rewards=Page("Rewards");var appearance=Page("Appearance");
        general.Controls.AddRange([grpGeneral,grpRequirements,grpCommonEvent]);
        grpGeneral.SetBounds(12,12,350,490);
        grpRequirements.SetBounds(376,12,350,120);
        grpCommonEvent.SetBounds(376,146,350,100);
        var checks=new FlowLayoutPanel{Name="ResourceBehaviorOptions",Location=new System.Drawing.Point(8,290),Size=new Size(332,180),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        foreach(var check in new Control[]{chkWalkableBefore,chkWalkableAfter,chkUseExplicitMaxHealthForResourceStates,_fallingTree})
        {
            check.AutoSize=false;check.Size=new Size(324,38);check.Margin=new Padding(0,2,0,2);checks.Controls.Add(check);
        }
        grpGeneral.Controls.Add(checks);
        foreach(Control control in grpGeneral.Controls)
            if(control is ComboBox or TextBox or NumericUpDown)control.Width=Math.Max(control.Width,grpGeneral.Width-control.Left-14);
        rewards.Controls.Add(grpDrops);grpDrops.Dock=DockStyle.Fill;
        var split=new SplitContainer{Dock=DockStyle.Fill,Size=new Size(780,550),SplitterDistance=370,BackColor=general.BackColor};
        var form=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(12)};
        void Field(string title,Control input)
        {
            var label=new Label{Text=T(title),AutoSize=true,Margin=new Padding(3,9,12,3),ForeColor=System.Drawing.Color.Gainsboro};
            input.Dock=DockStyle.Fill;input.Margin=new Padding(3,6,3,6);form.Controls.Add(label);form.Controls.Add(input);
        }
        lstDrops.Dock=DockStyle.Fill;split.Panel1.Controls.Add(lstDrops);
        Field("Item",cmbDropItem);Field("Minimum quantity",nudDropMinAmount);Field("Maximum quantity",nudDropMaxAmount);Field("Chance (%)",nudDropChance);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};actions.Controls.AddRange([btnDropAdd,btnDropRemove]);
        split.Panel2.Controls.Add(form);split.Panel2.Controls.Add(actions);actions.Dock=DockStyle.Bottom;
        grpDrops.Controls.Clear();grpDrops.Controls.Add(split);
        appearance.Controls.Add(grpGraphics);grpGraphics.Dock=DockStyle.Fill;
        pnlContainer.Controls.Add(tabs);tabs.BringToFront();
        MaximizeBox=true;MiniGameEditorTheme.Apply(tabs);
    }
}
