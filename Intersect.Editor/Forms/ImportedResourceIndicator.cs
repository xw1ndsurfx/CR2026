using System.Globalization;
using Intersect.Editor.Content;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms;

internal sealed class ImportedResourceIndicator : Label
{
    private CancellationTokenSource? _pending;
    internal ImportedResourceIndicator()
    {
        Dock = DockStyle.Top;
        Height = 46;
        Padding = new Padding(8, 0, 8, 0);
        ForeColor = System.Drawing.Color.LightGreen;
        AutoEllipsis = true;
        TextAlign = ContentAlignment.MiddleLeft;
    }

    private static string T(string key) => QualityOfLifeText.Get(key, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    internal async void RefreshStatus(string? source, string folder, string cache)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        if (source == null) { Text = string.Empty; return; }
        Text = T("Checking import status...");
        try
        {
            var found = await Task.Run(() => ImportedResourceStatus.Find(source, folder, cache, token), token);
            if (!token.IsCancellationRequested && !IsDisposed)
                Text = found == null ? T("No matching game copy detected.") : string.Format(T("Already imported: {0}"), found);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested && !IsDisposed) Text = T("Import status unavailable.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _pending?.Cancel(); _pending?.Dispose(); }
        base.Dispose(disposing);
    }
}
