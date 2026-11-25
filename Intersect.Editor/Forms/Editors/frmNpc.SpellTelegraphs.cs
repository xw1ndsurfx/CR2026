using System.Windows.Forms;
using DarkUI.Controls;

namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Optional native single-target spell telegraphs; other spells are intentionally
/// unchanged to preserve projectile/AoE behavior and native spell effects.
/// </summary>
public partial class FrmNpc
{
    private readonly DarkCheckBox _chkSpellTelegraph = new();
    private readonly DarkNumericUpDown _nudSpellTelegraphWindup = new();
    private bool _loadingSpellTelegraph;

    private void BuildSpellTelegraphControls()
    {
        _chkSpellTelegraph.Text = TelegraphText("Warn before targeted hostile spells");
        _chkSpellTelegraph.SetBounds(12, 518, 275, 26);
        _chkSpellTelegraph.CheckedChanged += (_, _) =>
        {
            if (!_loadingSpellTelegraph && mEditorItem != null)
                mEditorItem.AdvancedSpellTelegraphEnabled = _chkSpellTelegraph.Checked;
            UpdateSpellTelegraphControlState();
        };
        _grpCombatTelegraph.Controls.Add(_chkSpellTelegraph);

        _grpCombatTelegraph.Controls.Add(new Label
        {
            Text = TelegraphText("Spell warning duration (ms):"),
            Location = new System.Drawing.Point(12, 550),
            Size = new System.Drawing.Size(275, 20),
            ForeColor = System.Drawing.Color.Gainsboro,
        });
        _nudSpellTelegraphWindup.SetBounds(12, 572, 275, 26);
        _nudSpellTelegraphWindup.Minimum = 250;
        _nudSpellTelegraphWindup.Maximum = 2000;
        _nudSpellTelegraphWindup.Increment = 50;
        _nudSpellTelegraphWindup.Value = 650;
        _nudSpellTelegraphWindup.ValueChanged += (_, _) =>
        {
            if (!_loadingSpellTelegraph && mEditorItem != null)
                mEditorItem.AdvancedSpellTelegraphWindupMs = (int)_nudSpellTelegraphWindup.Value;
        };
        _grpCombatTelegraph.Controls.Add(_nudSpellTelegraphWindup);
        _grpCombatTelegraph.Controls.Add(new Label
        {
            Text = TelegraphText("Only single-target spells without splash; native AoE and projectiles are unchanged."),
            Location = new System.Drawing.Point(12, 608),
            Size = new System.Drawing.Size(275, 43),
            ForeColor = System.Drawing.Color.Silver,
        });
        UpdateSpellTelegraphControlState();
    }

    private void LoadSpellTelegraphControls()
    {
        if (mEditorItem == null)
            return;
        _loadingSpellTelegraph = true;
        try
        {
            _chkSpellTelegraph.Checked = mEditorItem.AdvancedSpellTelegraphEnabled;
            _nudSpellTelegraphWindup.Value = Math.Clamp(
                mEditorItem.AdvancedSpellTelegraphWindupMs,
                (int)_nudSpellTelegraphWindup.Minimum,
                (int)_nudSpellTelegraphWindup.Maximum);
        }
        finally
        {
            _loadingSpellTelegraph = false;
        }
        UpdateSpellTelegraphControlState();
    }

    private void UpdateSpellTelegraphControlState()
    {
        _nudSpellTelegraphWindup.Enabled = _chkSpellTelegraph.Checked;
    }
}
