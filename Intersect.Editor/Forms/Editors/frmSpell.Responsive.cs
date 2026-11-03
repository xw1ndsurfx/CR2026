using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DarkUI.Controls;
using Intersect.GameObjects;
using Intersect.Enums;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmSpell
{
    private static System.Drawing.Size _lastSpellEditorSize = System.Drawing.Size.Empty;
    private readonly TabControl _spellTabs = new();
    private readonly List<(Panel Panel, Control[] Groups)> _spellTabPages = [];
    private readonly DarkButton _newSpellAutoButton = new();
    private bool _spellTabsReady;
    private bool _waitingForNewSpell;
    private readonly HashSet<Guid> _knownSpellsBeforeCreation = [];

    private void BuildResponsiveSpellEditor()
    {
        SuspendLayout();
        try
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new System.Drawing.Size(720, 450);
            var bounds = Screen.FromControl(this).WorkingArea;
            var initial = _lastSpellEditorSize.IsEmpty
                ? new System.Drawing.Size(1080, 700) : _lastSpellEditorSize;
            Size = new System.Drawing.Size(
                Math.Min(bounds.Width, Math.Max(720, initial.Width)),
                Math.Min(bounds.Height, Math.Max(450, initial.Height)));

            pnlContainer.AutoScroll = false;
            pnlContainer.Controls.Clear();
            _spellTabs.Dock = DockStyle.Fill;
            _spellTabs.Multiline = true;
            pnlContainer.Controls.Add(_spellTabs);

            AddSpellTab("General", grpGeneral, grpRequirements, _spellRarityGroup);
            AddSpellTab("Cost & Targeting", grpSpellCost, grpTargetInfo);
            AddSpellTab("Combat & Effects", grpCombat);
            AddSpellTab("Special", grpEvent, grpWarp, grpDash);

            var creatorPage = new TabPage("Auto Creator")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };
            var heading = new Label
            {
                Text = "Create or rebalance spells using existing NPC health as a benchmark. " +
                       "Preview changes before applying them to a spell.",
                Location = new System.Drawing.Point(16, 20),
                Size = new System.Drawing.Size(490, 64),
                ForeColor = System.Drawing.Color.Gainsboro
            };
            var launch = new DarkButton
            {
                Text = "Open Spell Auto Creator",
                Location = new System.Drawing.Point(16, 98),
                Size = new System.Drawing.Size(230, 36)
            };
            launch.Click += (_, _) => ShowSpellAutoCreator();
            creatorPage.Controls.Add(heading);
            creatorPage.Controls.Add(launch);
            _spellTabs.TabPages.Add(creatorPage);

            _newSpellAutoButton.Text = "New Spell + Auto";
            _newSpellAutoButton.Size = new System.Drawing.Size(180, 28);
            _newSpellAutoButton.Click += (_, _) => BeginNewSpellWithAutoCreator();
            grpSpells.Controls.Add(_newSpellAutoButton);
            _newSpellAutoButton.BringToFront();

            _spellTabsReady = true;
            Resize += (_, _) => ArrangeSpellEditor();
            FormClosed += (_, _) =>
            {
                _lastSpellEditorSize = WindowState == FormWindowState.Normal
                    ? Size : RestoreBounds.Size;
            };
            ArrangeSpellEditor();
            ArrangeSpellSections();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void AddSpellTab(string label, params Control[] groups)
    {
        var tab = new TabPage(label)
        {
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
        };
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
        };

        foreach (var group in groups)
        {
            // Keep the existing designer controls and event handlers intact.
            group.Parent = panel;
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        }
        panel.Resize += (_, _) => ArrangeSpellGroupPanel(panel, groups);
        tab.Controls.Add(panel);
        _spellTabs.TabPages.Add(tab);
        _spellTabPages.Add((panel, groups));
    }

    private void ArrangeSpellGroupPanel(Panel panel, Control[] groups)
    {
        if (!_spellTabsReady || panel.IsDisposed || panel.ClientSize.Width < 50)
            return;

        panel.SuspendLayout();
        try
        {
            // TabPages report child.Visible=false while not selected, even when
            // the editor intends to show them. Arrange every group so switching
            // tabs never leaves controls stranded outside the scrollable area.
            var arranged = groups;
            var width = Math.Max(1, panel.ClientSize.Width - 22);
            var maxGroupWidth = Math.Max(1, groups.Max(group => group.Width));
            var count = width >= 2 * (maxGroupWidth + 12) ? 2 : 1;
            var heights = new int[count];
            for (var i = 0; i < arranged.Length; i++)
            {
                var column = Array.IndexOf(heights, heights.Min());
                var group = arranged[i];
                group.Location = new System.Drawing.Point(
                    8 + column * (maxGroupWidth + 12), 8 + heights[column]);
                heights[column] += group.Height + 12;
            }
            panel.AutoScrollMinSize = new System.Drawing.Size(
                Math.Max(0, count * (maxGroupWidth + 12) + 8),
                heights.Length == 0 ? 0 : heights.Max() + 8);
        }
        finally
        {
            panel.ResumeLayout(true);
        }
    }

    private void ArrangeSpellSections()
    {
        if (!_spellTabsReady) return;
        foreach (var (page, groups) in _spellTabPages)
            ArrangeSpellGroupPanel(page, groups);
    }

    internal void ArrangeSpellEditor()
    {
        if (!_spellTabsReady || ClientSize.Width < 200) return;
        var footerY = Math.Max(230, ClientSize.Height - 44);
        grpSpells.SetBounds(4, 30, 200, Math.Max(115, footerY - 37));
        lstGameObjects.SetBounds(5, 46, 190,
            Math.Max(50, grpSpells.ClientSize.Height - 90));
        _newSpellAutoButton.SetBounds(8,
            Math.Max(80, grpSpells.ClientSize.Height - 36), 180, 28);
        pnlContainer.SetBounds(212, 33,
            Math.Max(290, ClientSize.Width - 220),
            Math.Max(155, footerY - 38));
        btnSave.SetBounds(Math.Max(212, ClientSize.Width - 344),
            footerY, 158, 30);
        btnCancel.SetBounds(Math.Max(212, ClientSize.Width - 174),
            footerY, 158, 30);
        toolStrip.Width = ClientSize.Width;
        ArrangeSpellSections();
    }

    private void BeginNewSpellWithAutoCreator()
    {
        if (_waitingForNewSpell) return;
        _knownSpellsBeforeCreation.Clear();
        foreach (var spell in SpellDescriptor.Lookup.Values.OfType<SpellDescriptor>())
            _knownSpellsBeforeCreation.Add(spell.Id);

        _waitingForNewSpell = true;
        toolStripItemNew_Click(this, EventArgs.Empty);
    }

    private void OpenNewSpellAutoCreatorIfReady()
    {
        if (!_waitingForNewSpell) return;
        var created = SpellDescriptor.Lookup.Values.OfType<SpellDescriptor>()
            .Where(spell => !_knownSpellsBeforeCreation.Contains(spell.Id))
            .Select(spell => spell.Id).ToArray();
        if (created.Length != 1) return;

        _waitingForNewSpell = false;
        _knownSpellsBeforeCreation.Clear();
        if (lstGameObjects.SelectObject(created[0]) && IsHandleCreated && !IsDisposed)
            BeginInvoke(new Action(ShowSpellAutoCreator));
    }
}
