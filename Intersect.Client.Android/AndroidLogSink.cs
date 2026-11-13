using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace Intersect.Client.Android;

internal sealed class AndroidLogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        if (logEvent.Exception is { } exception) message += "\n" + exception;
        var priority = logEvent.Level switch
        {
            LogEventLevel.Fatal or LogEventLevel.Error => global::Android.Util.LogPriority.Error,
            LogEventLevel.Warning => global::Android.Util.LogPriority.Warn,
            LogEventLevel.Information => global::Android.Util.LogPriority.Info,
            _ => global::Android.Util.LogPriority.Debug,
        };
        global::Android.Util.Log.WriteLine(priority, "CR2026", message);
    }
}
