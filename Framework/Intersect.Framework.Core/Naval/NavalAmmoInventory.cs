namespace Intersect.Framework.Core.Naval;

public enum NavalAmmoUnlockRule : byte { GunneryLevel, CompletedQuest, GunneryLevelOrQuest }
public sealed class NavalAmmoInventorySettings
{
    public int BaseCapacity {get;set;}=1000;
    public int CapacityPerUpgrade {get;set;}=1000;
    public int MaximumUpgrades {get;set;}=5;
    public int PurchaseLimit {get;set;}=10000;
    public int UpgradePrice {get;set;}=1000;
    public Guid UpgradeCurrencyItemId {get;set;}
    public int RequiredGunneryLevel {get;set;}=5;
    public int GunneryLevelsPerUpgrade {get;set;}=5;
    public Guid UnlockQuestId {get;set;}
    public NavalAmmoUnlockRule UnlockRule {get;set;}=NavalAmmoUnlockRule.GunneryLevelOrQuest;
    public int Capacity(int tier)=>BaseCapacity+CapacityPerUpgrade*Math.Clamp(tier,0,MaximumUpgrades);
    public int LevelRequired(int tier)=>Math.Min(200,RequiredGunneryLevel+Math.Clamp(tier,0,MaximumUpgrades)*GunneryLevelsPerUpgrade);
    public long Price(int tier)=>(long)UpgradePrice*(Math.Clamp(tier,0,MaximumUpgrades)+1);
    public bool Unlocked(int tier,int level,bool questCompleted)=>UnlockRule switch
    {
        NavalAmmoUnlockRule.CompletedQuest=>UnlockQuestId!=Guid.Empty && questCompleted,
        NavalAmmoUnlockRule.GunneryLevel=>level>=LevelRequired(tier),
        _=>level>=LevelRequired(tier) || UnlockQuestId!=Guid.Empty && questCompleted,
    };
    public static long Used(NavalCareerData career)=>career.Ammo.Sum(a=>(long)a.Quantity);
    public int MaximumPurchase(NavalCareerData career,long balance,int unitPrice)=>unitPrice<0?0:
        (int)Math.Max(0,Math.Min(Math.Min(PurchaseLimit,(long)Capacity(career.AmmoUpgradeLevel)-Used(career)),
            unitPrice==0?int.MaxValue:balance/unitPrice));
    public bool IsValid()=>BaseCapacity is >=1 and <=1000000 && CapacityPerUpgrade is >=1 and <=1000000 && MaximumUpgrades is >=0 and <=20 &&
        (long)BaseCapacity+(long)CapacityPerUpgrade*MaximumUpgrades<=1000000 && PurchaseLimit is >=1 and <=100000 && UpgradePrice is >=0 and <=1000000000 &&
        RequiredGunneryLevel is >=1 and <=200 && GunneryLevelsPerUpgrade is >=0 and <=200 && Enum.IsDefined(UnlockRule) &&
        (UnlockRule!=NavalAmmoUnlockRule.CompletedQuest || UnlockQuestId!=Guid.Empty);
}
public sealed class NavalAmmoUpgradeOffer
{
    public int CurrentTier {get;set;}
    public int NextCapacity {get;set;}
    public int RequiredLevel {get;set;}
    public string QuestName {get;set;}="";
    public NavalAmmoUnlockRule UnlockRule {get;set;}
    public long Price {get;set;}
    public string Currency {get;set;}="";
    public bool Unlocked {get;set;}
    public bool CanBuy {get;set;}
}
