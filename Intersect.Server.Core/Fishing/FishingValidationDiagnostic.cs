using System.Globalization;
using Intersect.Framework.Core.Fishing;

namespace Intersect.Server.Fishing;

/// <summary>Localized validation context kept on the exception, without changing the wire protocol.</summary>
internal static class FishingValidationDiagnostic
{
    private static readonly object DataKey = new();
    private sealed record Detail(string Source, object[] Arguments);

    internal static InvalidDataException Create(string source, params object[] arguments)
    {
        var error = new InvalidDataException(string.Format(CultureInfo.InvariantCulture, source, arguments));
        error.Data[DataKey] = new Detail(source, arguments);
        return error;
    }

    internal static bool TryLocalize(Exception error, string language, out string message)
    {
        if (error is InvalidDataException && error.Data[DataKey] is Detail detail)
        {
            message = string.Format(CultureInfo.InvariantCulture, FishingText.Get(detail.Source, language), detail.Arguments);
            return true;
        }
        message = "";
        return false;
    }

    internal static string Reference(string? name, Guid id) => string.IsNullOrWhiteSpace(name)
        ? id.ToString() : $"{name} ({id})";
}
