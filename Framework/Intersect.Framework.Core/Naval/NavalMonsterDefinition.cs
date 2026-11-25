namespace Intersect.Framework.Core.Naval;

public enum NavalMonsterKind { Kraken, SeaSerpent, Leviathan }
public enum NavalMonsterPhase { Emerging, Assault, Retreating, Crushing, Sinking, Idle }

/// <summary>Authored ocean encounter; spawns are opt-in and never inferred from an appearance.</summary>
public sealed class NavalMonsterDefinition
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public NavalMonsterKind Kind {get;set;}
    public bool Enabled {get;set;}
    public Guid MapId {get;set;}
    public double X {get;set;}
    public double Y {get;set;}
    public double DetectionRange {get;set;}=6;
    public double DockSafeRange {get;set;}=8;
    public int RespawnSeconds {get;set;}=300;
    public int Level {get;set;}=20;
    public string Graphics {get;set;}="Naval_Kraken_v1.png";
    public Guid TentacleNpcId {get;set;}
    public int TentacleCount {get;set;}=8;
    public int TentacleHealth {get;set;}=250;
    public int TentacleDamage {get;set;}=20;
    public int AssaultSeconds {get;set;}=120;
    public int PulseSeconds {get;set;}=8;
    public int HullDamagePerTentacle {get;set;}=2;
    public Guid BossAnimationId {get;set;}
    public int BossAnimationOffsetY {get;set;}=-18;
    public Guid DeathAnimationId {get;set;}
    public int SailingExperienceReward {get;set;}=250;
    public NavalLootEntry[] Loot {get;set;}=[];

    public bool IsValid(int width,int height)=>Id!=Guid.Empty && Enum.IsDefined(Kind) && Name!=null && Name.Length<=128 &&
        (!Enabled || MapId!=Guid.Empty && TentacleNpcId!=Guid.Empty) &&
        double.IsFinite(X) && X>=0 && X<width && double.IsFinite(Y) && Y>=0 && Y<height &&
        double.IsFinite(DetectionRange) && DetectionRange is >=2 and <=20 &&
        double.IsFinite(DockSafeRange) && DockSafeRange is >=2 and <=30 && RespawnSeconds is >=10 and <=86400 &&
        Level is >=1 and <=200 && NavalPresentation.SafeAsset(Graphics) && (!Enabled || !string.IsNullOrWhiteSpace(Graphics)) &&
        TentacleCount is >=1 and <=16 && TentacleHealth is >=1 and <=100000 && TentacleDamage is >=1 and <=10000 &&
        AssaultSeconds is >=10 and <=1800 && PulseSeconds is >=1 and <=60 && HullDamagePerTentacle is >=1 and <=100 &&
        BossAnimationOffsetY is >=-512 and <=512 && SailingExperienceReward is >=0 and <=1000000 &&
        Loot!=null && Loot.Length<=32 && Loot.All(e=>e?.IsValid()==true);
}

/// <summary>Monotonic encounter clock. A fatal grasp cannot be cancelled by a late kill.</summary>
public sealed class NavalMonsterBattle
{
    public const int EmergingMilliseconds=3000,CrushingMilliseconds=4000;
    public NavalMonsterPhase Phase {get;private set;}=NavalMonsterPhase.Emerging;
    public int HullHealth {get;private set;}
    public int LiveTentacles {get;private set;}
    public long Started {get;}
    public long PhaseStarted {get;private set;}
    private readonly int _total,_duration,_pulse,_damage;
    private long _lastTime,_lastPulse;
    public NavalMonsterBattle(NavalMonsterDefinition definition,int hullHealth,long now)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if(!definition.IsValid(256,256))throw new ArgumentException("Invalid monster encounter.",nameof(definition));
        _total=definition.TentacleCount;_duration=definition.AssaultSeconds*1000;
        _pulse=definition.PulseSeconds*1000;_damage=definition.HullDamagePerTentacle;
        HullHealth=Math.Clamp(hullHealth,0,100);LiveTentacles=_total;
        Started=PhaseStarted=_lastTime=now;_lastPulse=now+EmergingMilliseconds;
    }
    // Returns newly incurred damage. Repeated snapshots at the same time never apply a second pulse.
    public int Advance(long now,int liveTentacles)
    {
        now=Math.Max(now,_lastTime);_lastTime=now;
        LiveTentacles=Math.Min(LiveTentacles,Math.Clamp(liveTentacles,0,_total));
        var before=HullHealth;
        if(Phase==NavalMonsterPhase.Crushing)
        {
            if(now-PhaseStarted>=CrushingMilliseconds){Phase=NavalMonsterPhase.Sinking;PhaseStarted+=CrushingMilliseconds;HullHealth=0;}
            return before-HullHealth;
        }
        if(Phase is NavalMonsterPhase.Retreating or NavalMonsterPhase.Sinking)return 0;
        if(now-Started<EmergingMilliseconds)
        {
            if(HullHealth==0){Phase=NavalMonsterPhase.Crushing;PhaseStarted=now;}
            else if(LiveTentacles==0){Phase=NavalMonsterPhase.Retreating;PhaseStarted=now;}
            return 0;
        }
        if(Phase==NavalMonsterPhase.Emerging){Phase=NavalMonsterPhase.Assault;PhaseStarted=Started+EmergingMilliseconds;}
        // Deadline is checked before victory: a kill reported after expiry cannot save the hull.
        if(now-PhaseStarted>=_duration || HullHealth==0){Phase=NavalMonsterPhase.Crushing;PhaseStarted=now;return 0;}
        if(LiveTentacles==0){Phase=NavalMonsterPhase.Retreating;PhaseStarted=now;return 0;}
        var pulses=(now-_lastPulse)/_pulse;
        if(pulses>0)
        {
            HullHealth=(int)Math.Max(0,HullHealth-Math.Min(100L,pulses*LiveTentacles*_damage));
            _lastPulse+=pulses*_pulse;
            if(HullHealth==0){Phase=NavalMonsterPhase.Crushing;PhaseStarted=now;}
        }
        return before-HullHealth;
    }
}
