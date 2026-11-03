using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Exportation;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private FrmExportationAssets? _exportationAssets;

    private void AddExportationAssetsMenu()
    {
        var menu = GetOrCreateMiniGameEditorsMenu();
        if (menu.DropDownItems.Cast<ToolStripItem>().Any(item => item.Name == "miniGame.ExportationAssets"))
            return;
        var item = new ToolStripMenuItem(ExportationText.Get("Exportation graphics and sounds",
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
            { Name = "miniGame.ExportationAssets" };
        item.Click += (_, _) =>
        {
            if (_exportationAssets is { IsDisposed: false })
            {
                EditorWindows.Show(_exportationAssets, this);
                _exportationAssets.BringToFront();
                return;
            }
            _exportationAssets = new FrmExportationAssets();
            EditorWindows.Show(_exportationAssets, this);
        };
        menu.DropDownItems.Add(item);
    }
}
