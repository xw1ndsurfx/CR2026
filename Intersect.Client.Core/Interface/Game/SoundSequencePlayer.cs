using Intersect.Client.Core.Sounds;
using Intersect.Client.Framework.Core.Sounds;
namespace Intersect.Client.Interface.Game;
internal sealed class SoundSequencePlayer
{
    private readonly Queue<string> _files = new();
    private readonly Func<string, ISound> _create;
    private ISound? _playing;
    public SoundSequencePlayer() : this(file => new Sound(file, false, 0)) { }
    internal SoundSequencePlayer(Func<string, ISound> create) => _create = create;
    public void Play(string first, string second)
    {
        Stop();
        foreach (var file in new[] { first, second })
            if (!string.IsNullOrWhiteSpace(file) && file.Length <= 260 && !file.Contains('/') && !file.Contains('\\')) _files.Enqueue(file);
        Update();
    }
    public void Update()
    {
        if (_playing != null && _playing.Update()) return;
        _playing?.Stop(); _playing = null;
        while (_files.TryDequeue(out var file))
        {
            var sound = _create(file);
            if (sound.Loaded) { _playing = sound; break; }
            sound.Stop();
        }
    }
    public void Stop() { _playing?.Stop(); _playing = null; _files.Clear(); }
}
