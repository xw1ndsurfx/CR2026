using Intersect.Editor.Forms.Styling;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmShop
{
    private void ModernizeShopWorkspace()
    {
        EditorWorkspaceLayout.Apply(this, grpShops, pnlContainer, btnSave, btnCancel);
        pnlContainer.AutoScroll = false;
        var bar = pnlContainer.Controls.OfType<FlowLayoutPanel>().Single();
        bar.Dock = DockStyle.Fill;
        // Keep the buying mode on its own row so translated radio captions do not collide with Add Item.
        var modeTop = lstBoughtItems.Bottom + 4;
        foreach (Control child in grpItemsBought.Controls)
            if (child != lstBoughtItems && child.Top >= lstBoughtItems.Bottom) child.Top += 30;
        grpItemsBought.Height += 30;
        var modes = new FlowLayoutPanel
        {
            Location = new System.Drawing.Point(8, modeTop), Size = new Size(grpItemsBought.Width - 16, 28),
            WrapContents = false,
        };
        rdoBuyWhitelist.AutoSize = rdoBuyBlacklist.AutoSize = false;
        rdoBuyWhitelist.Size = rdoBuyBlacklist.Size = new Size(112, 24);
        modes.Controls.AddRange([rdoBuyWhitelist, rdoBuyBlacklist]); grpItemsBought.Controls.Add(modes);
        var layout = new TableLayoutPanel
        {
            Name = "ShopEditorSections", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(bar, 0, 0); layout.SetColumnSpan(bar, 2);
        layout.Controls.Add(grpGeneral, 0, 1); layout.SetColumnSpan(grpGeneral, 2); grpGeneral.Dock = DockStyle.Fill;
        void Offers(GroupBox group, ListBox list, int column)
        {
            var originalBottom = list.Bottom;
            foreach (Control child in group.Controls)
            {
                if (child == list) continue;
                if (child.Top >= originalBottom)
                    child.Anchor = AnchorStyles.Bottom | AnchorStyles.Left |
                        (child is ComboBox ? AnchorStyles.Right : AnchorStyles.None);
                else if (child.Left > list.Right) child.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(4) };
            group.MinimumSize = new Size(258, group.Height);
            // DockStyle.Fill prevents AutoScroll from measuring overflow. Keep a real
            // content rectangle so the final fields remain reachable on short screens.
            group.Dock = DockStyle.None;
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            var minimum = group.MinimumSize;
            void FitOffers()
            {
                group.Size = new Size(Math.Max(minimum.Width, scroll.ClientSize.Width),
                    Math.Max(minimum.Height, scroll.ClientSize.Height));
            }
            scroll.Controls.Add(group);
            scroll.ClientSizeChanged += (_, _) => FitOffers();
            FitOffers();
            layout.Controls.Add(scroll, column, 2);
        }
        Offers(grpItemsSold, lstSoldItems, 0); Offers(grpItemsBought, lstBoughtItems, 1);
        pnlContainer.Controls.Add(layout);
    }
}
