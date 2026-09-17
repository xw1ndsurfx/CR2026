using System.ComponentModel;
using Intersect.Framework.Core.GameObjects.MiniGames;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

/// <summary>
/// Serializable contract for an event-launched mini-game.
/// Editor selection and server/client execution are intentionally not wired in this foundation.
/// </summary>
public partial class StartMiniGameCommand : EventCommand
{
    public override EventCommandType Type { get; } = EventCommandType.StartMiniGame;

    [DefaultValue(MiniGameType.Poker)]
    public MiniGameType Game { get; set; } = MiniGameType.Poker;

    /// <summary>
    /// Explicit, editor-assigned table identity. Never generate this during deserialization.
    /// The server must combine it with the map-instance identity when implementing table lookup.
    /// </summary>
    public Guid TableId { get; set; }

    // Include prevents IgnoreAndPopulate from replacing an omitted options object with null.
    // An explicitly supplied null remains invalid and is rejected by validation below.
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public PokerTableOptions Poker { get; set; } = new();

    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (TableId == Guid.Empty)
        {
            errors.Add("TableId must identify an explicitly configured table.");
        }

        switch (Game)
        {
            case MiniGameType.Poker:
                if (Poker is null)
                {
                    errors.Add("Poker options are required.");
                }
                else
                {
                    errors.AddRange(Poker.GetValidationErrors());
                }

                break;

            default:
                errors.Add("The mini-game type is not supported.");
                break;
        }

        return errors;
    }
}
