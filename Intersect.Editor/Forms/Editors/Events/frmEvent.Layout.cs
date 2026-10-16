using Intersect.Framework.Core.GameObjects.Events;
using DarkUI.Controls;

namespace Intersect.Editor.Forms.Editors.Events;

public partial class FrmEvent
{
    private readonly NumericUpDown _worldMapX = new() { Minimum = -2048, Maximum = 2048, Width = 100 };
    private readonly NumericUpDown _worldMapY = new() { Minimum = -2048, Maximum = 2048, Width = 100 };
    private readonly DarkCheckBox _worldMapName = new() { Text = "Show Name in World Map", AutoSize = true };
    private readonly NumericUpDown _animationX = new() { Minimum = -2048, Maximum = 2048, Width = 100 };
    private readonly NumericUpDown _animationY = new() { Minimum = -2048, Maximum = 2048, Width = 100 };
    private readonly DarkComboBox _animationLayer = new() { Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _loadingOffsets;
    private FormBorderStyle _normalBorder;
    private FormWindowState _normalWindowState;
    private Rectangle _normalBounds;
    private bool _fullscreen;
    private readonly List<TreeNode> _commandRoots = [];

    private void InitializeModernLayout()
    {
        SuspendLayout();
        MaximumSize = Size.Empty;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(1000, 620);
        Size = new Size(1180, 820);
        pnlEditorComponents.MaximumSize = Size.Empty;

        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 56, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(grpGeneral, 0, 0); header.Controls.Add(grpPageOptions, 1, 0);
        grpGeneral.Dock = grpPageOptions.Dock = DockStyle.Fill;

        var tabs = new Panel { Dock = DockStyle.Top, Height = 28 };
        tabs.Controls.Add(pnlTabsContainer); tabs.Controls.Add(btnTabsLeft); tabs.Controls.Add(btnTabsRight);
        pnlTabsContainer.Dock = DockStyle.Fill;
        btnTabsLeft.Dock = DockStyle.Left; btnTabsRight.Dock = DockStyle.Right;
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(8) };
        footer.Controls.AddRange([btnSave, btnCancel]);
        var fullscreen = new DarkButton { Text = "Full screen (F11)", AutoSize = true };
        fullscreen.Click += (_, _) => ToggleEventFullscreen();
        footer.Controls.Add(fullscreen);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 8, FixedPanel = FixedPanel.Panel1 };
        split.Size = new Size(1120, 600); split.SplitterDistance = 410;
        split.Panel1MinSize = 410; split.Panel2MinSize = 515;
        var properties = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
        };
        split.Panel1.Controls.Add(properties);
        foreach (var group in new Control[] { grpEventConditions, grpEntityOptions, grpTriggers, _questArrowGroup })
        {
            group.Margin = new Padding(4); group.Width = 380; properties.Controls.Add(group);
        }
        var animationLayerGroup = new DarkGroupBox { Text = "Animation layer", Width = 380, Height = 80 };
        _animationLayer.SetBounds(12, 30, 340, 24);
        _animationLayer.Items.AddRange(["Over Character (above all decor)", "Same as Character", "Behind Character"]);
        _animationLayer.SelectedIndexChanged += (_, _) =>
        {
            if (!_loadingOffsets && CurrentPage != null && _animationLayer.SelectedIndex >= 0)
                CurrentPage.AnimationRenderLayer = (EventAnimationLayer)_animationLayer.SelectedIndex;
        };
        animationLayerGroup.Controls.Add(_animationLayer); properties.Controls.Add(animationLayerGroup);
        var animationOffsets = new DarkGroupBox { Text = "In-game animation offset (pixels)", Width = 380, Height = 85 };
        var animationRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 20, 0, 0) };
        animationRow.Controls.AddRange([new DarkLabel { Text = "X", AutoSize = true }, _animationX,
            new DarkLabel { Text = "Y", AutoSize = true }, _animationY]);
        animationOffsets.Controls.Add(animationRow); properties.Controls.Add(animationOffsets);
        _animationX.ValueChanged += (_, _) => { if (!_loadingOffsets && CurrentPage != null) CurrentPage.AnimationOffsetX = (int)_animationX.Value; };
        _animationY.ValueChanged += (_, _) => { if (!_loadingOffsets && CurrentPage != null) CurrentPage.AnimationOffsetY = (int)_animationY.Value; };
        var offsets = new DarkGroupBox { Text = "World Map marker offset (map pixels)", Width = 380, Height = 100 };
        var offsetRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 20, 0, 0) };
        offsetRow.Controls.AddRange([new DarkLabel { Text = "X", AutoSize = true }, _worldMapX,
            new DarkLabel { Text = "Y", AutoSize = true }, _worldMapY]);
        offsetRow.WrapContents = true;
        offsetRow.Controls.Add(_worldMapName);
        _worldMapName.CheckedChanged += (_, _) => { if (!_loadingOffsets && CurrentPage != null) CurrentPage.ShowNameOnWorldMap = _worldMapName.Checked; };
        offsets.Controls.Add(offsetRow); properties.Controls.Add(offsets);
        _worldMapX.ValueChanged += (_, _) => SaveWorldMapOffsets();
        _worldMapY.ValueChanged += (_, _) => SaveWorldMapOffsets();

        foreach (var commands in new Control[] { grpNewCommands, grpEventCommands, grpCreateCommands })
        {
            split.Panel2.Controls.Add(commands); commands.Dock = DockStyle.Fill;
        }
        lstEventCommands.Dock = DockStyle.Fill;
        lstCommands.Dock = DockStyle.Fill;
        lblCloseCommands.Dock = DockStyle.Bottom;
        _commandRoots.AddRange(lstCommands.Nodes.Cast<TreeNode>().Select(node => (TreeNode)node.Clone()));
        var search = new DarkTextBox { Dock = DockStyle.Top, PlaceholderText = "Search commands..." };
        search.TextChanged += (_, _) =>
        {
            lstCommands.BeginUpdate(); lstCommands.Nodes.Clear();
            foreach (var root in _commandRoots)
            {
                var filtered = FilterCommands(root, search.Text.Trim());
                if (filtered != null) lstCommands.Nodes.Add(filtered);
            }
            lstCommands.ExpandAll(); lstCommands.EndUpdate();
        };
        grpNewCommands.Controls.Add(search); search.BringToFront();
        Controls.Remove(pnlEditorComponents);
        Controls.Add(split); Controls.Add(tabs); Controls.Add(header); Controls.Add(footer);
        split.BringToFront();
        KeyDown += (_, args) =>
        {
            if (args.KeyCode != Keys.F11) return;
            ToggleEventFullscreen(); args.Handled = args.SuppressKeyPress = true;
        };
        ResumeLayout(true);
    }

    private static TreeNode? FilterCommands(TreeNode source, string query)
    {
        if (query.Length == 0 || source.Text.Contains(query, StringComparison.OrdinalIgnoreCase)) return (TreeNode)source.Clone();
        var result = (TreeNode)source.Clone(); result.Nodes.Clear();
        foreach (TreeNode child in source.Nodes)
            if (FilterCommands(child, query) is { } match) result.Nodes.Add(match);
        return result.Nodes.Count == 0 ? null : result;
    }

    private void ToggleEventFullscreen()
    {
        if (_fullscreen)
        {
            WindowState = FormWindowState.Normal; FormBorderStyle = _normalBorder;
            Bounds = _normalBounds; WindowState = _normalWindowState;
        }
        else
        {
            _normalBorder = FormBorderStyle; _normalWindowState = WindowState;
            _normalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState = FormWindowState.Normal; FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized;
        }
        _fullscreen = !_fullscreen;
    }

    private void LoadWorldMapOffsets()
    {
        _loadingOffsets = true;
        _animationLayer.SelectedIndex = Enum.IsDefined(CurrentPage.AnimationRenderLayer) ? (int)CurrentPage.AnimationRenderLayer : 0;
        _animationX.Value = Math.Clamp(CurrentPage.AnimationOffsetX, -2048, 2048);
        _animationY.Value = Math.Clamp(CurrentPage.AnimationOffsetY, -2048, 2048);
        _worldMapName.Checked = CurrentPage.ShowNameOnWorldMap;
        _worldMapX.Value = Math.Clamp(CurrentPage.WorldMapOffsetX, -2048, 2048);
        _worldMapY.Value = Math.Clamp(CurrentPage.WorldMapOffsetY, -2048, 2048);
        _loadingOffsets = false;
    }

    private void SaveWorldMapOffsets()
    {
        if (_loadingOffsets || CurrentPage == null) return;
        CurrentPage.WorldMapOffsetX = (int)_worldMapX.Value;
        CurrentPage.WorldMapOffsetY = (int)_worldMapY.Value;
    }
}
