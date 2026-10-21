namespace Intersect.Framework.Core.GameObjects.Events.Commands;
public abstract class PlayerBonusCommand : EventCommand
{
    public int Percent {get;set;}=25;
    public int DurationSeconds {get;set;}=3600;
}
public sealed class GrantExperienceBonusCommand : PlayerBonusCommand
{public override EventCommandType Type=>EventCommandType.GrantExperienceBonus;}
public sealed class GrantLootBonusCommand : PlayerBonusCommand
{public override EventCommandType Type=>EventCommandType.GrantLootBonus;}
public sealed class OpenAdventureLobbyCommand : EventCommand
{
    public override EventCommandType Type=>EventCommandType.OpenAdventureLobby;
    public bool PromptCreation {get;set;}
    public int ActivityFilter {get;set;}=-1;
}
