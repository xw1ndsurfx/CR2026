using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Core;

/// <summary>Low-volume startup breadcrumbs, also written synchronously outside the logging pipeline.</summary>
internal static class StartupDiagnostics
{
    private static string _lastStage = "Main entered";
    private static ILogger _logger;

    internal static void Initialize() => AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        WriteConsole($"ProcessExit code={Environment.ExitCode}; lastStage={Volatile.Read(ref _lastStage)}");

    internal static void SetLogger(ILogger logger) => _logger = logger;

    internal static void Mark(string stage)
    {
        Volatile.Write(ref _lastStage, stage);
        WriteConsole(stage);
        // Diagnostics must not replace the original startup failure if a sink is unavailable.
        try { _logger?.LogInformation("[startup-diag] {Stage}", stage); }
        catch { }
    }

    internal static void Failure(string stage, Exception exception)
    {
        Volatile.Write(ref _lastStage, $"FAILED {stage}");
        WriteConsole($"FAILED {stage}\n{exception}");
        try { _logger?.LogError(exception, "[startup-diag] FAILED {Stage}", stage); }
        catch { }
    }

    internal static void Run(string stage, Action action)
    {
        var timer = Stopwatch.StartNew();
        Mark($"BEGIN {stage}");
        try
        {
            action();
            Mark($"END {stage} ({timer.ElapsedMilliseconds}ms)");
        }
        catch (Exception exception)
        {
            Failure(stage, exception);
            throw;
        }
    }

    private static void WriteConsole(string message)
    {
        try
        {
            System.Console.Error.WriteLine(
                $"{DateTimeOffset.UtcNow:O} [startup-diag] pid={Environment.ProcessId} thread={Environment.CurrentManagedThreadId} {message}"
            );
            System.Console.Error.Flush();
        }
        catch { }
    }
}
