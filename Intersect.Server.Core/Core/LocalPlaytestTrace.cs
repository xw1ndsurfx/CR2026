using System.Diagnostics;
using System.Text.Json;
using Intersect.Server.Entities;

namespace Intersect.Server.Core;

// Diagnostic capture for a loopback-only playtest process. Never captures auth/chat payloads.
internal static class LocalPlaytestTrace
{
    private static readonly object Gate = new();
    private static int _lines;
    internal static bool Enabled => Environment.GetEnvironmentVariable("INTERSECT_LOCAL_TEST_ONLY") == "1" &&
        Environment.GetEnvironmentVariable("INTERSECT_LOCAL_TEST_TRACE") == "1";

    internal static void Record(string action, Player player, object? details = null)
    {
        if (!Enabled) return;
        try
        {
            var record = new
            {
                utc = DateTimeOffset.UtcNow,
                action,
                player = player.Id,
                map = player.MapId,
                x = player.X, y = player.Y, exp = player.Exp,
                inGame = player.InGame,
                details,
                origin = new StackTrace(1, false).GetFrames()?.Take(12).Select(f =>
                    f.GetMethod()?.DeclaringType?.FullName + "." + f.GetMethod()?.Name).ToArray(),
            };
            lock (Gate)
            {
                if (_lines >= 50000) return;
                Directory.CreateDirectory("logs");
                File.AppendAllText(Path.Combine("logs", "local-playtest-trace.jsonl"),
                    JsonSerializer.Serialize(record) + Environment.NewLine);
                _lines++;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
