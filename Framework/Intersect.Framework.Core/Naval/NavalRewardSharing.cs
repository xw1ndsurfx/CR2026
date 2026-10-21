namespace Intersect.Framework.Core.Naval;

/// <summary>Largest-remainder allocation preserves the exact reward pool, including single-item lots.</summary>
public static class NavalRewardSharing
{
    public static Dictionary<Guid,long> Divide(long amount,IReadOnlyDictionary<Guid,long> weights)
    {
        var positive=weights.Where(w=>w.Value>0).OrderBy(w=>w.Key).ToArray();
        var result=positive.ToDictionary(w=>w.Key,_=>0L);
        if(amount<=0 || positive.Length==0)return result;
        var total=positive.Sum(w=>(decimal)w.Value);
        var fractions=new List<(Guid Id,decimal Fraction)>();long assigned=0;
        foreach(var entry in positive)
        {
            var exact=(decimal)entry.Value/total*amount;
            var share=(long)decimal.Floor(exact);result[entry.Key]=share;assigned+=share;
            fractions.Add((entry.Key,exact-share));
        }
        foreach(var entry in fractions.OrderByDescending(f=>f.Fraction).ThenBy(f=>f.Id).Take((int)(amount-assigned)))result[entry.Id]++;
        return result;
    }
    public static NavalCargoParcel Seal(NavalCargoParcel parcel)=>new() {Id=parcel.Id,ItemId=Guid.Empty,Quantity=0,Quality=parcel.Quality,Kind=parcel.Kind};
}
