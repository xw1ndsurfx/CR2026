#nullable enable
using System.Text;

namespace Intersect.Server.Core;

/// <summary>Bounded console history, independent of the configured logging providers.</summary>
internal static class ConsoleDiagnostics
{
    private static readonly object Gate = new();
    private static string? _directory;
    private static bool _installed;
    internal const int FileLimit = 4 * 1024 * 1024;

    internal static void Configure(string directory)
    {
        lock (Gate)
        {
            _directory = Path.GetFullPath(directory);
            if (_installed) return;
            _installed = true;
            System.Console.SetOut(new Mirror(System.Console.Out));
            System.Console.SetError(new Mirror(System.Console.Error));
        }
    }

    private static void Append(string text)
    {
        // Both stdout and stderr share the gate; rotation must never race a write.
        lock (Gate)
        {
            try
            {
                if (_directory == null || text.Length == 0) return;
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, "server-console.log");
                for (var offset = 0; offset < text.Length;)
                {
                    if (File.Exists(path) && new FileInfo(path).Length >= FileLimit)
                    {
                        File.Delete(path + ".3");
                        for (var index = 2; index >= 1; index--)
                            if (File.Exists(path + "." + index))
                                File.Move(path + "." + index, path + "." + (index + 1), true);
                        File.Move(path, path + ".1", true);
                    }
                    // Bound even a single unusually large console write.
                    var count = Math.Min(4096, text.Length - offset);
                    File.AppendAllText(path, text.Substring(offset, count), new UTF8Encoding(false));
                    offset += count;
                }
            }
            // Never replace an application failure with a diagnostic I/O failure, or recurse via stderr.
            catch { }
        }
    }

    private sealed class Mirror(TextWriter original) : TextWriter
    {
        public override Encoding Encoding => original.Encoding;
        public override void Write(char value) => Write(value.ToString());
        public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));
        public override void Write(string? value)
        {
            if (value == null) return;
            Append(value);
            original.Write(value);
        }
        public override void WriteLine(string? value) => Write(value + NewLine);
        public override void Flush() => original.Flush();
    }
}
