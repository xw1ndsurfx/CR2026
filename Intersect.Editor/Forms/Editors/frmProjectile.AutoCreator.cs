using DarkUI.Controls;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmProjectile
{
    private static string AutoText(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private void BuildProjectileAutoCreator()
    {
        var button = new DarkButton { Name = "ProjectileAutoCreator", Text = AutoText("Auto Projectile Creator"),
            Size = new(200, 31), Location = new(btnSave.Left - 212, btnSave.Top), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        button.Click += (_, _) => ShowProjectileAutoCreator(); Controls.Add(button); button.BringToFront();
        // The responsive workspace reparents Save into a footer after construction.
        // Follow that footer so its docked content cannot cover the creator button.
        btnSave.ParentChanged += (_, _) => {
            if (btnSave.Parent is not FlowLayoutPanel footer) return;
            button.Anchor = AnchorStyles.None; button.Size = new(200, 34);
            footer.Controls.Add(button);
        };
    }
    private void ShowProjectileAutoCreator()
    {
        if (mEditorItem == null) { MessageBox.Show(this, AutoText("Select or create a projectile first."), AutoText("Auto Projectile Creator")); return; }
        using var dialog = new FrmProjectileAutoCreator(mEditorItem);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } profile) return;
        profile.Apply(mEditorItem); mEditorItem.Name = dialog.ProjectileName;
        // Reset the range selector before rebuilding the generated animation list.
        lstAnimations.SelectedIndex = -1;
        UpdateEditor();
    }
}
