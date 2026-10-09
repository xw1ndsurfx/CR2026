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
        // Each entry is a dedicated editor action while retaining the existing serialized command type.
        foreach (var definition in Intersect.Framework.Core.MiniGames.Configuration.MiniGameCatalog.All)
        {
            category.Nodes.Add(new TreeNode($"Start {definition.DisplayName}...")
            {
                Name = $"start-mini-game-{definition.Type}",
                Tag = definition.Type,
            });
        }
        category.Nodes.Add(new TreeNode("Leave Mini-Game")
            { Name = "leave-mini-game", Tag = (int)EventCommandType.LeaveMiniGame });
        lstCommands.Nodes.Add(category);
        category.Expand();

        // Keep all legacy command paths unchanged; each mini-game still serializes as StartMiniGameCommand.
        lstCommands.NodeMouseDoubleClick -= lstCommands_NodeMouseDoubleClick;
        lstCommands.NodeMouseDoubleClick += MiniGameCommandDoubleClick;
        btnEdit.Click -= btnEdit_Click;
        btnEdit.Click += MiniGameCommandEdit;
    }

    private void MiniGameCommandDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        var selectedGame = e.Node.Parent?.Name == "mini-games" &&
            e.Node.Tag is MiniGameType type ? type : (MiniGameType?)null;
        var isLeave = e.Node.Name == "leave-mini-game";
        if (selectedGame == null && !isLeave)
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
            if (selectedGame is { } miniGame)
            {
                var start = new StartMiniGameCommand { Game = miniGame };
                using var dialog = new MiniGameCommandDialog(start, miniGame);
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
            using var dialog = new MiniGameCommandDialog(start, start.Game);
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
