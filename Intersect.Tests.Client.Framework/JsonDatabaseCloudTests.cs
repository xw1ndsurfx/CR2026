using Intersect.Client.Framework.Database;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Intersect.Tests.Client.Framework;

[TestFixture]
public sealed class JsonDatabaseCloudTests
{
    private string _directory = null!;
    private string Cloud => Path.Combine(_directory, "steam-user-1", "server.json");
    private JsonDatabase Local(string name) => new(Path.Combine(_directory, name + ".json"));

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(_directory = Path.Combine(Path.GetTempPath(), "corps-cloud-tests-" + Guid.NewGuid()));

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, true);

    [Test]
    public void ExportContainsOnlyPortableSettings()
    {
        var db = Local("a");
        foreach (var key in new[] { "Username", "Password", "SteamTicket", "TargetResolution", "TargetFps", "FullScreen", "UIScale", "WorldZoom", "HudPosition:Minimap", "Unknown_binding0" })
            db.SavePreference(key, "local-only");
        db.SavePreference("LanguageCode", "fr");
        db.SavePreference("MoveUp_binding0", "binding");
        db.SavePreference("Hotkey24_binding1", "binding");
        Assert.That(db.EnableCloudPreferences(Cloud), Is.True);
        var snapshot = JObject.Parse(File.ReadAllText(Cloud));
        Assert.That(snapshot.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "LanguageCode", "MoveUp_binding0", "Hotkey24_binding1" }));
    }

    [Test]
    public void AnotherComputerRestoresSettingsButRetainsItsCredentialsAndResolution()
    {
        var first = Local("a"); first.SavePreference("LanguageCode", "fr");
        first.EnableCloudPreferences(Cloud); first.SavePreference("MusicVolume", 30);
        var second = Local("b"); second.SavePreference("Password", "device-secret"); second.SavePreference("TargetResolution", 3);
        second.EnableCloudPreferences(Cloud);
        Assert.That(second.LoadPreference("LanguageCode"), Is.EqualTo("fr"));
        Assert.That(second.LoadPreference("MusicVolume"), Is.EqualTo("30"));
        Assert.That(second.LoadPreference("Password"), Is.EqualTo("device-secret"));
        Assert.That(second.LoadPreference("TargetResolution"), Is.EqualTo("3"));
    }

    [Test]
    public void ResetAndDeletionPropagateWithoutExportingLocalData()
    {
        var first = Local("a"); first.SavePreference("MoveUp_binding0", "binding"); first.EnableCloudPreferences(Cloud);
        first.DeletePreference("MoveUp_binding0");
        var second = Local("b"); second.SavePreference("MoveUp_binding0", "old"); second.SavePreference("Username", "kept");
        second.EnableCloudPreferences(Cloud);
        Assert.That(second.HasPreference("MoveUp_binding0"), Is.False);
        Assert.That(second.LoadPreference("Username"), Is.EqualTo("kept"));
        Assert.That(File.Exists(Cloud + ".tmp"), Is.False);
    }

    [Test]
    public void CloudCannotImportCredentialsOrDeviceSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Cloud)!);
        File.WriteAllText(Cloud, "{\"Password\":\"untrusted\",\"TargetResolution\":\"99\",\"LanguageCode\":\"es\"}");
        var db = Local("a"); db.SavePreference("Password", "kept"); db.SavePreference("TargetResolution", 2);
        db.EnableCloudPreferences(Cloud);
        Assert.That(db.LoadPreference("Password"), Is.EqualTo("kept"));
        Assert.That(db.LoadPreference("TargetResolution"), Is.EqualTo("2"));
        Assert.That(db.LoadPreference("LanguageCode"), Is.EqualTo("es"));
    }

    [Test]
    public void MalformedCloudLeavesLocalSettingsAndCloudFileUntouched()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Cloud)!); File.WriteAllText(Cloud, "broken");
        var db = Local("a"); db.SavePreference("LanguageCode", "fr");
        Assert.That(db.EnableCloudPreferences(Cloud), Is.False);
        db.SavePreference("MusicVolume", 20);
        Assert.That(db.LoadPreference("LanguageCode"), Is.EqualTo("fr"));
        Assert.That(File.ReadAllText(Cloud), Is.EqualTo("broken"));
    }

    [Test]
    public void DifferentSteamAccountsHaveIndependentSnapshots()
    {
        var first = Local("a"); first.SavePreference("LanguageCode", "fr"); first.EnableCloudPreferences(Cloud);
        var secondCloud = Path.Combine(_directory, "steam-user-2", "server.json");
        var second = Local("b"); second.SavePreference("LanguageCode", "es"); second.EnableCloudPreferences(secondCloud);
        Assert.That(JObject.Parse(File.ReadAllText(Cloud))["LanguageCode"]!.Value<string>(), Is.EqualTo("fr"));
        Assert.That(JObject.Parse(File.ReadAllText(secondCloud))["LanguageCode"]!.Value<string>(), Is.EqualTo("es"));
    }
}
