using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmItem
{
    private static System.Drawing.Size _savedItemEditorSize = System.Drawing.Size.Empty;
    private readonly TabControl _itemEditorTabs = new();
    private readonly List<(Panel Panel, Control[] Groups)> _itemTabGroups = [];
    private readonly DarkButton _btnNewItemAuto = new();
    private readonly DarkButton _btnItemAuto = new();
    private bool _itemResponsiveReady;
    private bool _waitingForAutoItem;
    private readonly HashSet<Guid> _beforeAutoItemIds = [];

    private void BuildResponsiveItemEditor()
    {
        InitializePresentationControls();
        SuspendLayout();
        try
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new System.Drawing.Size(750, 470);
            var screen = Screen.FromControl(this).WorkingArea;
            var desired = _savedItemEditorSize.IsEmpty
                ? new System.Drawing.Size(1200, 745) : _savedItemEditorSize;
            Size = new System.Drawing.Size(
                Math.Min(screen.Width, Math.Max(MinimumSize.Width, desired.Width)),
                Math.Min(screen.Height, Math.Max(MinimumSize.Height, desired.Height)));

            pnlContainer.AutoScroll = false;
            pnlContainer.Controls.Clear();
            _itemEditorTabs.Dock = DockStyle.Fill;
            _itemEditorTabs.Multiline = true;
            pnlContainer.Controls.Add(_itemEditorTabs);

            AddItemEditorTab("General / Trading", grpGeneral);
            AddItemEditorTab(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Trading and display",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),_itemPresentationGroup);
            // Equipment controls are moved, not recreated: their existing event
            // handlers and item bindings are all preserved.
            grpEquipment.Height = 90; // Slot selector only, details live in other tabs.
            grpEquipment.Width = 610;
            AddItemEditorTab("Equipment Slot", grpEquipment);
            AddItemEditorTab("Vital & Stat Bonuses", grpVitalBonuses,
                grpRegen, grpStatBonuses, grpStatRanges, grpEffects);
            AddItemEditorTab("Paperdolls", grpPaperdoll);
            AddItemEditorTab("Weapon & Shield", grpWeaponProperties, grpShieldProperties);

            var creatorTab = new TabPage("Auto Creator")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };
            creatorTab.Controls.Add(new Label
            {
                Text = "Create or rebalance equipment and general items. Choose a level, " +
                       "rarity and item category, then review the proposed statistics, price " +
                       "and level requirement before applying.",
                ForeColor = System.Drawing.Color.Gainsboro,
                Location = new System.Drawing.Point(18, 20),
                Size = new System.Drawing.Size(490, 72)
            });
            var openCreator = new DarkButton
            {
                Text = "Open Item Auto Creator",
                Location = new System.Drawing.Point(18, 107),
                Size = new System.Drawing.Size(230, 34)
            };
            openCreator.Click += (_, _) => ShowItemAutoCreator();
            creatorTab.Controls.Add(openCreator);
            _itemEditorTabs.TabPages.Add(creatorTab);

            _btnNewItemAuto.Text = "New Item + Auto";
            _btnNewItemAuto.Click += (_, _) => BeginNewItemAutoCreator();
            grpItems.Controls.Add(_btnNewItemAuto);
            _btnNewItemAuto.BringToFront();

            _btnItemAuto.Text = "Auto Create Item";
            _btnItemAuto.Click += (_, _) => ShowItemAutoCreator();
            Controls.Add(_btnItemAuto);
            _btnItemAuto.BringToFront();

            _itemResponsiveReady = true;
            Resize += (_, _) => ArrangeResponsiveItemEditor();
            FormClosed += (_, _) =>
            {
                _savedItemEditorSize = WindowState == FormWindowState.Normal
                    ? Size : RestoreBounds.Size;
            };
            ArrangeResponsiveItemEditor();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void AddItemEditorTab(string name, params Control[] groups)
    {
        var tab = new TabPage(name)
        {
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
        };
        var page = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
        };
        foreach (var group in groups)
        {
            group.Parent = page;
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        }
        tab.Controls.Add(page);
        _itemEditorTabs.TabPages.Add(tab);
        _itemTabGroups.Add((page, groups));
        page.Resize += (_, _) => ArrangeItemEditorGroups(page, groups);
    }

    private static void ArrangeItemEditorGroups(Panel page, Control[] groups)
    {
        if (page.IsDisposed || groups.Length == 0) return;
        page.SuspendLayout();
        try
        {
            var maxWidth = Math.Max(1, groups.Max(group => group.Width));
            var columns = groups.Length == 1 ? 1 :
                page.ClientSize.Width >= (maxWidth + 15) * 2 + 12 ? 2 : 1;
            var heights = new int[columns];
            foreach (var group in groups)
            {
                var column = Array.IndexOf(heights, heights.Min());
                group.Location = new System.Drawing.Point(
                    8 + column * (maxWidth + 15), 8 + heights[column]);
                heights[column] += group.Height + 12;
            }
            page.AutoScrollMinSize = new System.Drawing.Size(
                columns * (maxWidth + 15) + 12, heights.Max() + 12);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private void ArrangeResponsiveItemEditor()
    {
        if (!_itemResponsiveReady || ClientSize.Width < 200) return;

        var footer = Math.Max(240, ClientSize.Height - 44);
        grpItems.SetBounds(4, 32, 233, Math.Max(128, footer - 39));
        lstGameObjects.SetBounds(6, 53, 222,
            Math.Max(48, grpItems.ClientSize.Height - 103));
        _btnNewItemAuto.SetBounds(7,
            Math.Max(88, grpItems.ClientSize.Height - 39), 217, 29);
        pnlContainer.SetBounds(246, 37,
            Math.Max(285, ClientSize.Width - 254), Math.Max(165, footer - 40));

        btnCancel.SetBounds(Math.Max(255, ClientSize.Width - 173),
            footer, 158, 31);
        btnSave.SetBounds(Math.Max(255, ClientSize.Width - 343),
            footer, 158, 31);
        _btnItemAuto.SetBounds(Math.Max(250, ClientSize.Width - 535),
            footer, 180, 31);

        toolStrip.Width = ClientSize.Width;
        foreach (var (page, groups) in _itemTabGroups)
            ArrangeItemEditorGroups(page, groups);
    }

    private void BeginNewItemAutoCreator()
    {
        if (_waitingForAutoItem) return;
        _beforeAutoItemIds.Clear();
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>())
            _beforeAutoItemIds.Add(item.Id);
        _waitingForAutoItem = true;
        toolStripItemNew_Click(this, EventArgs.Empty);
    }

    private void OpenNewItemAutoCreatorIfReady()
    {
        if (!_waitingForAutoItem) return;
        var newIds = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
            .Where(item => !_beforeAutoItemIds.Contains(item.Id))
            .Select(item => item.Id).ToArray();
        // A parallel server-side item creation must not select the wrong record.
        if (newIds.Length != 1) return;
        _waitingForAutoItem = false;
        _beforeAutoItemIds.Clear();
        if (lstGameObjects.SelectObject(newIds[0]) && IsHandleCreated && !IsDisposed)
            BeginInvoke(new Action(ShowItemAutoCreator));
    }
}
