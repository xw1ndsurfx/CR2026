namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public enum ProfessionModification
{
    Learn = 0,
    Forget = 1,
    AddExperience = 2,
    SetExperience = 3,
    SetLevel = 4,
}

public partial class ModifyProfessionCommand : EventCommand
{
    public override EventCommandType Type { get; } = EventCommandType.ModifyProfession;
    public Guid ProfessionId { get; set; }
    public ProfessionModification Action { get; set; }
    public long Value { get; set; }
}
