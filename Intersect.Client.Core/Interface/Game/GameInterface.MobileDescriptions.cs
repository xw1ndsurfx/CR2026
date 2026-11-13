namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    internal void CloseMobileDescriptions()
    {
        _itemDescriptionWindow?.CloseMobileDescription();
        _spellDescriptionWindow?.CloseMobileDescription();
    }
}
