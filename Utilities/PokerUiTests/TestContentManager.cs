using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;

// The real title bar resolves a named font through GameContentManager.Current during its
// constructor. Reproduce that part of normal client startup before constructing any window.
// This fixture is compiled only into the test executable. It supplies synthetic fonts and
// empty layouts; it does not change PokerWindow, Titlebar or Text.
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

    // Code-only text measurements must not import or generate the game's disk UI layouts.
    // A cache hit with empty content represents an absent override without requesting a save.
    public override bool GetLayout(UI stage, string name, string resolution, bool skipCache,
        Action<string, bool> layoutHandler)
    {
        layoutHandler(string.Empty, true);
        return true;
    }

    protected override string GetLayout(UI stage, string name, string resolution, bool skipCache,
        out bool cacheHit)
    {
        cacheHit = true;
        return string.Empty;
    }

    public override string GetUIJson(UI stage, string name, string resolution, out bool cacheHit)
    {
        cacheHit = true;
        return string.Empty;
    }

    public override void SaveUIJson(UI stage, string name, string json, string? resolution)
    {
        // Test fixture only: never write generated layouts to the repository or user data.
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
