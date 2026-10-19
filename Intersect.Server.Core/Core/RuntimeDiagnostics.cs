#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Intersect.Server.Core;

/// <summary>Low-frequency evidence for abrupt container exits that cannot run managed crash handlers.</summary>
internal static class RuntimeDiagnostics
{
    private static Timer? _timer;
    private static PosixSignalRegistration? _termination;
    private static int _started;
    private static int _writing;

    internal static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                _termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                    ServerCrashLog.Record("SIGTERM received; default termination remains enabled"));
            }
            catch (Exception error) { ServerCrashLog.Record("Cannot observe SIGTERM", error); }
        }
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { _timer?.Dispose(); _termination?.Dispose(); };
        _timer = new Timer(_ => Sample(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private static void Sample()
    {
        if (Interlocked.Exchange(ref _writing, 1) != 0) return;
        try { ServerCrashLog.Record(Snapshot()); }
        catch (Exception error) { ServerCrashLog.Record("Runtime sample unavailable", error); }
        finally { Volatile.Write(ref _writing, 0); }
    }

    internal static string Snapshot()
    {
        using var process = Process.GetCurrentProcess();
        var gc = GC.GetGCMemoryInfo();
        ThreadPool.GetAvailableThreads(out var workers, out var io);
        var message = FormattableString.Invariant(
            $"Runtime heartbeat; workingSetBytes={process.WorkingSet64}; privateBytes={process.PrivateMemorySize64}; managedBytes={GC.GetTotalMemory(false)}; gcAvailableBytes={gc.TotalAvailableMemoryBytes}; gcHeapBytes={gc.HeapSizeBytes}; gcCommittedBytes={gc.TotalCommittedBytes}; cpuSeconds={process.TotalProcessorTime.TotalSeconds:F1}; processThreads={process.Threads.Count}; poolThreads={ThreadPool.ThreadCount}; poolPending={ThreadPool.PendingWorkItemCount}; poolAvailableWorkers={workers}; poolAvailableIO={io}");
        if (!OperatingSystem.IsLinux()) return message;
        return message + "; " + ReadContainerMemory("/sys/fs/cgroup");
    }

    internal static string ReadContainerMemory(string root)
    {
        var values = new List<string>();
        foreach (var relative in new[] { "memory.current", "memory.max", "memory.events", "memory/memory.usage_in_bytes", "memory/memory.limit_in_bytes", "memory/memory.failcnt" })
        {
            try
            {
                var path = Path.Combine(root, relative);
                if (!File.Exists(path)) continue;
                using var reader = File.OpenText(path);
                var buffer = new char[4096];
                var length = reader.ReadBlock(buffer, 0, buffer.Length);
                var text = new string(buffer, 0, length).Trim().Replace('\r', ' ').Replace('\n', ',');
                values.Add(relative + "=" + text);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        return values.Count > 0 ? string.Join("; ", values) : "cgroupMemory=unavailable";
    }
}
