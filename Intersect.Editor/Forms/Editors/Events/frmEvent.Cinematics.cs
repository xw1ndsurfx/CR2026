using Intersect.Editor.Forms.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
namespace Intersect.Editor.Forms.Editors.Events;

public partial class FrmEvent
{
    private void AddCinematicCommands()
    {
        var root = new TreeNode(CinematicText.T("Title")) { Name = "cinematics" };
        root.Nodes.Add(new TreeNode(CinematicText.T("PlayCinematic")) { Tag = (int)EventCommandType.PlayCinematic });
        lstCommands.Nodes.Add(root);
    }
}
