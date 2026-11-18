namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Lends the real NPC editor pages to the generator, retaining their resource
/// pickers, validation and handlers. Future registered NPC pages follow automatically.
/// The caller owns the data transaction; this class only restores control ownership.
/// </summary>
internal sealed class NpcAutoCreatorWorkspace : IDisposable
{
    private readonly System.Windows.Forms.TabControl _source;
    private readonly (TabPage Page, int Index)[] _pages;
    private readonly TabPage? _selected;
    internal TabControl Tabs { get; }

    internal NpcAutoCreatorWorkspace(Form dialog, System.Windows.Forms.TabControl source, IEnumerable<TabPage> pages,
        Button cancel, string generatorCaption, string doneCaption)
    {
        _source = source;
        _selected = source.SelectedTab;
        _pages = pages.Distinct().Select(page => (page, source.TabPages.IndexOf(page))).ToArray();
        var generator = new TabPage(generatorCaption) { Name = "NpcAutoGenerator", BackColor = dialog.BackColor };
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true,
            AutoScrollMinSize = dialog.AutoScrollMinSize, BackColor = dialog.BackColor };
        // Materialize first: changing Parent mutates the original Controls collection.
        foreach (var control in dialog.Controls.Cast<Control>().Where(c => c != cancel).ToArray())
            control.Parent = scroll;
        generator.Controls.Add(scroll);
        Tabs = new TabControl { Name = "NpcAutoCreatorWorkspace", Dock = DockStyle.Fill, Multiline = true };
        Tabs.TabPages.Add(generator);
        foreach (var (page, _) in _pages) Tabs.TabPages.Add(page);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
        var done = new Button { Name = "NpcAutoKeepChanges", Text = doneCaption,
            AutoSize = true, Height = 30, DialogResult = DialogResult.OK };
        cancel.AutoSize = true; cancel.Anchor = AnchorStyles.None;
        footer.Controls.Add(done); footer.Controls.Add(cancel);
        dialog.AutoScrollMinSize = Size.Empty; dialog.AutoScroll = false;
        dialog.Controls.Add(Tabs); dialog.Controls.Add(footer);
        dialog.AcceptButton = done; dialog.CancelButton = cancel;
        dialog.MinimumSize = new Size(800, 480); dialog.Size = new Size(1050, 720);
    }

    internal void ReviewGeneratedProfile()
    {
        if (Tabs.TabPages.Count > 1) Tabs.SelectedIndex = 1;
    }

    public void Dispose()
    {
        foreach (var (page, index) in _pages.OrderBy(entry => entry.Index))
            _source.TabPages.Insert(Math.Clamp(index, 0, _source.TabPages.Count), page);
        if (_selected != null) _source.SelectedTab = _selected;
    }
}

/// <summary>Cancel returns to the unsaved draft at entry, without touching its disk backup.</summary>
internal sealed class NpcAutoCreatorDraft(Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor npc) : IDisposable
{
    private readonly string _original = npc.JsonData;
    private bool _kept;
    private bool _disposed;
    internal void Keep() => _kept = true;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_kept) npc.Load(_original, true);
    }
}
