using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms;
using Intersect.Framework.Core.QualityOfLife;

internal static class ImportedResourceIndicatorTests
{
    internal static void Run()
    {
        // Isolate the deliberately missing synchronization context from other
        // WinForms fixtures and their automatically installed message context.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { RunCore(); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Check(thread.Join(TimeSpan.FromSeconds(30)), "Import indicator regression completed");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void RunCore()
    {
        var root = Path.Combine(Path.GetTempPath(), "import-indicator-" + Guid.NewGuid().ToString("N"));
        var previousContext = SynchronizationContext.Current;
        var checkThreads = Control.CheckForIllegalCrossThreadCalls;
        Directory.CreateDirectory(root);
        try
        {
            // The failing CI path constructs previews without a message-loop
            // context. Search completion must not create a HWND on the pool.
            SynchronizationContext.SetSynchronizationContext(null);
            Control.CheckForIllegalCrossThreadCalls = true;
            var source = Path.Combine(root, "source.wav");
            var folder = Path.Combine(root, "game");
            var cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(folder);
            File.WriteAllText(source, "sample audio");
            File.Copy(source, Path.Combine(folder, "renamed.wav"));
            var uiThread = Environment.CurrentManagedThreadId;
            using var indicator = new ImportedResourceIndicator();
            indicator.HandleCreated += (_, _) => Check(Environment.CurrentManagedThreadId == uiThread, "HWND created on the UI thread");
            indicator.TextChanged += (_, _) => Check(Environment.CurrentManagedThreadId == uiThread, "Text updated on the UI thread");
            void Finish(Task search)
            {
                Check(search.Wait(TimeSpan.FromSeconds(10)), "Import lookup completed without waiting on the UI loop");
                search.GetAwaiter().GetResult();
            }
            string T(string text) => QualityOfLifeText.Get(text, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            var imported = string.Format(T("Already imported: {0}"), "renamed.wav");
            Finish(indicator.RefreshStatus(source, folder, cache));
            Check(!indicator.IsHandleCreated, "Background completion must not create a hidden control handle");
            _ = indicator.Handle;
            Check(indicator.Text == imported, "Showing a hidden preview applies its completed result");

            var missing = Path.Combine(root, "not-imported.wav");
            File.WriteAllText(missing, "different sample");
            Finish(indicator.RefreshStatus(source, folder, cache)); // Leaves an old UI callback queued.
            Finish(indicator.RefreshStatus(missing, folder, cache));
            Application.DoEvents();
            Check(indicator.Text == T("No matching game copy detected."), "Rapid selection keeps only the latest result");
            Finish(indicator.RefreshStatus(source, folder, cache));
            Finish(indicator.RefreshStatus(null, folder, cache));
            Application.DoEvents();
            Check(indicator.Text == "", "Clearing selection cancels queued results");

            Finish(indicator.RefreshStatus(Path.Combine(root, "deleted.wav"), folder, cache));
            Application.DoEvents();
            Check(indicator.Text == T("Import status unavailable."), "Missing source reports an unavailable status safely");
            Finish(indicator.RefreshStatus(source, folder, cache));
            typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(indicator, null);
            Application.DoEvents();
            Check(indicator.Text == imported, "Handle recreation preserves the latest completed result");

            var disposed = new ImportedResourceIndicator();
            _ = disposed.Handle;
            var pending = disposed.RefreshStatus(source, folder, cache);
            disposed.Dispose();
            Finish(pending);
            Application.DoEvents();
            var hidden = new ImportedResourceIndicator();
            pending = hidden.RefreshStatus(source, folder, cache);
            hidden.Dispose();
            Finish(pending);
            Application.DoEvents();
            Console.WriteLine("PASS import indicator: UI-thread handles/text, hidden preview, rapid selection, cancellation, missing source, handle recreation and disposal");
        }
        finally
        {
            Control.CheckForIllegalCrossThreadCalls = checkThreads;
            SynchronizationContext.SetSynchronizationContext(previousContext);
            Directory.Delete(root, true);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
