using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Animations;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmMenuAnimations : DarkForm
{
    public FrmMenuAnimations()
    {
        Text = "Main Menu animations";
        Size = new Size(1100, 600);
        MinimumSize = new Size(850, 450);
        StartPosition = FormStartPosition.CenterParent;
        var draft = JsonConvert.DeserializeObject<List<MenuAnimationDefinition>>(JsonConvert.SerializeObject(ClientConfiguration.Instance.MenuAnimations)) ?? [];
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        var animations = AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name).ToList();
        grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Animation", DataPropertyName = "AnimationId", DataSource = animations, DisplayMember = "Name", ValueMember = "Id" });
        foreach (var name in new[] { "XPercent", "YPercent", "ScalePercent", "TransparencyPercent" })
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, DataPropertyName = name });
        foreach (var name in new[] { "Loop", "Foreground" })
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = name, DataPropertyName = name });
        grid.DataSource = new BindingList<MenuAnimationDefinition>(draft);
        grid.DataError += (_, e) => e.ThrowException = false;
        var help = new Label { Dock = DockStyle.Top, Height = 60, Text = "Choose an existing animation. X/Y are screen percentages (50 = center). Foreground appears above menu graphics; clicks pass through. Definitions are copied into this installation's resources/config.json so they work before login. Distribute config.json and animation textures with the client." };
        var save = new Button { Dock = DockStyle.Bottom, Height = 40, Text = "Save client menu animations" };
        save.Click += (_, _) =>
        {
            grid.EndEdit();
            BindingContext[grid.DataSource].EndCurrentEdit();
            if (draft.Count > 32 || draft.Any(r => r.XPercent is < 0 or > 100 || r.YPercent is < 0 or > 100 || r.ScalePercent is < 1 or > 2000 || r.TransparencyPercent is < 0 or > 100))
            {
                MessageBox.Show(this, "Use X/Y 0-100, scale 1-2000 and transparency 0-100 (max 32 animations).");
                return;
            }
            foreach (var rule in draft)
            {
                if (AnimationDescriptor.Get(rule.AnimationId) is { } animation)
                {
                    rule.Lower = JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(animation.Lower));
                    rule.Upper = JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(animation.Upper));
                }
                else if (rule.Lower == null && rule.Upper == null)
                {
                    MessageBox.Show(this, "Select an existing animation for every row.");
                    return;
                }
            }
            ClientConfiguration.Instance.MenuAnimations = draft;
            ClientConfiguration.Instance.Save();
            Close();
        };
        Controls.Add(grid);
        Controls.Add(help);
        Controls.Add(save);
        MiniGameEditorTheme.Apply(this);
    }
}
