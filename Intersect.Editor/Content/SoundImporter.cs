using System.Runtime.InteropServices;
using System.Text;
using NVorbis;
using SharpDX.MediaFoundation;

namespace Intersect.Editor.Content;

public enum SoundImportKind { Sound, VoiceSound }

/// <summary>Produces game-compatible PCM WAV files without overwriting existing assets.</summary>
public static class SoundImporter
{
    public static string Folder(SoundImportKind kind) => kind switch
    {
        SoundImportKind.Sound => "sounds",
        SoundImportKind.VoiceSound => VoiceSoundFiles.Folder,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static string DisplayName(SoundImportKind kind) => kind == SoundImportKind.VoiceSound ? "Voice Sound" : "Sound";
    public static string GameFolder(string gameRoot, SoundImportKind kind) => Path.Combine(gameRoot, "resources", Folder(kind));
    public static string LibraryFolder(string libraryRoot, SoundImportKind kind) => Path.Combine(libraryRoot, Folder(kind));
    public static SoundImportKind LibraryKind(string libraryRoot, string file)
    {
        var first = Path.GetRelativePath(libraryRoot, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        // Existing untyped staging folders remain effects; never silently relocate old assets.
        return first.Equals(VoiceSoundFiles.Folder, StringComparison.OrdinalIgnoreCase) ? SoundImportKind.VoiceSound : SoundImportKind.Sound;
    }

    public const string FileFilter = "Audio files|*.wav;*.mp3;*.ogg;*.flac;*.m4a;*.aac;*.wma;*.aif;*.aiff|All files|*.*";
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".wav", ".mp3", ".ogg", ".flac", ".m4a", ".aac", ".wma", ".aif", ".aiff" };
    private static readonly Lazy<bool> MediaFoundation = new(() => { MediaManager.Startup(); return true; });
    public static bool Supports(string path) => Extensions.Contains(Path.GetExtension(path));

    public static string UniquePath(string requested)
    {
        if (!File.Exists(requested) && !Directory.Exists(requested)) return requested;
        var folder = Path.GetDirectoryName(requested) ?? "";
        var name = Path.GetFileName(requested);
        for (var number = 2; ; number++)
        {
            var candidate = Path.Combine(folder, $"{number}_{name}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    public static string Import(string source, string requestedDestination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = Path.ChangeExtension(requestedDestination, ".wav");
        var folder = Path.GetDirectoryName(Path.GetFullPath(requested))!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".sound-import-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                if (Path.GetExtension(source).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                    DecodeVorbis(source, output, cancellationToken);
                else DecodeMediaFoundation(source, output, cancellationToken);
            }
            if (new FileInfo(temporary).Length <= 44) throw new InvalidDataException("The audio file contains no samples.");
            cancellationToken.ThrowIfCancellationRequested();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = UniquePath(requested);
                try { File.Move(temporary, destination); return destination; }
                catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void DecodeMediaFoundation(string source, Stream output, CancellationToken token)
    {
        _ = MediaFoundation.Value;
        using var reader = new SourceReader(Path.GetFullPath(source));
        reader.SetStreamSelection(SourceReaderIndex.AllStreams, false);
        reader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);
        using var requested = new MediaType();
        requested.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        requested.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
        requested.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);
        reader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, requested);
        using var format = reader.GetCurrentMediaType(SourceReaderIndex.FirstAudioStream);
        var channels = format.Get(MediaTypeAttributeKeys.AudioNumChannels);
        var rate = format.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond);
        if (format.Get(MediaTypeAttributeKeys.AudioBitsPerSample) != 16)
            throw new InvalidDataException("The audio decoder did not produce 16-bit PCM.");
        using var wave = new PcmWaveWriter(output, rate, Math.Min(channels, 2));
        while (true)
        {
            token.ThrowIfCancellationRequested();
            using var sample = reader.ReadSample(SourceReaderIndex.FirstAudioStream, SourceReaderControlFlags.None,
                out _, out var flags, out _);
            if ((flags & (SourceReaderFlags.Error | SourceReaderFlags.Currentmediatypechanged)) != 0)
                throw new InvalidDataException("The audio format changed or could not be decoded.");
            if (sample != null)
            {
                using var buffer = sample.ConvertToContiguousBuffer();
                var pointer = buffer.Lock(out _, out var length);
                try
                {
                    var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
                    if (channels <= 2) wave.Write(bytes);
                    else
                    {
                        var floats = new float[length / 2];
                        for (var i = 0; i < floats.Length; i++) floats[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
                        wave.WriteSamples(floats, floats.Length, channels);
                    }
                }
                finally { buffer.Unlock(); }
            }
            if ((flags & SourceReaderFlags.Endofstream) != 0) break;
        }
    }

    private static void DecodeVorbis(string source, Stream output, CancellationToken token)
    {
        using var reader = new VorbisReader(source);
        using var wave = new PcmWaveWriter(output, reader.SampleRate, Math.Min(reader.Channels, 2));
        var samples = new float[4096 * reader.Channels];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = reader.ReadSamples(samples, 0, samples.Length);
            if (count == 0) break;
            wave.WriteSamples(samples, count, reader.Channels);
        }
    }

    private sealed class PcmWaveWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _channels;
        public PcmWaveWriter(Stream stream, int rate, int channels)
        {
            if (rate <= 0 || channels is < 1 or > 2) throw new InvalidDataException("Invalid audio format.");
            _stream = stream; _channels = channels;
            _writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            _writer.Write(Encoding.ASCII.GetBytes("RIFF")); _writer.Write(0u);
            _writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); _writer.Write(16u);
            _writer.Write((short)1); _writer.Write((short)channels); _writer.Write(rate);
            _writer.Write(checked(rate * channels * 2)); _writer.Write((short)(channels * 2)); _writer.Write((short)16);
            _writer.Write(Encoding.ASCII.GetBytes("data")); _writer.Write(0u);
        }
        public void Write(byte[] bytes)
        {
            if (bytes.Length % (_channels * 2) != 0) throw new InvalidDataException("Incomplete PCM frame.");
            if (_stream.Length + bytes.Length > uint.MaxValue) throw new InvalidDataException("Audio exceeds the WAV file size limit.");
            _writer.Write(bytes);
        }
        public void WriteSamples(float[] samples, int count, int sourceChannels)
        {
            if (sourceChannels < 1 || count % sourceChannels != 0) throw new InvalidDataException("Incomplete audio frame.");
            var bytes = new byte[count / sourceChannels * _channels * 2];
            var offset = 0;
            for (var frame = 0; frame < count; frame += sourceChannels)
                for (var channel = 0; channel < _channels; channel++)
                {
                    double sum = 0; var mixed = 0;
                    for (var sourceChannel = channel; sourceChannel < sourceChannels; sourceChannel += _channels)
                    { sum += samples[frame + sourceChannel]; mixed++; }
                    var value = sum / mixed;
                    if (!double.IsFinite(value)) value = 0;
                    var pcm = (short)Math.Clamp(Math.Round(value * 32768d), short.MinValue, short.MaxValue);
                    bytes[offset++] = (byte)(pcm & 255); bytes[offset++] = (byte)((pcm >> 8) & 255);
                }
            Write(bytes);
        }
        public void Dispose()
        {
            var length = _stream.Length;
            _stream.Position = 4; _writer.Write(checked((uint)(length - 8)));
            _stream.Position = 40; _writer.Write(checked((uint)(length - 44)));
            _writer.Flush(); _stream.Position = length;
        }
    }
}
