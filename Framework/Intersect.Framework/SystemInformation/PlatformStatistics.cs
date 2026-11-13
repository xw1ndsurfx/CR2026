using Hardware.Info;
using Microsoft.Extensions.Logging;

namespace Intersect.Framework.SystemInformation;

public class PlatformStatistics
{
    private static readonly HardwareInfo? HardwareInfo;

    public static IGPUStatisticsProvider? GPUStatisticsProvider { get; set; }

    public static ILogger? Logger { get; set; }

    public static long AvailablePhysicalMemory => HardwareInfo is { } info
        ? (long)info.MemoryStatus.AvailablePhysical
        : Math.Max(0, TotalPhysicalMemory - GC.GetTotalMemory(false));

    public static long TotalPhysicalMemory => HardwareInfo is { } info
        ? (long)info.MemoryStatus.TotalPhysical
        : GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public static long AvailableGPUMemory => GPUStatisticsProvider?.AvailableMemory ?? AvailableSystemMemory;

    public static long TotalGPUMemory => GPUStatisticsProvider?.TotalMemory ?? TotalSystemMemory;

    public static long AvailableSystemMemory => HardwareInfo is { } info
        ? (long)info.MemoryStatus.AvailableVirtual : AvailablePhysicalMemory;

    public static long TotalSystemMemory => HardwareInfo is { } info
        ? (long)info.MemoryStatus.TotalVirtual : TotalPhysicalMemory;

    public static void Refresh()
    {
        try
        {
            HardwareInfo?.RefreshMemoryStatus();
        }
        catch
        {
            // Do nothing
        }
    }

    static PlatformStatistics()
    {
        // Hardware.Info only implements Windows, macOS and Linux desktop probes.
        // Android can report the managed memory budget without loading those probes.
        if (!OperatingSystem.IsAndroid()) HardwareInfo = new HardwareInfo();

        Refresh();
    }
}
