using System.Security.Cryptography;
using System.Text;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;

namespace Intersect.Client.Interface.Game;

internal sealed partial class LogiCoinShopWindow
{
    private ImagePanel _newsImage = null!;
    private Label _newsCaption = null!;
    private Button _newsCard = null!;
    private bool _newsLoading;
    private int _newsVersion;
    private long _nextNewsAttempt;

    private void AddNewsBanner()
    {
        _newsCard = new Button(this, "LatestLogiklikNews") { Text = Intersect.Client.Localization.ClientLanguages.Text("Logiklik News"), Font = Skin.DefaultFont, FontSize = 11 };
        _newsCard.SetBounds(541, 187, 201, 101);
        _newsCard.SetToolTipText(Intersect.Client.Localization.ClientLanguages.Text("Read the latest Corps Royaux news"));
        _newsCard.Clicked += (_, _) => Interface.InGameUiOrNull?.OpenLogiklikNews();
        _newsImage = new ImagePanel(_newsCard, "LatestNewsImage") { MaintainAspectRatio = false, MouseInputEnabled = false, IsHidden = true };
        _newsImage.SetBounds(2, 2, 197, 97);
        _newsCaption = AddText(_newsCard, "LatestNewsTitle", "", 3, 81, 195, 18, 8, Cream, center: true);
        _newsCaption.MouseInputEnabled = false;
        _newsCaption.IsHidden = true;
    }

    private async Task RefreshNewsBannerAsync()
    {
        if (_newsLoading || Environment.TickCount64 < _nextNewsAttempt) return;
        _newsLoading = true;
        _nextNewsAttempt = Environment.TickCount64 + 30_000;
        var version = _newsVersion;
        try
        {
            var news = await LogiklikNewsWindow.LoadLatestPreviewAsync().ConfigureAwait(false);
            if (news != null) _nextNewsAttempt = Environment.TickCount64 + 300_000;
            RunOnMainThread(() =>
            {
                if (version != _newsVersion || news == null) return;
                _newsCaption.Text = news.Title;
                _newsCard.SetToolTipText(news.Title + "\nOpen Logiklik News");
                _newsImage.Texture = null; _newsImage.IsHidden = true; _newsCard.Text = Intersect.Client.Localization.ClientLanguages.Text("Logiklik News");
                if (news.Image is not { Length: > 0 } image) return;
                try
                {
                    var key = "logiklik-news-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(news.ImageUrl))).ToLowerInvariant();
                    _newsImage.Texture = GameContentManager.Current.Load<IGameTexture>(ContentType.Interface, key,
                        () => new MemoryStream(image, writable: false));
                    _newsImage.IsHidden = false;
                    _newsCard.Text = string.Empty;
                }
                catch { _newsCard.Text = Intersect.Client.Localization.ClientLanguages.Text("Logiklik News"); }
            });
        }
        catch { /* The news shortcut remains usable when the feed is offline. */ }
        finally { _newsLoading = false; }
    }

    protected override void Dispose(bool disposing)
    {
        ++_newsVersion;
        base.Dispose(disposing);
    }
}
