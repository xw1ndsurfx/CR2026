using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmClass
{
    private void ModernizeClassWorkspace()
    {
        EditorWorkspaceLayout.Apply(this, grpClasses, pnlContainer, btnSave, btnCancel);
        pnlContainer.AutoScroll = false;
        var tabs = new TabControl { Name = "ClassEditorSections", Dock = DockStyle.Fill };
        FlowLayoutPanel Page(string title)
        {
            var page = new TabPage(QualityOfLifeText.Get(title,
                System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
            page.Controls.Add(flow); tabs.TabPages.Add(page); return flow;
        }
        Page("General").Controls.AddRange([grpGeneral, grpSpawnPoint]);
        Page("Appearance").Controls.Add(grpSprite);
        var stats = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
        };
        stats.Controls.AddRange([grpBaseStats, grpRegen]);
        Page("Stats").Controls.AddRange([stats, grpCombat]);
        // The existing experience-grid action swaps these two panels; keep that behavior.
        var progression = new Panel { Size = new Size(Math.Max(grpLeveling.Width, grpExpGrid.Width) + 16,
            Math.Max(grpLeveling.Height, grpExpGrid.Height) + 16) };
        progression.Controls.AddRange([grpLeveling, grpExpGrid]);
        grpLeveling.Location = grpExpGrid.Location = new System.Drawing.Point(8, 8);
        Page("Progression").Controls.Add(progression);
        Page("Starting items and spells").Controls.AddRange([grpSpawnItems, grpSpells]);
        pnlContainer.Controls.Add(tabs);
    }
}
