using System.Globalization;
using System.Windows.Forms;
using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Combat;
using Intersect.Framework.Core.GameObjects.NPCs;

namespace Intersect.Editor.Forms.Editors;

/// <summary>Boss-only combo authoring; all fields persist in NPCDescriptor.</summary>
public partial class FrmNpc
{
    private readonly DarkGroupBox _grpBossCombo = new();
    private readonly DarkCheckBox _chkBossCombo = new();
    private readonly DarkNumericUpDown _nudBossComboCount = new();
    private readonly DarkComboBox _cmbBossComboSecond = new();
    private readonly DarkComboBox _cmbBossComboThird = new();
    private readonly DarkNumericUpDown _nudBossComboFollowup = new();
    private readonly DarkNumericUpDown _nudBossComboPreview = new();
    private bool _updatingBossCombo;

    private void BuildBossComboControls()
    {
        _grpBossCombo.Text = TelegraphText("Boss attack combos");
        _grpBossCombo.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        _grpBossCombo.BorderColor = System.Drawing.Color.FromArgb(100, 78, 55);
        _grpBossCombo.ForeColor = System.Drawing.Color.Gainsboro;
        _grpBossCombo.Size = new System.Drawing.Size(302, 394);
        _grpBossCombo.TabStop = false;

        _chkBossCombo.Text = TelegraphText("Enable multi-hit boss attacks");
        _chkBossCombo.SetBounds(12, 23, 275, 28);
        _chkBossCombo.CheckedChanged += (_, _) =>
        {
            if (!_updatingBossCombo && mEditorItem != null)
                mEditorItem.AdvancedBossComboEnabled = _chkBossCombo.Checked;
            UpdateBossComboControlState();
        };
        _grpBossCombo.Controls.Add(_chkBossCombo);
        AddBossComboLabel("Warn before each strike; no invisible hits.", 56, 36);

        AddBossComboLabel("Number of attacks:", 102);
        _nudBossComboCount.SetBounds(12, 123, 275, 26);
        _nudBossComboCount.Minimum = 2;
        _nudBossComboCount.Maximum = AdvancedBossComboPlan.MaximumSteps;
        _nudBossComboCount.Value = 2;
        _nudBossComboCount.ValueChanged += (_, _) =>
        {
            if (!_updatingBossCombo && mEditorItem != null)
                mEditorItem.AdvancedBossComboCount = (int)_nudBossComboCount.Value;
            UpdateBossComboControlState();
        };
        _grpBossCombo.Controls.Add(_nudBossComboCount);

        AddBossComboLabel("Attack 2 shape:", 155);
        SetupBossShapeCombo(_cmbBossComboSecond, 177, 2,
            value => { if (!_updatingBossCombo && mEditorItem != null) mEditorItem.AdvancedBossComboSecondShape = value; });
        AddBossComboLabel("Attack 3 shape:", 211);
        SetupBossShapeCombo(_cmbBossComboThird, 233, 3,
            value => { if (!_updatingBossCombo && mEditorItem != null) mEditorItem.AdvancedBossComboThirdShape = value; });

        AddBossComboLabel("Next warning duration (ms):", 266);
        _nudBossComboFollowup.SetBounds(12, 288, 275, 26);
        _nudBossComboFollowup.Minimum = 250;
        _nudBossComboFollowup.Maximum = 2000;
        _nudBossComboFollowup.Increment = 50;
        _nudBossComboFollowup.Value = 550;
        _nudBossComboFollowup.ValueChanged += (_, _) =>
        {
            if (!_updatingBossCombo && mEditorItem != null)
                mEditorItem.AdvancedBossComboFollowupWindupMs = (int)_nudBossComboFollowup.Value;
        };
        _grpBossCombo.Controls.Add(_nudBossComboFollowup);

        AddBossComboLabel("Preview attack number:", 320);
        _nudBossComboPreview.SetBounds(12, 342, 275, 26);
        _nudBossComboPreview.Minimum = 1;
        _nudBossComboPreview.Maximum = AdvancedBossComboPlan.MaximumSteps;
        _nudBossComboPreview.Value = 1;
        _nudBossComboPreview.ValueChanged += (_, _) =>
            _combatTelegraphPreview.Invalidate();
        _grpBossCombo.Controls.Add(_nudBossComboPreview);

        pnlContainer.Controls.Add(_grpBossCombo);
        UpdateBossComboControlState();
    }

    private void AddBossComboLabel(string key, int top, int height = 20)
    {
        _grpBossCombo.Controls.Add(new Label
        {
            Text = TelegraphText(key),
            Location = new System.Drawing.Point(12, top),
            Size = new System.Drawing.Size(275, height),
            ForeColor = System.Drawing.Color.Gainsboro,
        });
    }

    private void SetupBossShapeCombo(DarkComboBox combo, int top, int initial, Action<int> changed)
    {
        combo.SetBounds(12, top, 275, 26);
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.AddRange(new object[]
        {
            TelegraphText("Target tile"), TelegraphText("Straight line"),
            TelegraphText("Forward cone"), TelegraphText("Circle around target"),
        });
        combo.SelectedIndexChanged += (_, _) =>
        {
            changed(Math.Max(0, combo.SelectedIndex));
            _combatTelegraphPreview.Invalidate();
        };
        _grpBossCombo.Controls.Add(combo);
        _updatingBossCombo = true;
        try { combo.SelectedIndex = initial; }
        finally { _updatingBossCombo = false; }
    }

    private void UpdateBossComboControlState()
    {
        var enabled = _chkIsBoss.Checked && _chkBossCombo.Checked;
        _chkBossCombo.Enabled = _chkIsBoss.Checked;
        _nudBossComboCount.Enabled = enabled;
        _cmbBossComboSecond.Enabled = enabled;
        _cmbBossComboThird.Enabled = enabled && _nudBossComboCount.Value >= 3;
        _nudBossComboFollowup.Enabled = enabled;
        _nudBossComboPreview.Enabled = enabled;
        _nudBossComboPreview.Maximum = enabled ? _nudBossComboCount.Value : 1;
        _combatTelegraphPreview.Invalidate();
    }

    private void LoadBossComboControls()
    {
        if (mEditorItem == null)
            return;

        _updatingBossCombo = true;
        try
        {
            _chkBossCombo.Checked = mEditorItem.AdvancedBossComboEnabled;
            _nudBossComboCount.Value = Math.Clamp(mEditorItem.AdvancedBossComboCount, 2, 3);
            _cmbBossComboSecond.SelectedIndex = Math.Clamp(mEditorItem.AdvancedBossComboSecondShape, 0, 3);
            _cmbBossComboThird.SelectedIndex = Math.Clamp(mEditorItem.AdvancedBossComboThirdShape, 0, 3);
            _nudBossComboFollowup.Value = Math.Clamp(
                mEditorItem.AdvancedBossComboFollowupWindupMs, 250, 2000);
            _nudBossComboPreview.Value = 1;
        }
        finally
        {
            _updatingBossCombo = false;
        }
        UpdateBossComboControlState();
    }
}
