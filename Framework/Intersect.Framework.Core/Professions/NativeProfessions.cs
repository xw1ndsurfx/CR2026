using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Framework.Core.Professions;

public static class NativeProfessions
{
    public static bool IsNative(Guid id) => NavalProfessions.IsNative(id) || JoustProfession.IsNative(id);
    public static string Caption(Guid id, string language) => JoustProfession.IsNative(id)
        ? QualityOfLifeText.Get("Jousting", language)
        : NavalSetupText.Get(NavalProfessions.Caption(id), language);
    public static void Include(ProfessionConfiguration configuration)
    {
        NavalProfessions.Include(configuration);
        JoustProfession.Include(configuration);
    }
}
