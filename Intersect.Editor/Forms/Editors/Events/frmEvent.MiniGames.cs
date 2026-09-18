using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

public partial class FrmEvent
{
    private bool _miniGamesBound;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e); // The existing Load handler localizes the original designer nodes first.
        if (_miniGamesBound) return;
        _miniGamesBound = true;
        var category = new TreeNode("Mini-Games") { Name = "mini-games" };
        category.Nodes.Add(new TreeNode("Start Mini-Game...")
            { Name = "start-mini-game", Tag = (int)EventCommandType.StartMiniGame });
        category.Nodes.Add(new TreeNode("Leave Mini-Game")
            { Name = "leave-mini-game", Tag = (int)EventCommandType.LeaveMiniGame });
        lstCommands.Nodes.Add(category);
        category.Expand();

        // Keep all legacy command paths unchanged. Only the two new commands use this editor.
        lstCommands.NodeMouseDoubleClick -= lstCommands_NodeMouseDoubleClick;
        lstCommands.NodeMouseDoubleClick += MiniGameCommandDoubleClick;
        btnEdit.Click -= btnEdit_Click;
        btnEdit.Click += MiniGameCommandEdit;
    }

    private void MiniGameCommandDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (!int.TryParse(e.Node.Tag?.ToString(), out var value) ||
            (value != (int)EventCommandType.StartMiniGame && value != (int)EventCommandType.LeaveMiniGame))
        {
            lstCommands_NodeMouseDoubleClick(sender, e);
            return;
        }
        if (mCurrentCommand < 0 || mCurrentCommand >= mCommandProperties.Count ||
            !mCommandProperties[mCurrentCommand].Editable) return;
        var target = mCommandProperties[mCurrentCommand];
        grpNewCommands.Hide();
        try
        {
            EventCommand command;
            if (value == (int)EventCommandType.StartMiniGame)
            {
                var start = new StartMiniGameCommand();
                using var dialog = new MiniGameCommandDialog(start);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                command = start;
            }
            else command = new LeaveMiniGameCommand();

            // Insert only after Save: cancelling the dialog must never alter an event list.
            var index = mIsInsert ? target.MyList.IndexOf(target.Cmd) : target.MyList.Count;
            if (index < 0) return;
            target.MyList.Insert(index, command);
            ListPageCommands();
        }
        finally { EnableButtons(); }
    }

    private void MiniGameCommandEdit(object sender, EventArgs e)
    {
        if (mCurrentCommand < 0 || mCurrentCommand >= mCommandProperties.Count) return;
        var target = mCommandProperties[mCurrentCommand];
        if (!target.Editable || target.MyIndex < 0 || target.MyIndex >= target.MyList.Count) return;
        var command = target.MyList[target.MyIndex];
        if (command is StartMiniGameCommand start)
        {
            using var dialog = new MiniGameCommandDialog(start);
            if (dialog.ShowDialog(this) == DialogResult.OK) ListPageCommands();
            EnableButtons();
        }
        else if (command is LeaveMiniGameCommand)
        {
            // Leave has no settings, just like the existing Open Bank / Release Player commands.
            EnableButtons();
        }
        else btnEdit_Click(sender, e);
    }
}
