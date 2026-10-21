using Intersect.Framework.Core.Professions;
namespace Intersect.Framework.Core.Naval;
// Reserved system identifiers. They reference native naval progression, never content items or variables.
public static class NavalProfessions
{
    public static readonly Guid Sailing=Guid.Parse("f0c30000-0000-4000-8000-000000000001");
    public static readonly Guid Gunnery=Guid.Parse("f0c30000-0000-4000-8000-000000000002");
    public static readonly Guid Shipwright=Guid.Parse("f0c30000-0000-4000-8000-000000000003");
    public static bool IsNative(Guid id)=>id==Sailing || id==Gunnery || id==Shipwright;
    public static string Caption(Guid id)=>id==Sailing?"Sailing":id==Gunnery?"Gunnery":"Shipwright";
    public static ProfessionDefinition[] Definitions()=>new[]{Sailing,Gunnery,Shipwright}.Select(id=>new ProfessionDefinition {Id=id,Name=Caption(id),Description="Naval progression"}).ToArray();
    public static void Include(ProfessionConfiguration configuration)=>configuration.Professions=configuration.Professions.Where(p=>!IsNative(p.Id)).Concat(Definitions()).ToArray();
}
