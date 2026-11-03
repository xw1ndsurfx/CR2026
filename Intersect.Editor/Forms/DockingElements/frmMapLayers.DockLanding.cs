using System.Globalization;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

namespace Intersect.Editor.Forms.DockingElements;

public partial class FrmMapLayers
{
    private GroupBox? _dockLandingGroup;
    private readonly CheckBox _dockCustomLanding = new() { AutoSize = true };
    private readonly NumericUpDown _dockLandingX = new() { Minimum = 0, Maximum = 4095, Width = 75 };
    private readonly NumericUpDown _dockLandingY = new() { Minimum = 0, Maximum = 4095, Width = 75 };
    private readonly Label _dockLandingStatus = new() { AutoSize = true, MaximumSize = new(220, 0) };
    private static string DockText(string source) => NavalSetupText.Get(source, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private void InitializeDockLandingEditor()
    {
        _dockLandingGroup = new GroupBox { Name = "DockLandingSettings", Text = DockText("Disembarkation point"),
            Location = new(8, 42), Size = new(250, 300), Visible = false };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(8, 8, 4, 8), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        _dockCustomLanding.Text = DockText("Choose the arrival tile");
        _dockCustomLanding.Name = "DockCustomLanding"; _dockLandingX.Name = "DockLandingX"; _dockLandingY.Name = "DockLandingY";
        _dockCustomLanding.CheckedChanged += (_, _) =>
        { _dockLandingX.Enabled = _dockLandingY.Enabled = _dockCustomLanding.Checked; };
        _dockLandingX.Enabled = _dockLandingY.Enabled = false;
        flow.Controls.Add(_dockCustomLanding);
        var row = new FlowLayoutPanel { Width = 220, Height = 32, WrapContents = false };
        row.Controls.AddRange([new Label { Text = "X", AutoSize = true }, _dockLandingX, new Label { Text = "Y", AutoSize = true }, _dockLandingY]);
        flow.Controls.Add(row);
        var pick = new Button { Name = "ChooseDockLanding", Text = DockText("Choose on the map"), Width = 220, Height = 30 };
        pick.Click += (_, _) =>
        {
            if (Globals.CurrentMap == null || Globals.MapEditorWindow == null) return;
            Globals.MapEditorWindow.BeginDockLandingSelection((x, y) =>
            {
                if (IsDisposed) return false;
                if (Globals.CurrentMap.Attributes[x, y] is MapDockAttribute or MapBlockedAttribute or MapWarpAttribute or MapResourceAttribute
                    or MapSlideAttribute or MapGrappleStoneAttribute or MapZDimensionAttribute or MapAnimationAttribute { IsBlock: true })
                { _dockLandingStatus.Text = DockText("This tile is blocked. Choose a walkable pier tile without Q or Block."); return false; }
                _dockLandingX.Value = x; _dockLandingY.Value = y; _dockCustomLanding.Checked = true;
                _dockLandingStatus.Text = DockText("Arrival selected. Paint or repaint the Q dock to save this point.");
                return true;
            }, () => { if (!IsDisposed) _dockLandingStatus.Text = DockText("Selection cancelled."); });
            _dockLandingStatus.Text = DockText("Click the arrival tile on this map. Right-click or Escape cancels.");
        };
        flow.Controls.Add(pick);
        flow.Controls.Add(new Label { AutoSize = true, MaximumSize = new(220, 0), Text = DockText("Choose a walkable tile on the pier, then paint or repaint Q. Q remains a solid interaction marker. A blocked arrival refuses disembarkation. Uncheck to use automatic placement.") });
        flow.Controls.Add(_dockLandingStatus);
        _dockLandingGroup.Controls.Add(flow); pnlAttributes.Controls.Add(_dockLandingGroup);
        EditorTheme.Apply(_dockLandingGroup);
    }

    private void ShowDockLandingEditor()
    {
        _dockLandingX.Maximum = Math.Max(0, Options.Instance.Map.MapWidth - 1);
        _dockLandingY.Maximum = Math.Max(0, Options.Instance.Map.MapHeight - 1);
        _dockLandingGroup!.Visible = true; _dockLandingGroup.BringToFront();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && Globals.MapEditorWindow?.ChoosingDockLanding == true)
        { Globals.MapEditorWindow.CancelDockLandingSelection(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
