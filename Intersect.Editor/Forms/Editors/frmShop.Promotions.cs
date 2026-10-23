using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmShop
{
    private void InitializeOfferPromotions()
    {
        var edit = new LogiklikButton { Name = "EditOfferPromotion", Text = FrmShopPromotion.T("Special offer..."),
            Bounds = new Rectangle(8, 20, grpItemsSold.ClientSize.Width - 16, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Enabled = false };
        lstSoldItems.Top += 34; lstSoldItems.Height -= 34; btnItemUp.Top += 34;
        grpItemsSold.Controls.Add(edit);
        lstSoldItems.SelectedIndexChanged += (_, _) => edit.Enabled = lstSoldItems.SelectedIndex >= 0;
        edit.Click += (_, _) =>
        {
            var index = lstSoldItems.SelectedIndex;
            if (mEditorItem == null || index < 0 || index >= mEditorItem.SellingItems.Count) return;
            var offer = mEditorItem.SellingItems[index];
            using var dialog = new FrmShopPromotion(ItemDescriptor.GetName(offer.ItemId), offer.CostItemQuantity, offer.Promotion);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            offer.Promotion = dialog.Result;
            UpdateLists(); lstSoldItems.SelectedIndex = index;
        };
    }
}
