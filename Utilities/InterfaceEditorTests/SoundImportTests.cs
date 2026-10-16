using System.IO.Compression;
using System.Reflection;
using System.Text;
using SharpDX.MediaFoundation;
using System.Runtime.InteropServices;
using Intersect.Editor.Content;
using Intersect.Editor.Forms;

internal static class SoundImportTests
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "CR2026-sounds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "source.wav");
            MakeWave(input, false);
            var destination = Path.Combine(root, "game", "hit.wav");
            var first = SoundImporter.Import(input, destination, default); Validate(first);
            var original = File.ReadAllBytes(first);
            var second = SoundImporter.Import(input, destination, default);
            var third = SoundImporter.Import(input, destination.ToUpperInvariant(), default);
            Check(Path.GetFileName(second) == "2_hit.wav" && Path.GetFileName(third).Equals("3_HIT.wav", StringComparison.OrdinalIgnoreCase),
                "Duplicate names receive prefixes, including case-only collisions");
            Check(File.ReadAllBytes(first).SequenceEqual(original), "Import preserves the first existing file");
            var floatWave = Path.Combine(root, "float.wav"); MakeWave(floatWave, true);
            Validate(SoundImporter.Import(floatWave, Path.Combine(root, "converted.wav"), default));
            var mp3 = Path.Combine(root, "encoded.mp3"); MakeMp3(mp3);
            Validate(SoundImporter.Import(mp3, Path.Combine(root, "mp3.wav"), default));
            System.Console.WriteLine("PASS actual MP3 to PCM WAV conversion");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try { SoundImporter.Import(input, destination, cancelled.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { }
            }
            var bad = Path.Combine(root, "bad.mp3"); File.WriteAllText(bad, "not audio");
            try { SoundImporter.Import(bad, destination, default); throw new InvalidOperationException("Invalid audio was accepted"); }
            catch (SharpDX.SharpDXException) { }
            Check(Directory.GetFiles(Path.GetDirectoryName(destination)!).Length == 3 &&
                !Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "Failures and cancellation leave no partial game assets");
            Parallel.For(0, 4, _ => SoundImporter.Import(input, Path.Combine(root, "parallel", "hit.wav"), default));
            Check(Directory.GetFiles(Path.Combine(root, "parallel")).Length == 4, "Concurrent imports preserve all duplicates without overwrite");

            // Exercise the actual ZIP and loose-file import paths, not just the converter.
            using var form = new FrmSoundImport();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(FrmSoundImport).GetField("_importRoot", flags)!.SetValue(form, Path.Combine(root, "library"));
            var zip = Path.Combine(root, "sounds.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "one/hit.wav", "two/hit.WAV" })
                { using var output = archive.CreateEntry(name).Open(); using var source = File.OpenRead(input); source.CopyTo(output); }
                using (var output = archive.CreateEntry("encoded.mp3").Open())
                using (var source = File.OpenRead(mp3)) source.CopyTo(output);
                using var broken = new StreamWriter(archive.CreateEntry("bad.mp3").Open()); broken.Write("invalid");
            }
            var progressType = typeof(FrmSoundImport).GetNestedType("SoundImportProgress", BindingFlags.NonPublic)!;
            var progress = Activator.CreateInstance(typeof(QuietProgress<>).MakeGenericType(progressType))!;
            var result = typeof(FrmSoundImport).GetMethod("ProcessZipImport", flags)!.Invoke(form, [zip, progress, CancellationToken.None])!;
            int Count(string name) => (int)result.GetType().GetProperty(name)!.GetValue(result)!;
            Check(Count("Imported") == 3 && Count("RenamedDuplicates") == 1 && Count("Skipped") == 1 && Count("Converted") == 1,
                "ZIP imports convert audio, prefix duplicates and report corrupt files");
            var errors = (System.Collections.ICollection)result.GetType().GetProperty("Errors")!.GetValue(result)!;
            Check(errors.Count == 1, "Import reports individual conversion failures");
            var loose = typeof(FrmSoundImport).GetMethod("ProcessFileImport", flags)!.Invoke(form, [new[] { mp3 }, progress, CancellationToken.None])!;
            Check((int)loose.GetType().GetProperty("Imported")!.GetValue(loose)! == 1 &&
                (int)loose.GetType().GetProperty("Converted")!.GetValue(loose)! == 1, "Loose MP3 import converts automatically");
            var ogg = Environment.GetEnvironmentVariable("CR2026_TEST_OGG");
            if (!string.IsNullOrWhiteSpace(ogg))
            {
                Validate(SoundImporter.Import(ogg, Path.Combine(root, "vorbis.wav"), default));
                System.Console.WriteLine("PASS actual OGG Vorbis to PCM WAV conversion");
            }
            System.Console.WriteLine("PASS sound import: PCM/float WAV conversion, prefix collisions, concurrent imports, cancellation, corrupt audio, actual ZIP and file import");
        }
        finally
        {
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.Delete(file);
            foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length)) Directory.Delete(directory);
            Directory.Delete(root);
        }
    }
    public sealed class QuietProgress<T> : IProgress<T> { public void Report(T value) { } }
    private static void MakeMp3(string path)
    {
        using var writer = MediaFactory.CreateSinkWriterFromURL(path, IntPtr.Zero, null);
        using var compressed = new MediaType();
        compressed.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        compressed.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Mp3);
        compressed.Set(MediaTypeAttributeKeys.AudioNumChannels, 2);
        compressed.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, 44100);
        compressed.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, 16000);
        writer.AddStream(compressed, out var stream);
        using var pcm = new MediaType();
        pcm.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        pcm.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
        pcm.Set(MediaTypeAttributeKeys.AudioNumChannels, 2);
        pcm.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, 44100);
        pcm.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);
        pcm.Set(MediaTypeAttributeKeys.AudioBlockAlignment, 4);
        pcm.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, 176400);
        writer.SetInputMediaType(stream, pcm, null); writer.BeginWriting();
        var bytes = new byte[176400];
        for (var i = 0; i < 44100; i++)
        {
            var value = (short)(Math.Sin(i * 2 * Math.PI * 440 / 44100) * 8000);
            for (var channel = 0; channel < 2; channel++)
            { bytes[i * 4 + channel * 2] = (byte)(value & 255); bytes[i * 4 + channel * 2 + 1] = (byte)((value >> 8) & 255); }
        }
        using var sample = MediaFactory.CreateSample();
        using var buffer = MediaFactory.CreateMemoryBuffer(bytes.Length);
        var pointer = buffer.Lock(out _, out _);
        try { Marshal.Copy(bytes, 0, pointer, bytes.Length); } finally { buffer.Unlock(); }
        buffer.CurrentLength = bytes.Length; sample.AddBuffer(buffer);
        sample.SampleTime = 0; sample.SampleDuration = 10000000;
        writer.WriteSample(stream, sample); writer.Finalize();
    }
    private static void MakeWave(string path, bool floating)
    {
        using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
        const int frames = 2205; var bytes = frames * (floating ? 4 : 1);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)(floating ? 3 : 1)); writer.Write((short)1); writer.Write(22050);
        writer.Write(22050 * (floating ? 4 : 1)); writer.Write((short)(floating ? 4 : 1)); writer.Write((short)(floating ? 32 : 8));
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
        for (var i = 0; i < frames; i++)
        { var value = Math.Sin(i * 2 * Math.PI * 440 / 22050) * 0.25; if (floating) writer.Write((float)value); else writer.Write((byte)(128 + value * 127)); }
    }
    private static void Validate(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        Check(Encoding.ASCII.GetString(reader.ReadBytes(4)) == "RIFF", "Output is a real WAV");
        Check(reader.ReadUInt32() == reader.BaseStream.Length - 8, "RIFF length matches samples");
        reader.BaseStream.Position = 20;
        Check(reader.ReadInt16() == 1 && reader.ReadInt16() is 1 or 2, "Output is mono/stereo PCM");
        reader.BaseStream.Position = 34; Check(reader.ReadInt16() == 16, "Output is game-compatible 16-bit audio");
        reader.BaseStream.Position = 40; Check(reader.ReadUInt32() == reader.BaseStream.Length - 44, "WAV data length is correct");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
