using System.Diagnostics;
using System.Runtime.CompilerServices;
using Intersect.Server.Core;
using Microsoft.Extensions.Logging;

if (args.Length > 0 && args[0] == "child")
{
    Directory.SetCurrentDirectory(args[2]);
    StartupDiagnostics.Initialize();
    StartupDiagnostics.Initialize();
    switch (args[1])
    {
        case "exit":
            StartupDiagnostics.SetLogger(new ThrowingLogger());
            StartupDiagnostics.Mark("test shutdown requested");
            StartupDiagnostics.Failure("test failure", new AggregateException(
                "outer failure", new IOException("inner failure")));
            Environment.Exit(23);
            break;
        case "unhandled":
            new Thread(() => throw new InvalidOperationException("test unhandled thread")).Start();
            Thread.Sleep(Timeout.Infinite);
            break;
        case "unobserved":
            using (var seen = new ManualResetEventSlim())
            {
                TaskScheduler.UnobservedTaskException += (_, _) => seen.Set();
                CreateFaultedTask();
                for (var i = 0; i < 100 && !seen.IsSet; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(20);
                }
                if (!seen.IsSet) return 2;
            }
            break;
        case "abrupt":
            ServerCrashLog.Record("Unverifiable monetary commit; test before termination",
                new IOException("test commit exception"));
            Console.WriteLine("durable");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);
            break;
        case "directory":
            Directory.CreateDirectory("server-data");
            Directory.SetCurrentDirectory("server-data");
            StartupDiagnostics.SetLogDirectory();
            StartupDiagnostics.Mark("after working directory changed");
            break;
    }
    return 0;
}

var root = Path.Combine(Path.GetTempPath(), "cr-diagnostics-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var failures = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
string ReadJournal(string directory) => string.Join("\n",
    Directory.GetFiles(Path.Combine(directory, "logs"), "crash-*.log").Select(File.ReadAllText));

Process StartChild(string mode, string directory)
{
    Directory.CreateDirectory(directory);
    var start = new ProcessStartInfo("dotnet")
    {
        RedirectStandardError = true, RedirectStandardOutput = true,
        UseShellExecute = false, CreateNoWindow = true,
    };
    start.ArgumentList.Add(typeof(ThrowingLogger).Assembly.Location);
    start.ArgumentList.Add("child");
    start.ArgumentList.Add(mode);
    start.ArgumentList.Add(directory);
    start.Environment["DOTNET_DbgEnableMiniDump"] = "0";
    return Process.Start(start) ?? throw new Exception("Child did not start");
}
int RunChild(string mode, string directory)
{
    using var child = StartChild(mode, directory);
    var output = child.StandardOutput.ReadToEndAsync();
    var error = child.StandardError.ReadToEndAsync();
    if (!child.WaitForExit(15000))
    {
        child.Kill(entireProcessTree: true);
        child.WaitForExit();
        throw new Exception("Child timed out: " + mode);
    }
    Task.WaitAll(output, error);
    return child.ExitCode;
}

Test("full nested exception and exit code survive a throwing logger", () =>
{
    var directory = Path.Combine(root, "exit");
    Check(RunChild("exit", directory) == 23, "Original exit code changed");
    var log = ReadJournal(directory);
    Check(log.Contains("test shutdown requested") && log.Contains("outer failure") &&
          log.Contains("inner failure") && log.Contains("ProcessExit code=23"), "Missing durable diagnostics");
    Check(log.Split("ProcessExit code=23").Length == 2, "Initialize installed duplicate exit handlers");
    Check(log.Contains("pid=") && log.Contains("thread=") && log.Contains("commit="), "Missing context");
});
Test("a real unhandled thread exception is persisted before process termination", () =>
{
    var directory = Path.Combine(root, "unhandled");
    Check(RunChild("unhandled", directory) != 0, "Unhandled exception was swallowed");
    var log = ReadJournal(directory);
    Check(log.Contains("UnhandledException terminating=True") && log.Contains("test unhandled thread") &&
          log.Contains("Program.cs:line"), "Unhandled exception or stack missing");
});
Test("a real unobserved task exception is persisted without changing task policy", () =>
{
    var directory = Path.Combine(root, "unobserved");
    Check(RunChild("unobserved", directory) == 0, "Unobserved task fixture failed");
    Check(ReadJournal(directory).Contains("test unobserved task"), "Unobserved task not recorded");
});
Test("the journal is in the configured server working directory", () =>
{
    var directory = Path.Combine(root, "directory");
    Check(RunChild("directory", directory) == 0, "Directory fixture failed");
    var log = ReadJournal(Path.Combine(directory, "server-data"));
    Check(log.Contains("after working directory changed") && log.Contains("ProcessExit"), "Journal stayed in old directory");
});
Test("fatal diagnostics are readable before exit and remain after a forced kill", () =>
{
    var directory = Path.Combine(root, "abrupt");
    using var child = StartChild("abrupt", directory);
    var error = child.StandardError.ReadToEndAsync();
    try
    {
        Check(child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult()
              == "durable", "Child did not complete synchronous write");
        Check(ReadJournal(directory).Contains("test commit exception"), "Record was buffered until exit");
    }
    finally
    {
        if (!child.HasExited) child.Kill(entireProcessTree: true);
        child.WaitForExit();
        error.GetAwaiter().GetResult();
    }
    Check(ReadJournal(directory).Contains("test commit exception"), "Record lost after kill");
});
Test("parallel writes preserve all records", () =>
{
    var directory = Path.Combine(root, "parallel");
    ServerCrashLog.SetDirectory(directory);
    Parallel.For(0, 32, index => ServerCrashLog.Record("parallel-record-" + index));
    var log = string.Join("\n", Directory.GetFiles(directory).Select(File.ReadAllText));
    for (var index = 0; index < 32; index++)
        Check(log.Contains("parallel-record-" + index + Environment.NewLine), "Concurrent record lost");
});
Test("an unwritable journal does not replace the original failure", () =>
{
    var blocked = Path.Combine(root, "blocked");
    File.WriteAllText(blocked, "A file prevents directory creation");
    var oldError = Console.Error;
    using var fallback = new StringWriter();
    Console.SetError(fallback);
    try
    {
        ServerCrashLog.SetDirectory(blocked);
        ServerCrashLog.Record("original failure marker", new IOException("original exception"));
    }
    finally { Console.SetError(oldError); }
    Check(fallback.ToString().Contains("original exception"), "Original exception was lost on journal failure");
});
Console.WriteLine($"{7 - failures}/7 crash diagnostic checks passed. Fixtures: {root}");
return failures == 0 ? 0 : 1;

[MethodImpl(MethodImplOptions.NoInlining)]
static void CreateFaultedTask()
{
    var task = Task.Run(() => throw new InvalidOperationException("test unobserved task"));
    ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
}

sealed class ThrowingLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => throw new IOException("test logger unavailable");
}
