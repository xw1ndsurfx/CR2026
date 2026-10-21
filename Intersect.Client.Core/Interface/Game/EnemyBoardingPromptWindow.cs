using Intersect.Client.Framework.Gwen.Control;
namespace Intersect.Client.Interface.Game;
internal sealed class EnemyBoardingPromptWindow(Canvas parent):Window(parent,NavalManagementWindow.T("Enemy ship disabled"),false,"EnemyBoardingPrompt")
{
    protected override void EnsureInitialized() { }
}
