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
