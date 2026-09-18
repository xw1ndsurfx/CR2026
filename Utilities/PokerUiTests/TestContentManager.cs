using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;

// The real title bar resolves a named font through GameContentManager.Current during its
// constructor. Reproduce that part of normal client startup before constructing any window.
// This fixture is compiled only into the test executable; it performs no asset I/O and does
// not change PokerWindow, Titlebar, Text, or their production initialization paths.
internal static class TestContentBootstrap
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize() => _ = new TestContentManager();
}

internal sealed class TestContentManager : GameContentManager
{
    public TestContentManager()
    {
        // The base constructor installs Current. Named resolution still uses the real lookup.
        mFontDict.Add("sourcesansproblack", new TestFont());
    }

    private static void UnexpectedLoad() => throw new InvalidOperationException("UI measurement tests must not load real assets.");
    public override void LoadTexturePacks() => UnexpectedLoad();
    public override void LoadTilesets(string[] tilesetnames) => UnexpectedLoad();
    public override void LoadItems() => UnexpectedLoad();
    public override void LoadEntities() => UnexpectedLoad();
    public override void LoadSpells() => UnexpectedLoad();
    public override void LoadAnimations() => UnexpectedLoad();
    public override void LoadFaces() => UnexpectedLoad();
    public override void LoadImages() => UnexpectedLoad();
    public override void LoadFogs() => UnexpectedLoad();
    public override void LoadResources() => UnexpectedLoad();
    public override void LoadPaperdolls() => UnexpectedLoad();
    public override void LoadGui() => UnexpectedLoad();
    public override void LoadMisc() => UnexpectedLoad();
    public override void LoadFonts() => UnexpectedLoad();
    public override void LoadShaders() => UnexpectedLoad();
    public override void LoadSounds() => UnexpectedLoad();
    public override void LoadMusic() => UnexpectedLoad();
    protected override TAsset Load<TAsset>(Dictionary<string, IAsset> lookup, ContentType contentType,
        string assetName, Func<Stream> createStream) =>
        throw new InvalidOperationException("UI measurement tests must not load real assets.");
}
