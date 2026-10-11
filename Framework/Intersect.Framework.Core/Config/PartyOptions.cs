namespace Intersect.Config;

public partial class PartyOptions
{
    /// <summary>
    /// Defines the maximum amount of members a party can have.
    /// </summary>
    public int MaximumMembers{ get; set; } = 5;

    public int SharedXpRange{ get; set; } = 40;

    public int NpcDeathCommonEventStartRange{ get; set; } = 0;

    /// <summary>
    /// NPC EXP bonus per other, alive, in-range party member. Only the player
    /// credited with the kill receives the full monster EXP plus this bonus.
    /// Example: with 25% per additional member and a two-player party,
    /// a 100 EXP NPC grants 125 EXP to the killer and 0 to the teammate.
    /// A solo player gets no party bonus.
    /// </summary>
    public float BonusExperiencePercentPerMember{ get; set; } = 0;
}
