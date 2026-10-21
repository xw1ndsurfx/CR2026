namespace Intersect.Framework.Core.Naval;

public enum NavalPatrolDifficulty : byte { Normal, Elite, Boss }

/// <summary>Editor-only generation of editable values, calibrated to the configured naval combat curve.</summary>
public static class NavalPatrolAutoBalance
{
    public static void Apply(HerboniaPatrol patrol,int level,NavalPatrolDifficulty difficulty,NavalConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(patrol);
        ArgumentNullException.ThrowIfNull(settings);
        if(level is <1 or >200 || !Enum.IsDefined(difficulty) || patrol.Ship==null || settings.Sailing?.IsValid()!=true ||
            settings.CannonDamage is <1 or >100 || settings.CannonReloadMilliseconds is <1000 or >60000)
            throw new ArgumentOutOfRangeException(nameof(level));
        var progress=(level-1)/199d;
        var healthFactor=difficulty switch {NavalPatrolDifficulty.Elite=>1.8,NavalPatrolDifficulty.Boss=>3.5,_=>1};
        var damageFactor=difficulty switch {NavalPatrolDifficulty.Elite=>1.3,NavalPatrolDifficulty.Boss=>1.65,_=>1};
        var rewardFraction=difficulty switch {NavalPatrolDifficulty.Elite=>.45,NavalPatrolDifficulty.Boss=>1,_=>.2};
        // Three broadside shots. At each level use the best unlocked damage/reload ratio,
        // with standard ammunition as the baseline even if the ammunition list was customized.
        var ammoRatio=Math.Max(1,(settings.Ammunition??[]).Where(a=>a!=null && a.IsValid() && a.RequiredSailingLevel<=level)
            .Select(a=>a.DamagePercent/(double)a.ReloadPercent).DefaultIfEmpty(1).Max());
        var playerDps=3d*settings.CannonDamage*ammoRatio/(settings.CannonReloadMilliseconds/1000d);
        var fightSeconds=14+46*progress;
        patrol.Level=level;
        patrol.MaximumHealth=(int)Math.Clamp(Math.Round(playerDps*fightSeconds*healthFactor),1,100000);
        // Owned hulls have 100 HP: normal enemies become more dangerous gradually,
        // rather than a high-level broadside immediately sinking every player ship.
        patrol.CannonDamage=(int)Math.Clamp(Math.Ceiling(100*(settings.CannonReloadMilliseconds/1000d)/(3*(70-35*progress))*damageFactor),1,100);
        patrol.SailingExperienceReward=(int)Math.Clamp(Math.Round(settings.Sailing.Required(level)*rewardFraction),1,1000000000);
        patrol.DetectionRange=Math.Round(10+8*progress,2);
        patrol.LeashRadius=Math.Round(24+16*progress,2);
        patrol.PatrolRadius=Math.Round(4+4*progress,2);
        patrol.DockSafeRange=6;
        patrol.RespawnSeconds=(int)Math.Round((45+level*.5)*(difficulty==NavalPatrolDifficulty.Boss?2.5:difficulty==NavalPatrolDifficulty.Elite?1.5:1));
        var movement=difficulty==NavalPatrolDifficulty.Boss?.85:1;
        patrol.Ship.MaxSpeed=Math.Round((2.2+1.8*progress)*movement,2);
        patrol.Ship.ReverseSpeed=Math.Round(patrol.Ship.MaxSpeed*.2,2);
        patrol.Ship.Acceleration=Math.Round(.7+.65*progress,2);
        patrol.Ship.Drag=.5;
        patrol.Ship.TurnRate=Math.Round((30+25*progress)*movement,2);
    }
}
