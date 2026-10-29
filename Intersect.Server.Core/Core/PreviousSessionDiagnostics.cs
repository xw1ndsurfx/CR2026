#nullable enable
using System.Text;

namespace Intersect.Server.Core;

internal static class PreviousSessionDiagnostics
{
    internal static void Report(string directory, string currentFileName)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            var previous = Directory.EnumerateFiles(directory, "crash-Intersect Server-*.log")
                .Where(path => Path.GetFileName(path) != currentFileName)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault();
            if (previous == null) return;
            // Read a bounded tail, even if a previous exception journal is very large.
            using var stream = new FileStream(previous, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Max(0, stream.Length - 32768), SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var tail = reader.ReadToEnd();
            var lines = tail.Split('\n');
            var exit = lines.LastOrDefault(line => line.Contains("[crash-diag]") && line.Contains(" ProcessExit code="));
            var heartbeat = lines.LastOrDefault(line => line.Contains(" Runtime heartbeat;"));
            var evidence = exit ?? "No ProcessExit record in previous journal tail. Cause unknown: abrupt termination or another server process may still be running. This is not proof of OOM.";
            var report = $"Previous session journal={Path.GetFileName(previous)}\n{evidence}\nLast recorded state: {heartbeat ?? "unavailable"}\n";
            File.WriteAllText(Path.Combine(directory, "previous-session.txt"), report, new UTF8Encoding(false));
            // Do not copy the report into this journal: it must not masquerade as this session's exit.
            System.Console.Error.WriteLine("[crash-diag] " + report);
        }
        catch (Exception error) { ServerCrashLog.Record("Previous session report unavailable", error); }
    }
}
