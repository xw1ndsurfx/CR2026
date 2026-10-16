using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.Housing;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public partial class Player
{
    [JsonIgnore, Column("Housing")]
    public string HousingJson { get => JsonConvert.SerializeObject(House); set => House = HouseProfile.Read(value); }
    [JsonIgnore, NotMapped] public HouseProfile House { get; set; } = new();
    public void HousePet(Guid id, bool housed)
    {
        if (!PetCollection.OwnedPets.TryGetValue(id, out var pet)) return;
        pet.Housed = housed;
        if (housed && PetCollection.ActivePetId == id) { PetCollection.ActivePetId = Guid.Empty; DismissPetRuntime(); }
        SendPetState();
    }
}
