namespace Intersect.Client.Interface.Game;

internal sealed partial class LogiklikNewsWindow
{
    internal sealed record NewsPreview(string Title, string ImageUrl, byte[]? Image);
    private static readonly SemaphoreSlim PreviewGate = new(1, 1);
    private static NewsPreview? _preview;
    private static DateTime _previewExpires;

    internal static async Task<NewsPreview?> LoadLatestPreviewAsync()
    {
        await PreviewGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_preview != null && DateTime.UtcNow < _previewExpires) return _preview;
            var items = ParseFeed(await s_httpClient.GetStringAsync(FeedUrl).ConfigureAwait(false));
            if (items.FirstOrDefault() is not { } latest) return null;
            byte[]? image = null;
            var imageUrl = string.Empty;
            foreach (var url in latest.ImageUrls)
            {
                try
                {
                    image = await s_httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                    if (image.Length == 0) continue;
                    imageUrl = url;
                    break;
                }
                catch { /* Keep the article available even if an image host is unavailable. */ }
            }
            _preview = new NewsPreview(latest.Title, imageUrl, image);
            _previewExpires = DateTime.UtcNow.AddMinutes(5);
            return _preview;
        }
        finally { PreviewGate.Release(); }
    }
}
