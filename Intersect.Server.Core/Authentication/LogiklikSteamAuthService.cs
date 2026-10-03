using System.Net.Http.Json;
using System.Text.Json;

namespace Intersect.Server.Authentication;

internal sealed record LogiklikSteamAuthenticationResult(
    bool Ok,
    string Username,
    bool Created,
    string Error
);

internal static class LogiklikSteamAuthService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    internal static LogiklikSteamAuthenticationResult Authenticate(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) ||
            ticket.Length > 5120 ||
            (ticket.Length & 1) != 0 ||
            !ticket.All(Uri.IsHexDigit))
        {
            return new(false, string.Empty, false, "Invalid Steam authentication ticket.");
        }

        var options = Options.Instance?.Logiklik;
        var baseUrl = (options?.ApiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        var gameKey = (options?.GameKey ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(gameKey))
        {
            return new(false, string.Empty, false, "Logiklik Steam authentication is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                baseUrl + "/auth/steam/ticket"
            );
            request.Headers.TryAddWithoutValidation("X-Logiklik-Game-Key", gameKey);
            request.Content = JsonContent.Create(new { ticket });

            using var response = Http.Send(request);
            var raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var json = string.IsNullOrWhiteSpace(raw) ? null : JsonDocument.Parse(raw);
            var root = json?.RootElement;

            var username =
                root.HasValue &&
                root.Value.TryGetProperty("corps_royaux_username", out var usernameProperty)
                    ? usernameProperty.GetString() ?? string.Empty
                    : string.Empty;

            var created =
                root.HasValue &&
                root.Value.TryGetProperty("created", out var createdProperty) &&
                createdProperty.ValueKind == JsonValueKind.True;

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(username))
            {
                var error =
                    root.HasValue &&
                    root.Value.TryGetProperty("error", out var errorProperty)
                        ? errorProperty.GetString() ?? "Steam authentication failed."
                        : "Steam authentication failed.";

                return new(false, string.Empty, false, error);
            }

            return new(true, username, created, string.Empty);
        }
        catch (Exception exception)
        {
            return new(false, string.Empty, false, exception.Message);
        }
    }
}
