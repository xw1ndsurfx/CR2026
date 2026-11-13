namespace Intersect.Server.Entities;

public partial class Player
{
    public bool IsDialogueProtected => EventLookup?.Values.Any(instance => instance.IsDialogueProtected) == true;
}
