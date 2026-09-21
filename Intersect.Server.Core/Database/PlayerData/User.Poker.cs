using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.Server.Database.PlayerData;

public partial class User
{
    // Share the exact lock used by Save/SaveAsync. No second, ineffective wallet lock.
    [NotMapped, JsonIgnore]
    internal object PokerSaveGate => mSavingLock;
}
