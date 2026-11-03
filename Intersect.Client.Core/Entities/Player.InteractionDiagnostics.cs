using Intersect.Client.Core;
using Intersect.Client.Core.Controls;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;

namespace Intersect.Client.Entities;

public partial class Player
{
    private void TraceInteractionPress()
    {
        // Observe the native binding before busy/naval checks. This query only reads
        // current/previous input states; held controls do not emit a line every frame.
        if (!Controls.IsControlJustPressed(Control.AttackInteract)) return;

        ClientLoopDiagnostics.Note(
            $"interaction-press mapId={MapId}; x={X}; y={Y}; facing={DirectionFacing}; " +
            $"busy={IsBusy}; pendingDeath={PendingDeath}; eventHolds={Globals.EventHolds.Count}; " +
            $"moveRoute={Globals.MoveRouteActive}; shop={Globals.GameShop != null}; " +
            $"bank={Globals.InBank}; craft={Globals.InCraft}; trade={Globals.InTrade}; " +
            $"inputFocus={Interface.Interface.HasInputFocus()}; aboard={Naval.NavalWorld.IsAboard(Id)}; " +
            $"moving={IsMoving}; attacking={IsAttacking}; blocking={IsBlocking}; casting={IsCasting}; " +
            $"combatMovement={Options.Instance.Player.AllowCombatMovement}");
    }
}
