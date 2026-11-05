namespace Intersect.Framework.Core.CraftingOrders;

/// <summary>FR/EN/ES commission board strings kept together for locale coverage.</summary>
public static class CraftingOrdersText
{
    private static readonly Dictionary<string, string[]> Entries = new(StringComparer.Ordinal)
    {
        ["Open Crafting Orders"] = ["Open Crafting Orders", "Ouvrir les commandes d'artisanat", "Abrir encargos de artesanía"],
        ["Crafting Orders"] = ["Crafting Orders", "Commandes d'artisanat", "Encargos de artesanía"],
        ["Available Orders"] = ["Available Orders", "Commandes disponibles", "Encargos disponibles"],
        ["My Orders"] = ["My Orders", "Mes commandes", "Mis encargos"],
        ["Post an Order"] = ["Post an Order", "Publier une commande", "Publicar un encargo"],
        ["Refresh"] = ["Refresh", "Actualiser", "Actualizar"],
        ["Aureons"] = ["Aureons", "Aureons", "Aureons"],
        ["Quantity"] = ["Quantity", "Quantité", "Cantidad"],
        ["Reward (Aureons)"] = ["Reward (Aureons)", "Rémunération (Aureons)", "Pago (Aureons)"],
        ["Hours"] = ["Hours", "Heures", "Horas"],
        ["Choose a craftable recipe output to request."] = ["Choose a craftable recipe output to request.", "Choisissez un objet fabriquable à commander.", "Elige un objeto fabricable para encargar."],
        ["Commissions are backed by secure Aureons escrow."] = ["Commissions are backed by secure Aureons escrow.", "Les Aureons sont conservés en dépôt sécurisé.", "Los Aureons se guardan en depósito seguro."],
        ["Accept Order"] = ["Accept Order", "Accepter la commande", "Aceptar encargo"],
        ["Deliver Crafted Items"] = ["Deliver Crafted Items", "Livrer les objets fabriqués", "Entregar objetos fabricados"],
        ["Cancel Order"] = ["Cancel Order", "Annuler la commande", "Cancelar encargo"],
        ["No crafting orders to show."] = ["No crafting orders to show.", "Aucune commande à afficher.", "No hay encargos disponibles."],
        ["Enter valid order terms."] = ["Enter valid order terms.", "Saisissez des conditions valides.", "Introduce condiciones válidas."],

        ["CraftingOrders.Subtitle"] = ["Publish a commission or craft an item for another player.", "Publiez une commande ou fabriquez un objet pour un autre joueur.", "Publica un encargo o fabrica un objeto para otro jugador."],
        ["CraftingOrders.MyCrafting"] = ["My crafting", "Mes fabrications", "Mis fabricaciones"],
        ["CraftingOrders.Filters"] = ["Filters", "Filtres", "Filtros"],
        ["CraftingOrders.SearchOrders"] = ["Search items or players", "Rechercher un objet ou joueur", "Buscar objetos o jugadores"],
        ["CraftingOrders.UrgentOff"] = ["Urgent orders: off", "Commandes urgentes : non", "Encargos urgentes: no"],
        ["CraftingOrders.UrgentOn"] = ["Urgent orders: on", "Commandes urgentes : oui", "Encargos urgentes: sí"],
        ["CraftingOrders.Commissions"] = ["Available commissions", "Commandes d'artisanat", "Encargos de artesanía"],
        ["CraftingOrders.Count"] = ["{0} orders", "{0} commandes", "{0} encargos"],
        ["CraftingOrders.OrderDetails"] = ["Order details", "Détails de la commande", "Detalles del encargo"],
        ["CraftingOrders.SelectOrder"] = ["Select an order", "Sélectionnez une commande", "Selecciona un encargo"],
        ["CraftingOrders.Customer"] = ["Requested by: {0}", "Demandée par : {0}", "Solicitado por: {0}"],
        ["CraftingOrders.Reward"] = ["Reward: {0:N0} Aureons", "Rémunération : {0:N0} Aureons", "Recompensa: {0:N0} Aureons"],
        ["CraftingOrders.Due"] = ["Deadline: {0}", "Délai : {0}", "Plazo: {0}"],
        ["CraftingOrders.CoinAmount"] = ["{0:N0} Aureons", "{0:N0} Aureons", "{0:N0} Aureons"],
        ["CraftingOrders.SelectRecipe"] = ["1. Choose an item", "1. Choisir un objet", "1. Elegir un objeto"],
        ["CraftingOrders.Terms"] = ["2. Order details", "2. Détails de la commande", "2. Detalles del encargo"],
        ["CraftingOrders.Search"] = ["Search recipes", "Rechercher une recette", "Buscar recetas"],
        ["CraftingOrders.RecipesCount"] = ["{0} / {1}", "{0} / {1}", "{0} / {1}"],
        ["CraftingOrders.NoEligibleRecipes"] = ["No tradeable crafting recipes are available. Configure a recipe in the editor, then refresh.", "Aucune recette échangeable disponible. Configurez une recette dans l'éditeur, puis actualisez.", "No hay recetas comerciables disponibles. Configura una receta en el editor y actualiza."],
        ["CraftingOrders.NoSearchMatches"] = ["No recipes match your search.", "Aucune recette ne correspond à votre recherche.", "Ninguna receta coincide con la búsqueda."],
        ["CraftingOrders.SelectItem"] = ["Select an item from the list", "Sélectionnez un objet dans la liste", "Selecciona un objeto de la lista"],
        ["CraftingOrders.SelectBeforePublishing"] = ["Choose a craftable item before publishing.", "Choisissez un objet à fabriquer avant de publier.", "Elige un objeto fabricable antes de publicar."],
        ["CraftingOrders.DeadlineHours"] = ["Deadline (hours)", "Délai (heures)", "Plazo (horas)"],
        ["CraftingOrders.EscrowExplanation"] = ["The reward is reserved now and paid to the crafter after delivery.", "Les Aureons seront réservés et versés à l'artisan après livraison.", "Los Aureons se reservan y se pagan al artesano tras la entrega."],
        ["CraftingOrders.Summary"] = ["{0} × {1} · {2} Aureons · {3} h", "{0} × {1} · {2} Aureons · {3} h", "{0} × {1} · {2} Aureons · {3} h"],
        ["CraftingOrders.PublishOrder"] = ["Publish the order", "Publier la commande", "Publicar el encargo"],
        ["CraftingOrders.Sending"] = ["Sending the order…", "Envoi de la commande…", "Enviando el encargo…"],
        ["CraftingOrders.Posted"] = ["Order posted. Reward held in escrow.", "Commande publiée. Rémunération mise en dépôt.", "Encargo publicado. Pago depositado."],
        ["CraftingOrders.Accepted"] = ["Order accepted. Craft the requested item and deliver it here.", "Commande acceptée. Fabriquez l'objet puis livrez-le ici.", "Encargo aceptado. Fabrica el objeto y entrégalo aquí."],
        ["CraftingOrders.Delivered"] = ["Delivered. Payment credited to the crafter.", "Livraison effectuée. Paiement crédité à l'artisan.", "Entrega realizada. Pago abonado al artesano."],
        ["CraftingOrders.Cancelled"] = ["Order cancelled and refunded.", "Commande annulée et remboursée.", "Encargo cancelado y reembolsado."],
        ["CraftingOrders.Unavailable"] = ["Order unavailable.", "Commande indisponible.", "Encargo no disponible."],
        ["CraftingOrders.MissingItems"] = ["Not enough crafted items in inventory.", "Objets insuffisants dans l'inventaire.", "No hay suficientes objetos en el inventario."],
        ["CraftingOrders.InvalidTerms"] = ["Invalid terms (1–100 items; 1–720 hours).", "Conditions invalides (1 à 100 objets; 1 à 720 heures).", "Condiciones inválidas (1 a 100 objetos; 1 a 720 horas)."],
        ["CraftingOrders.InvalidRecipe"] = ["No eligible crafting recipe for this item.", "Aucune recette d'artisanat admissible pour cet objet.", "No hay receta de artesanía válida para este objeto."],
        ["CraftingOrders.MissingCurrency"] = ["Aureons currency is not configured.", "La monnaie Aureons n'est pas configurée.", "La moneda Aureons no está configurada."],
        ["CraftingOrders.InsufficientFunds"] = ["Not enough Aureons.", "Aureons insuffisants.", "Aureons insuficientes."],
        ["CraftingOrders.Status.0"] = ["Available", "Disponible", "Disponible"],
        ["CraftingOrders.Status.1"] = ["In progress", "En fabrication", "En fabricación"],
        ["CraftingOrders.Status.2"] = ["Delivered", "Livrée", "Entregado"],
        ["CraftingOrders.Status.3"] = ["Cancelled", "Annulée", "Cancelado"],
        ["CraftingOrders.Status.4"] = ["Expired / refunded", "Expirée / remboursée", "Caducado / reembolsado"],
    };
    public static string Get(string key, string? locale)
    {
        if (!Entries.TryGetValue(key, out var values)) return key;
        return values[locale?.ToLowerInvariant() switch { "fr" => 1, "es" => 2, _ => 0 }];
    }
}
