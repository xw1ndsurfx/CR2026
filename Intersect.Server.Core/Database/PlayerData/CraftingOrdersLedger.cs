using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intersect.Server.Database.PlayerData;

// Escrow and the affected account are committed in the same player-database transaction.
[Table("CraftingOrdersLedger")]
public sealed class CraftingOrdersLedger
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; } = 1;
    public string State { get; set; } = "{}";
    [ConcurrencyCheck] public long Revision { get; set; }
}
