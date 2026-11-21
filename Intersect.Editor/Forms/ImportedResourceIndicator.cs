using System.Globalization;
using Intersect.Editor.Content;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms;

internal sealed class ImportedResourceIndicator : Label
{
    private CancellationTokenSource? _pending;
    private readonly object _statusLock = new();
    private (CancellationToken Token, string Text, bool Imported)? _resolvedStatus;
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

    internal async Task RefreshStatus(string? source, string folder, string cache)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        lock (_statusLock) _resolvedStatus = null;
        if (source == null) { Text = string.Empty; return; }
        Text = T("Checking import status...");
        try
        {
            var status = await Task.Run(() =>
            {
                var declared=ImportedResourceDeclarations.Read(source,cache,token);
                if(declared==true)return (T("Marked as imported."),true);
                var found=ImportedResourceStatus.Find(source,folder,cache,token);
                if(declared==false)return (found==null?T("Not marked as imported."):string.Format(T("Not marked; matching game copy: {0}"),found),false);
                return (found==null?T("No matching game copy detected."):string.Format(T("Already imported: {0}"),found),found!=null);
            },token).ConfigureAwait(false);
            PublishStatus(token,status.Item1,status.Item2);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            PublishStatus(token, T("Import status unavailable."));
        }
    }

    private void PublishStatus(CancellationToken token, string text,bool imported=false)
    {
        lock (_statusLock)
        {
            if (token.IsCancellationRequested || IsDisposed || Disposing) return;
            _resolvedStatus = (token, text,imported);
            // A preview may still be hidden and have no HWND. Never create one
            // from the search thread; OnHandleCreated applies the saved result.
            if (!IsHandleCreated) return;
            try { BeginInvoke((Action)ApplyStatus); }
            catch (InvalidOperationException)
            {
                // The UI can destroy its handle between the check and the post.
                // A recreated handle will pick up the saved result instead.
            }
        }
    }

    private void ApplyStatus()
    {
        lock (_statusLock)
            if (!IsDisposed && !Disposing && _resolvedStatus is { } status && !status.Token.IsCancellationRequested)
            {
                Text = status.Text;
                ForeColor=status.Imported?System.Drawing.Color.LightGreen:System.Drawing.Color.Silver;
            }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_statusLock)
            {
                _pending?.Cancel(); _pending?.Dispose(); _pending = null;
                _resolvedStatus = null;
            }
        }
        base.Dispose(disposing);
    }
}
