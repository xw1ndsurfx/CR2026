using System.ComponentModel;

namespace Intersect.Config;

public sealed class LogiklikIntegrationOptions
{
    [DefaultValue("https://logiklik.com/logiklik-api")]
    public string ApiBaseUrl { get; set; } = "https://logiklik.com/logiklik-api";

    /// <summary>
    /// Shared secret used only by the Corps Royaux server when calling the Logiklik game bridge.
    /// This value is excluded from the public client options payload.
    /// </summary>
    public string GameKey { get; set; } = string.Empty;
}
