using Intersect.Localization;

namespace Intersect.Framework.Core.Naval;

public static class NavalSetupText
{
    public const string Title = "Naval setup";
    public const string Menu = "Prepare a test ship automatically";
    public const string Created = "Ship ready on {0}. Ship: ({1}, {2}); boarding point: ({3}, {4}). Navigation is enabled. Use /naval near the boarding point.";
    public const string Existing = "Existing ship on {0} kept. Boarding point: ({1}, {2}). Navigation is enabled.";
    public const string NotOcean = "Select a saved map inside the Ocean folder or one of its subfolders.";
    public const string NoSpace = "No free area was found for the full hull and a boarding point. Check the map collision attributes and existing obstacles.";
    public const string ConfigurationError = "The naval configuration could not be read or saved. Existing data was preserved.";
    public const string ShipLimit = "The naval configuration already contains the maximum of 64 ships.";
    public const string SaveFirst = "Save the selected map before preparing a ship. Placement uses the collision data saved on the server.";

    public static GameTextCatalog Catalog { get; } = Load();
    private static GameTextCatalog Load()
    {
        using var stream = typeof(NavalSetupText).Assembly.GetManifestResourceStream("Intersect.Naval.SetupText")!;
        using var reader = new StreamReader(stream);
        return GameTextCatalog.FromJson(reader.ReadToEnd());
    }
    public static string Get(string source, string language) => Catalog.Get(source, language);
}
