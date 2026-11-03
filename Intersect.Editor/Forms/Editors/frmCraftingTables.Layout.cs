namespace Intersect.Editor.Forms.Editors;

public partial class FrmCraftingTables
{
    private void InitializeResponsiveLayout()
    {
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScroll = false;
        MinimumSize = new Size(700, 560);
        var stretch = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lstGameObjects.Anchor = stretch;
        txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        btnClearSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        txtName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        cmbFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        btnAddFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _campfireCreator.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _campfireHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lstCrafts.Anchor = stretch;
        btnCraftUp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCraftDown.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        foreach (var control in new Control[] { cmbCrafts, btnAddCraftedItem, btnRemoveCraftedItem })
            control.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblAddCraftedItem.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        var arranging = false;
        void Arrange()
        {
            if (arranging || ClientSize.Width < 400 || ClientSize.Height < 300) return;
            arranging = true;
            try
            {
                AutoSize = false;
                AutoScroll = false;
                const int margin = 12, gap = 8;
                var top = toolStrip.Bottom + 10;
                var footerHeight = Math.Max(btnSave.Height, btnCancel.Height) + 2 * margin;
                var bodyHeight = Math.Max(180, ClientSize.Height - top - footerHeight);
                grpTables.SetBounds(margin, top, Math.Clamp(ClientSize.Width / 4, 210, 300), bodyHeight);
                pnlContainer.SetBounds(grpTables.Right + gap, top, ClientSize.Width - grpTables.Right - gap - margin, bodyHeight);
                var scroll = pnlContainer.AutoScrollPosition;
                // Reserve the scrollbar width even when hidden, avoiding horizontal
                // scrollbars or cropped controls at the minimum window size.
                var width = Math.Max(250, pnlContainer.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
                grpGeneral.SetBounds(scroll.X, scroll.Y, width, grpGeneral.Height);
                grpCrafts.SetBounds(scroll.X, scroll.Y + grpGeneral.Height + gap, width, Math.Max(335, pnlContainer.ClientSize.Height - grpGeneral.Height - 2 * gap));
                btnCancel.SetBounds(ClientSize.Width - margin - 170, ClientSize.Height - margin - btnCancel.Height, 170, btnCancel.Height);
                btnSave.SetBounds(btnCancel.Left - gap - 170, btnCancel.Top, 170, btnCancel.Height);
            }
            finally { arranging = false; }
        }
        Resize += (_, _) => Arrange();
        ClientSizeChanged += (_, _) => Arrange();
        Layout += (_, _) => Arrange();
        Shown += (_, _) => Arrange();
        ClientSize = new Size(900, 620);
        Arrange();
    }
}
