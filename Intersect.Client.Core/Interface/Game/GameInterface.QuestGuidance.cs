namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private QuestGuidanceManager? _questGuidance;

    private void UpdateQuestGuidance() =>
        (_questGuidance ??= new QuestGuidanceManager(GameCanvas)).Update();

    private void DisposeQuestGuidance()
    {
        _questGuidance?.Clear();
        _questGuidance = null;
    }
}
