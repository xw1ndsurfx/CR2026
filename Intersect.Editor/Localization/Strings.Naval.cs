using System.Globalization;
using Intersect.Framework.Core.Naval;
using Intersect.Localization;

namespace Intersect.Editor.Localization;

public static partial class Strings
{
    public static class Naval
    {
        private static LocalizedString Text(string source) => new(NavalSetupText.Get(source, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
        public static LocalizedString Title = Text(NavalSetupText.Title);
        public static LocalizedString Prepare = Text(NavalSetupText.Menu);
        public static LocalizedString Created = Text(NavalSetupText.Created);
        public static LocalizedString Existing = Text(NavalSetupText.Existing);
        public static LocalizedString NotOcean = Text(NavalSetupText.NotOcean);
        public static LocalizedString NoSpace = Text(NavalSetupText.NoSpace);
        public static LocalizedString ConfigurationError = Text(NavalSetupText.ConfigurationError);
        public static LocalizedString ShipLimit = Text(NavalSetupText.ShipLimit);
        public static LocalizedString SaveFirst = Text(NavalSetupText.SaveFirst);
    }
}
