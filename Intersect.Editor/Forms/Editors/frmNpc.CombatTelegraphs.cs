using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DarkUI.Controls;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Combat;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Author melee telegraphs on existing NPC descriptors. Shared geometry guarantees
/// that the editor preview depicts exactly the server's warning footprint.
/// </summary>
public partial class FrmNpc
{
    private readonly DarkGroupBox _grpCombatTelegraph = new();
    private readonly DarkCheckBox _chkCombatTelegraph = new();
    private readonly DarkComboBox _cmbCombatTelegraphShape = new();
    private readonly DarkNumericUpDown _nudCombatTelegraphRange = new();
    private readonly DarkNumericUpDown _nudCombatTelegraphWindup = new();
    private readonly Panel _combatTelegraphPreview = new();
    private readonly DarkCheckBox _chkBossEnrage = new();
    private bool _updatingCombatTelegraph;

    private static string TelegraphText(string source) =>
        QualityOfLifeText.Get(source, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private void BuildCombatTelegraphControls()
    {
        _grpCombatTelegraph.Text = TelegraphText("Attack telegraphs");
        _grpCombatTelegraph.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        _grpCombatTelegraph.BorderColor = System.Drawing.Color.FromArgb(100, 78, 55);
        _grpCombatTelegraph.ForeColor = System.Drawing.Color.Gainsboro;
        _grpCombatTelegraph.Size = new System.Drawing.Size(302, 525);
        _grpCombatTelegraph.TabStop = false;

        _chkCombatTelegraph.Text = TelegraphText("Warn before this NPC attacks");
        _chkCombatTelegraph.SetBounds(12, 27, 277, 26);
        _chkCombatTelegraph.CheckedChanged += (_, _) =>
        {
            if (!_updatingCombatTelegraph && mEditorItem != null)
                mEditorItem.AdvancedTelegraphEnabled = _chkCombatTelegraph.Checked;
            UpdateCombatTelegraphControlState();
        };
        _grpCombatTelegraph.Controls.Add(_chkCombatTelegraph);
        _grpCombatTelegraph.Controls.Add(new Label
        {
            Text = TelegraphText("Bosses already warn by default."),
            Location = new System.Drawing.Point(12, 57), Size = new System.Drawing.Size(275, 36),
            ForeColor = System.Drawing.Color.Silver,
        });

        AddCombatTelegraphLabel("Area pattern:", 95);
        _cmbCombatTelegraphShape.SetBounds(12, 117, 275, 26);
        _cmbCombatTelegraphShape.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCombatTelegraphShape.Items.AddRange(new object[]
        {
            TelegraphText("Target tile"), TelegraphText("Straight line"),
            TelegraphText("Forward cone"), TelegraphText("Circle around target"),
        });
        _cmbCombatTelegraphShape.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingCombatTelegraph && mEditorItem != null)
                mEditorItem.AdvancedTelegraphShape = Math.Max(0, _cmbCombatTelegraphShape.SelectedIndex);
            UpdateCombatTelegraphControlState();
        };
        _grpCombatTelegraph.Controls.Add(_cmbCombatTelegraphShape);

        AddCombatTelegraphLabel("Reach (tiles):", 156);
        _nudCombatTelegraphRange.SetBounds(12, 177, 275, 26);
        _nudCombatTelegraphRange.Minimum = 1;
        _nudCombatTelegraphRange.Maximum = CombatTelegraphGeometry.MaximumRange;
        _nudCombatTelegraphRange.Value = 1;
        _nudCombatTelegraphRange.ValueChanged += (_, _) =>
        {
            if (!_updatingCombatTelegraph && mEditorItem != null)
                mEditorItem.AdvancedTelegraphRange = (int)_nudCombatTelegraphRange.Value;
            _combatTelegraphPreview.Invalidate();
        };
        _grpCombatTelegraph.Controls.Add(_nudCombatTelegraphRange);

        AddCombatTelegraphLabel("Warning duration (ms, 0 = server):", 215);
        _nudCombatTelegraphWindup.SetBounds(12, 236, 275, 26);
        _nudCombatTelegraphWindup.Minimum = 0;
        _nudCombatTelegraphWindup.Maximum = 2000;
        _nudCombatTelegraphWindup.Increment = 50;
        _nudCombatTelegraphWindup.Value = 0;
        _nudCombatTelegraphWindup.ValueChanged += (_, _) =>
        {
            if (!_updatingCombatTelegraph && mEditorItem != null)
                mEditorItem.AdvancedTelegraphWindupMs = (int)_nudCombatTelegraphWindup.Value;
        };
        _grpCombatTelegraph.Controls.Add(_nudCombatTelegraphWindup);

        AddCombatTelegraphLabel("Preview (gold: monster, blue: target)", 278);
        _combatTelegraphPreview.SetBounds(12, 302, 275, 125);
        _combatTelegraphPreview.BackColor = System.Drawing.Color.FromArgb(27, 25, 29);
        _combatTelegraphPreview.Paint += PaintCombatTelegraphPreview;
        _grpCombatTelegraph.Controls.Add(_combatTelegraphPreview);

        _chkBossEnrage.Text = TelegraphText("Enrage under 35% HP (boss only)");
        _chkBossEnrage.SetBounds(12, 438, 275, 25);
        _chkBossEnrage.CheckedChanged += (_, _) =>
        {
            if (!_updatingCombatTelegraph && mEditorItem != null)
                mEditorItem.AdvancedBossEnrageEnabled = _chkBossEnrage.Checked;
        };
        _grpCombatTelegraph.Controls.Add(_chkBossEnrage);
        _grpCombatTelegraph.Controls.Add(new Label
        {
            Text = TelegraphText("Faster warnings and stronger hits in phase two."),
            Location = new System.Drawing.Point(12, 471),
            Size = new System.Drawing.Size(275, 35),
            ForeColor = System.Drawing.Color.Silver,
        });

        pnlContainer.Controls.Add(_grpCombatTelegraph);
        _updatingCombatTelegraph = true;
        try { _cmbCombatTelegraphShape.SelectedIndex = 0; }
        finally { _updatingCombatTelegraph = false; }
        UpdateCombatTelegraphControlState();
    }

    private void AddCombatTelegraphLabel(string key, int top)
    {
        _grpCombatTelegraph.Controls.Add(new Label
        {
            Text = TelegraphText(key),
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(12, top),
            Size = new System.Drawing.Size(275, 20),
        });
    }

    private void UpdateCombatTelegraphControlState()
    {
        var enabled = _chkCombatTelegraph.Checked || _chkIsBoss.Checked;
        _cmbCombatTelegraphShape.Enabled = enabled;
        _nudCombatTelegraphRange.Enabled = enabled &&
            _cmbCombatTelegraphShape.SelectedIndex != (int)CombatTelegraphShape.TargetTile;
        _nudCombatTelegraphWindup.Enabled = enabled;
        _combatTelegraphPreview.Enabled = enabled;
        _chkBossEnrage.Enabled = _chkIsBoss.Checked;
        _combatTelegraphPreview.Invalidate();
    }

    private void LoadCombatTelegraphControls()
    {
        if (mEditorItem == null)
            return;

        _updatingCombatTelegraph = true;
        try
        {
            _chkCombatTelegraph.Checked = mEditorItem.AdvancedTelegraphEnabled;
            _chkBossEnrage.Checked = mEditorItem.AdvancedBossEnrageEnabled;
            _cmbCombatTelegraphShape.SelectedIndex = Math.Clamp(
                mEditorItem.AdvancedTelegraphShape, 0, _cmbCombatTelegraphShape.Items.Count - 1);
            _nudCombatTelegraphRange.Value = Math.Clamp(
                mEditorItem.AdvancedTelegraphRange, (int)_nudCombatTelegraphRange.Minimum,
                (int)_nudCombatTelegraphRange.Maximum);
            _nudCombatTelegraphWindup.Value = Math.Clamp(
                mEditorItem.AdvancedTelegraphWindupMs, (int)_nudCombatTelegraphWindup.Minimum,
                (int)_nudCombatTelegraphWindup.Maximum);
        }
        finally
        {
            _updatingCombatTelegraph = false;
        }

        UpdateCombatTelegraphControlState();
    }

    private void PaintCombatTelegraphPreview(object? sender, PaintEventArgs e)
    {
        const int grid = 9;
        const int tileSize = 12;
        var offsetX = (_combatTelegraphPreview.ClientSize.Width - grid * tileSize) / 2;
        const int offsetY = 7;
        var shape = AdvancedBossComboPlan.ShapeForStep(
            _chkIsBoss.Checked && _chkBossCombo.Checked
                ? (int)_nudBossComboPreview.Value - 1 : 0,
            (CombatTelegraphShape)Math.Max(0, _cmbCombatTelegraphShape.SelectedIndex),
            _cmbBossComboSecond.SelectedIndex, _cmbBossComboThird.SelectedIndex);
        var tiles = CombatTelegraphGeometry.GetTiles(
            shape, 4, 5, 4, 4, Direction.Up, (int)_nudCombatTelegraphRange.Value).ToHashSet();

        using var floor = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(55, 51, 49));
        using var danger = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(187, 63, 51));
        using var monster = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(212, 170, 76));
        using var target = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(83, 177, 218));
        using var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(32, 29, 28));

        for (var y = 0; y < grid; y++)
        for (var x = 0; x < grid; x++)
        {
            var brush = x == 4 && y == 5 ? monster :
                tiles.Contains(new CombatTelegraphTile(x, y)) ? danger : floor;
            var rect = new System.Drawing.Rectangle(offsetX + x * tileSize, offsetY + y * tileSize,
                tileSize - 1, tileSize - 1);
            e.Graphics.FillRectangle(brush, rect);
            e.Graphics.DrawRectangle(border, rect);
            // The blue target must remain visible even inside the dangerous red zone.
            if (x == 4 && y == 4)
                e.Graphics.FillRectangle(target, rect.X + 3, rect.Y + 3, 5, 5);
        }
    }
}
