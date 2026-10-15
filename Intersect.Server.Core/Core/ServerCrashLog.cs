#nullable enable

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Intersect.Server.Core;

/// <summary>A best-effort synchronous journal independent of the application's logging providers.</summary>
internal static class ServerCrashLog
{
    private static readonly object WriteGate = new();
    private static readonly string FileName =
        $"crash-Intersect Server-{DateTime.UtcNow:yyyy_MM_dd-HH_mm_ss_fff}-{Environment.ProcessId}.log";
    private static string? _path;

    internal static void SetDirectory(string directory)
    {
        try
        {
            var path = Path.GetFullPath(Path.Combine(directory, FileName));
            if (Interlocked.Exchange(ref _path, path) == path)
                return;

            var assembly = Assembly.GetEntryAssembly();
            var commit = assembly?.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "CommitSha")?.Value ?? "unknown";
            Record($"Journal opened; version={assembly?.GetName().Version}; commit={commit}; " +
                   $"runtime={RuntimeInformation.FrameworkDescription}; os={RuntimeInformation.OSDescription}; " +
                   $"architecture={RuntimeInformation.ProcessArchitecture}; path={path}");
        }
        catch (Exception error)
        {
            WriteFallback($"Cannot configure crash journal: {error}");
        }
    }

    internal static void Record(string message, Exception? exception = null)
    {
        var entered = false;
        string? entry = null;
        try
        {
            entry = $"{DateTimeOffset.UtcNow:O} [crash-diag] pid={Environment.ProcessId} " +
                    $"thread={Environment.CurrentManagedThreadId} {message}" +
                    (exception == null ? string.Empty : $"{Environment.NewLine}{exception}") +
                    Environment.NewLine;
            var path = Volatile.Read(ref _path);
            if (path == null)
                return;

            // An exception on another thread must not wait indefinitely for this journal's lock.
            entered = Monitor.TryEnter(WriteGate, TimeSpan.FromMilliseconds(250));
            if (!entered)
            {
                WriteFallback("Crash journal busy; " + entry);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete,
                4096, FileOptions.WriteThrough
            );
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(entry);
                writer.Flush();
            }
            stream.Flush(flushToDisk: true);
        }
        catch (Exception error)
        {
            WriteFallback($"Crash journal write failed: {error}{Environment.NewLine}{entry ?? message}");
        }
        finally
        {
            if (entered)
                Monitor.Exit(WriteGate);
        }
    }

    private static void WriteFallback(string message)
    {
        try
        {
            System.Console.Error.WriteLine(message);
            System.Console.Error.Flush();
        }
        catch { }
    }
}
