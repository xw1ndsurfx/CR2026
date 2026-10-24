using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.General;
namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private FrmCinematicDirector? _cinematicDirector;
    private void AddCinematicDirectorMenu()
    {
        var menu = new ToolStripMenuItem(CinematicText.T("Title")) { Name = "royalCinematicDirector" };
        menu.Click += (_, _) =>
        {
            if (Globals.CurrentMap == null) { MessageBox.Show(this, CinematicText.T("MapRequired")); return; }
            if (_cinematicDirector is { IsDisposed: false }) { _cinematicDirector.Activate(); return; }
            _cinematicDirector = new FrmCinematicDirector(); _cinematicDirector.Show(this);
        };
        contentEditorsToolStripMenuItem.DropDownItems.Add(menu);
    }
}
