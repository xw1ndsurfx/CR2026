using System.Reflection;
using System.Text.Json;
using Intersect.Client.Framework.Audio;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.MonoGame.Audio;
using Intersect.Client.MonoGame.File_Management;
using Intersect.Compression;
using Intersect.Configuration;
using Intersect.Framework.Core.Fishing;
using NUnit.Framework;

namespace Intersect.Tests.Client;

/// <summary>Uses the production sound indexer without opening an audio device.</summary>
[TestFixture, NonParallelizable]
public sealed class FishingAudioTests
{
    [Test]
    public void LooseSoundFilesKeepFolderKeysWithoutCollidingWithLegacyBasenames()
    {
        using var fixture = new SoundFixture();
        var legacy = fixture.File("sounds/Legacy.wav");
        var opening = fixture.File("sounds/Fishing/Fishing_UI_Open.wav");
        var catchAtRoot = fixture.File("sounds/Catch.wav");
        var fishingCatch = fixture.File("sounds/Fishing/Catch.wav");
        var harbourCatch = fixture.File("sounds/Harbour/Catch.wav");
        var voice = fixture.File("voicesound/Dialog.wav");
        fixture.File("sounds/Fishing/ignored.png");

        fixture.Content.LoadSounds();

        var defaultOpening = FishingSoundCatalog.Default("session_start").File;
        AssertSource(fixture.Content.GetSound(GameContentManager.RemoveExtension(defaultOpening)), opening);
        AssertSource(fixture.Content.GetSound("legacy"), legacy);
        AssertSource(fixture.Content.GetSound("catch"), catchAtRoot);
        AssertSource(fixture.Content.GetSound("fishing/catch"), fishingCatch);
        AssertSource(fixture.Content.GetSound("harbour/catch"), harbourCatch);
        AssertSource(fixture.Content.GetSound("voicesound/dialog"), voice);
        Assert.That(fixture.Content.SoundNames, Is.EquivalentTo(new[]
        {
            "legacy", "fishing/fishing_ui_open", "catch", "fishing/catch", "harbour/catch", "voicesound/dialog",
        }));
        Assert.That(fixture.Content.GetSound("fishing_ui_open"), Is.Null,
            "Nested files must not create basename aliases that collide with old sounds.");
    }

    [Test]
    public void LooseSoundsRetainPrecedenceOverPacksAndPackedOnlySoundsRemainAvailable()
    {
        using var fixture = new SoundFixture();
        var looseRoot = fixture.File("sounds/Legacy.wav");
        var looseNested = fixture.File("sounds/Fishing/Catch.wav");
        var index = new[] { "legacy.wav", "fishing/catch.wav", "fishing/fishing_ui_open.wav" }
            .Select(name => new { FileName = name, PackName = "sound.pack", FileStartByte = 0, FileEndByte = 0 });
        var packDirectory = Path.Combine(fixture.Root, "packs");
        Directory.CreateDirectory(packDirectory);
        GzipCompression.WriteCompressedString(Path.Combine(packDirectory, "sound.index"), JsonSerializer.Serialize(index));

        fixture.Content.LoadSounds();

        AssertSource(fixture.Content.GetSound("legacy"), looseRoot);
        AssertSource(fixture.Content.GetSound("fishing/catch"), looseNested);
        AssertSource(fixture.Content.GetSound("fishing/fishing_ui_open"), "fishing/fishing_ui_open.wav");
        Assert.That(fixture.Content.SoundPacks.Contains("fishing/fishing_ui_open.wav"), Is.True);
        Assert.That(fixture.Content.SoundNames, Is.EquivalentTo(new[]
        {
            "legacy", "fishing/catch", "fishing/fishing_ui_open",
        }));
    }

    private static void AssertSource(GameAudioSource? source, string realPath)
    {
        Assert.That(source, Is.TypeOf<MonoSoundSource>());
        Assert.That(source!.IsLoaded, Is.False, "Indexing must not require an audio device.");
        Assert.That(typeof(MonoSoundSource).GetField("mRealPath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(source), Is.EqualTo(realPath), "The normalized lookup key must preserve the actual file path and case.");
    }

    private sealed class SoundFixture : IDisposable
    {
        private static readonly FieldInfo Current = typeof(GameContentManager)
            .GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousContent = Current.GetValue(null);
        private readonly string _previousResources = ClientConfiguration.ResourcesDirectory;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "fishing-sounds-" + Guid.NewGuid().ToString("N"));
        internal MonoContentManager Content { get; }

        internal SoundFixture()
        {
            Directory.CreateDirectory(Root);
            ClientConfiguration.ResourcesDirectory = Root;
            Content = new MonoContentManager();
        }

        internal string File(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Sound files are registered lazily; decoding belongs to playback.
            System.IO.File.WriteAllBytes(path, []);
            return path;
        }

        public void Dispose()
        {
            Content.SoundPacks?.Dispose();
            // ContentWatcher currently has no public disposal hook.
            ((FileSystemWatcher)typeof(ContentWatcher).GetField("_watcher", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Content.ContentWatcher)!).Dispose();
            Current.SetValue(null, _previousContent);
            ClientConfiguration.ResourcesDirectory = _previousResources;
            Directory.Delete(Root, recursive: true);
        }
    }
}
