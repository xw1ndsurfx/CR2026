using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Intersect.Client.Core;

internal readonly record struct ClientLoopReport(string Kind, string Phase, long DelayMilliseconds, long Generation);

/// <summary>Independent clock/progress probe; never invokes game, input or graphics APIs from the watcher thread.</summary>
internal sealed class ClientLoopProgress(
    Func<long> clock, long threshold = 15_000, Action<ClientLoopReport>? completedPhase = null)
{
    private string _phase = "starting";
    private long _progressAt = clock();
    private long _generation;
    private long _reportedGeneration = -1;
    private long _lastHeartbeatAt = clock();
    private bool _stalled;
    internal void Mark(string phase)
    {
        var now = clock();
        var previousPhase = Volatile.Read(ref _phase);
        var duration = Math.Max(0, now - Interlocked.Exchange(ref _progressAt, now));
        Volatile.Write(ref _phase, phase);
        var generation = Interlocked.Increment(ref _generation);
        // Completed slow frames still make progress, so the 15-second stall detector alone cannot identify them.
        if (duration >= 100) completedPhase?.Invoke(new("slow-phase", previousPhase, duration, generation));
    }
    // Only the watcher calls Poll; Mark is called on the game thread.
    internal ClientLoopReport? Poll()
    {
        var now = clock();
        var generation = Interlocked.Read(ref _generation);
        var delay = Math.Max(0, now - Interlocked.Read(ref _progressAt));
        var phase = Volatile.Read(ref _phase);
        if (delay >= threshold)
        {
            if (_reportedGeneration == generation) return null;
            _reportedGeneration = generation; _stalled = true; _lastHeartbeatAt = now;
            return new("no-progress", phase, delay, generation);
        }
        if (_stalled)
        {
            _stalled = false; _lastHeartbeatAt = now;
            return new("resumed", phase, delay, generation);
        }
        if (now - _lastHeartbeatAt >= 30_000)
        {
            _lastHeartbeatAt = now;
            return new("heartbeat", phase, delay, generation);
        }
        return null;
    }
}

/// <summary>Diagnostic-only watcher. A stall report is an observation, not proof of a deadlock.</summary>
internal sealed class ClientLoopDiagnostics : IDisposable
{
    private static ClientLoopDiagnostics? _current;
    private readonly ClientLoopProgress _progress;
    private readonly ILogger _logger;
    private readonly string _path;
    private readonly ConcurrentQueue<string> _messages = new();
    private int _queuedMessages;
    private readonly Timer _timer;
    private int _polling, _stopped;
    private readonly int _gameThread = Environment.CurrentManagedThreadId;

    private ClientLoopDiagnostics(ILogger logger)
    {
        _logger = logger;
        _progress = new ClientLoopProgress(() => Environment.TickCount64, completedPhase: report =>
            Enqueue($"{report.Kind}: phase={report.Phase}; durationMs={report.DelayMilliseconds}; progress={report.Generation}"));
        _path = Path.Combine("logs", $"client-loop-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Enqueue($"started diagnosticRevision=2; version={version}; runtime={Environment.Version}; os={RuntimeInformation.OSDescription}; architecture={RuntimeInformation.ProcessArchitecture}; gameThread={_gameThread}");
        logger.LogInformation("Client loop diagnostics: {Path}", _path);
        _timer = new Timer(_ => Poll(), null, 2000, 2000);
    }
    internal static ClientLoopDiagnostics Start(ILogger logger)
    {
        var instance = new ClientLoopDiagnostics(logger);
        Interlocked.Exchange(ref _current, instance)?.Dispose();
        return instance;
    }
    internal static void Mark(string phase) => Volatile.Read(ref _current)?._progress.Mark(phase);
    internal static void Note(string message) => Volatile.Read(ref _current)?.Enqueue(message);
    private void Poll()
    {
        if (Volatile.Read(ref _stopped) != 0 || Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            while (_messages.TryDequeue(out var message))
            {
                Interlocked.Decrement(ref _queuedMessages);
                Write(message);
            }
            if (_progress.Poll() is not { } report) return;
            var memory = GC.GetTotalMemory(false);
            Write($"{report.Kind}: phase={report.Phase}; ageMs={report.DelayMilliseconds}; progress={report.Generation}; managedBytes={memory}");
            if (report.Kind == "no-progress") _logger.LogWarning(
                "Client loop has made no progress for {Age} ms at {Phase}. It may be blocked or paused. See {Path}",
                report.DelayMilliseconds, report.Phase, _path);
        }
        catch { /* Diagnostics must not terminate or block the client when a sink is unavailable. */ }
        finally { Volatile.Write(ref _polling, 0); }
    }
    // UI callbacks enqueue only: even an unavailable/suspended OneDrive path cannot block game input.
    private void Enqueue(string message)
    {
        if (Volatile.Read(ref _stopped) != 0) return;
        if (Interlocked.Increment(ref _queuedMessages) > 64) { Interlocked.Decrement(ref _queuedMessages); return; }
        _messages.Enqueue(message);
    }
    private void Write(string message)
    {
        try
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, $"{DateTime.UtcNow:O} [client-loop] {message}\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        Interlocked.CompareExchange(ref _current, null, this);
        _timer.Dispose();
    }
}
