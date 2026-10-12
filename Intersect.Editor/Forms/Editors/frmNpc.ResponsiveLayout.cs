using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DarkUI.Controls;

namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Responsive NPC editor: preserve existing controls and their event handlers,
/// but put related groups on individually scrolling pages. No game data changes.
/// </summary>
public partial class FrmNpc
{
    private static Size _lastNpcEditorSize = Size.Empty;
    private readonly TabControl _npcTabs = new();
    private readonly List<(Panel Page, Control[] Groups)> _npcTabPanels = [];

    private void BuildResponsiveNpcEditor()
    {
        SuspendLayout();
        try
        {
            AutoSize = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new Size(795, 480);
            var screen = Screen.FromControl(this).WorkingArea;
            var desired = _lastNpcEditorSize.IsEmpty
                ? new Size(1150, 735) : _lastNpcEditorSize;
            Size = new Size(
                Math.Min(screen.Width, Math.Max(MinimumSize.Width, desired.Width)),
                Math.Min(screen.Height, Math.Max(MinimumSize.Height, desired.Height)));

            pnlContainer.AutoScroll = false;
            pnlContainer.Controls.Clear();
            pnlContainer.Padding = new Padding(0);
            _npcTabs.Dock = DockStyle.Fill;
            _npcTabs.Multiline = true;
            _npcTabs.SizeMode = TabSizeMode.Normal;
            _npcTabs.HotTrack = true;
            pnlContainer.Controls.Add(_npcTabs);

            AddNpcEditorTab("General", grpGeneral, grpCommonEvents, _grpBoss);
            AddNpcEditorTab("Stats & Resist", grpStats, grpRegen, grpImmunities);
            AddNpcEditorTab("Combat", grpCombat, _grpCombatAi, grpSpells);
            AddNpcEditorTab("Behavior", grpBehavior, grpNpcVsNpc);
            AddNpcEditorTab("Drops", grpDrops);
            AddNpcEditorTab("Pets", _grpPet);

            var autoTab = new TabPage("Auto Creator")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                ForeColor = System.Drawing.Color.Gainsboro,
            };
            var autoMessage = new Label
            {
                Text = "Generate and balance an NPC by level, class and difficulty. " +
                       "You can inspect the combat simulation before applying any changes.",
                ForeColor = System.Drawing.Color.Gainsboro,
                Location = new System.Drawing.Point(16, 24),
                Size = new Size(420, 65),
            };
            var openCreator = new DarkButton
            {
                Text = "Open NPC Auto Creator",
                Location = new System.Drawing.Point(16, 101),
                Size = new Size(220, 34),
            };
            openCreator.Click += (_, _) => ShowNpcAutoCreator();
            autoTab.Controls.Add(autoMessage);
            autoTab.Controls.Add(openCreator);
            _npcTabs.TabPages.Add(autoTab);

            Resize += (_, _) => ResizeResponsiveNpcEditor();
            FormClosed += (_, _) =>
            {
                if (WindowState == FormWindowState.Normal)
                {
                    _lastNpcEditorSize = Size;
                }
                else if (!RestoreBounds.Size.IsEmpty)
                {
                    _lastNpcEditorSize = RestoreBounds.Size;
                }
            };
            ResizeResponsiveNpcEditor();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void AddNpcEditorTab(string name, params Control[] groups)
    {
        var tab = new TabPage(name)
        {
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
        };
        var page = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
        };
        foreach (var group in groups)
        {
            // Reparenting retains each numeric field's original data bindings,
            // handlers, labels and item lists.
            group.Parent = page;
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        }

        tab.Controls.Add(page);
        _npcTabs.TabPages.Add(tab);
        _npcTabPanels.Add((page, groups));
        page.Resize += (_, _) => LayoutNpcGroups(page, groups);
    }

    private void LayoutNpcGroups(Panel page, Control[] groups)
    {
        if (page.IsDisposed || page.ClientSize.Width < 40)
        {
            return;
        }

        page.SuspendLayout();
        try
        {
            // At <= 695 px the content stacks vertically, allowing every
            // group to remain accessible on small laptops.
            var widest = Math.Max(290, groups.Max(group => group.Width));
            var columns = Math.Max(1, Math.Min(3,
                (page.ClientSize.Width - 20) / (widest + 12)));
            var heights = new int[columns];
            for (var i = 0; i < groups.Length; i++)
            {
                var column = Array.IndexOf(heights, heights.Min());
                var group = groups[i];
                group.Location = new System.Drawing.Point(
                    10 + column * (widest + 12), 10 + heights[column]);
                heights[column] += group.Height + 12;
            }

            page.AutoScrollMinSize = new Size(
                Math.Max(0, columns * (widest + 12) + 10),
                heights.Max() + 12);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private void ResizeResponsiveNpcEditor()
    {
        if (_npcTabs.IsDisposed || ClientSize.Width < 400)
        {
            return;
        }

        var footerY = Math.Max(230, ClientSize.Height - 44);
        grpNpcs.SetBounds(4, 32, 232, Math.Max(120, footerY - 41));
        // GameObjectList is a native TreeView and needs to grow with its group.
        lstGameObjects.Height = Math.Max(65, grpNpcs.ClientSize.Height - lstGameObjects.Top - 8);
        pnlContainer.SetBounds(245, 35,
            Math.Max(300, ClientSize.Width - 253),
            Math.Max(160, footerY - 39));
        btnCancel.SetBounds(Math.Max(260, ClientSize.Width - 173),
            footerY, 160, 31);
        btnSave.SetBounds(Math.Max(260, ClientSize.Width - 347),
            footerY, 160, 31);
        // The Auto Creator button added to Controls is positioned beside Save.
        foreach (Control control in Controls)
        {
            if (control is DarkButton && control.Text == "Auto Create NPC")
            {
                control.SetBounds(Math.Max(245, ClientSize.Width - 545),
                    footerY, 186, 31);
            }
        }

        toolStrip.Width = ClientSize.Width;
        foreach (var (page, groups) in _npcTabPanels)
        {
            LayoutNpcGroups(page, groups);
        }
    }
}
