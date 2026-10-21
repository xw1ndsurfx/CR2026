using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.RoyalStylist;
namespace Intersect.Server.Networking;
internal static class PremiumConvenienceTransactions
{
    internal static string Apply(Player player,PremiumConvenienceRequestPacket packet,bool premium,Func<bool> save)
    {
        lock(player.EntityLock)
        {
            var before=player.PetsJson;var appearance=(player.Appearance??new()).SanitizedCopy();var message="";
            var profile=player.PetCollection.Convenience;
            try
            {
                if(packet.Action!=PremiumConvenienceAction.Open)
                {
                    if(!premium && packet.Action is not (PremiumConvenienceAction.DeleteOutfit or PremiumConvenienceAction.ToggleRecipeFavorite))throw new InvalidOperationException("Premium is required.");
                    switch(packet.Action)
                    {
                        case PremiumConvenienceAction.SaveOutfit:
                            if(profile.Outfits.Count>=10 || string.IsNullOrWhiteSpace(packet.Name) || packet.Name.Any(char.IsControl))throw new InvalidOperationException("Invalid saved outfit.");
                            profile.Outfits.Add(new(){Name=packet.Name.Trim(),Appearance=appearance.SanitizedCopy()});break;
                        case PremiumConvenienceAction.ApplyOutfit:
                            player.Appearance=(profile.Outfits.FirstOrDefault(outfit=>outfit.Id==packet.Id)??throw new InvalidOperationException("Saved outfit not found.")).Appearance.SanitizedCopy();break;
                        case PremiumConvenienceAction.DeleteOutfit:
                            profile.Outfits.RemoveAll(outfit=>outfit.Id==packet.Id);break;
                        case PremiumConvenienceAction.ToggleRecipeFavorite:
                            if(!profile.FavoriteRecipes.Remove(packet.Id))
                            {
                                if(!premium)throw new InvalidOperationException("Premium is required.");
                                if(profile.FavoriteRecipes.Count>=100 || CraftingRecipeDescriptor.Get(packet.Id)==null)throw new InvalidOperationException("Recipe unavailable.");
                                profile.FavoriteRecipes.Add(packet.Id);
                            }
                            break;
                        case PremiumConvenienceAction.PetDecoration:
                            if(!player.PetCollection.OwnedPets.TryGetValue(packet.PetId,out var pet) || packet.Id!=Guid.Empty && !RoyalStylistRuntime.Current.PetDecorations.Any(decoration=>decoration.Id==packet.Id))throw new InvalidOperationException("Pet accessory unavailable.");
                            pet.DecorationId=packet.Id;break;
                    }
                    if(!save())throw new IOException();
                    message="Saved.";
                }
            }
            catch(Exception exception)
            {
                player.PetsJson=before;player.Appearance=appearance;
                message=exception is InvalidOperationException?exception.Message:"Changes could not be saved.";
            }
            return message;
        }
    }
}
