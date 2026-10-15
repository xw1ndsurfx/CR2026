#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Core;

/// <summary>Low-volume startup breadcrumbs, also written synchronously outside the logging pipeline.</summary>
internal static class StartupDiagnostics
{
    private static string _lastStage = "Main entered";
    private static ILogger? _logger;
    private static int _initialized;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
            return;

        SetLogDirectory();
        // Subscribe before the application's shutdown handlers so the original failure is durable first.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CaptureFailure($"UnhandledException terminating={args.IsTerminating}",
                args.ExceptionObject as Exception ?? new Exception("Non-Exception unhandled object"));
        TaskScheduler.UnobservedTaskException += (_, args) =>
            CaptureFailure("UnobservedTaskException", args.Exception);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            WriteConsole($"ProcessExit code={Environment.ExitCode}; lastStage={Volatile.Read(ref _lastStage)}");
    }

    // Bootstrapper can change the working directory after Main has installed the early handlers.
    internal static void SetLogDirectory() =>
        ServerCrashLog.SetDirectory(Path.Combine(Environment.CurrentDirectory, "logs"));

    internal static void SetLogger(ILogger logger) => _logger = logger;

    internal static void CaptureFailure(string stage, Exception exception)
    {
        Volatile.Write(ref _lastStage, $"FAILED {stage}");
        // Do not call application logging providers from this early exception handler.
        ServerCrashLog.Record($"FAILED {stage}", exception);
    }

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
        // Persist before touching stderr or logging providers, either of which can fail independently.
        ServerCrashLog.Record(message);
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
