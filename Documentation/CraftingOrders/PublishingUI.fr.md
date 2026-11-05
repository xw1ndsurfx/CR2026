# Commandes d'artisanat — correctif de publication

## Cause de l'onglet vide

L'ancien filtrage de `CraftingOrdersRuntime` exigeait
`ItemDescriptor.CanSellInMarketplace = true`, option désactivée par défaut.
Cela empêchait l'affichage de nombreuses recettes d'artisanat normales.

La liste se base maintenant sur les sorties des recettes existantes
(`CraftingRecipeDescriptor`) dont l'objet possède `CanTrade = true`.
Les objets de type monnaie ou sac sont exclus. La même vérification est
appliquée sur le serveur au moment de publier et d'accepter une commande.

## Nouveau parcours joueur

1. Un événement exécute **Open Crafting Orders**.
2. Dans **Publier une commande**, le joueur choisit un objet dans
   un catalogue avec recherche et défilement, où les icônes d'objets
   et la sélection active sont visibles.
3. Il indique la **quantité** (1–100), la **récompense en Aureons**
   (1–100 000 000) et le **délai** (1–720 heures).
4. Il confirme avec le bouton **Publier la commande**.
5. La réponse du serveur affiche le succès ou l'erreur. Après succès,
   l'onglet **Mes commandes** montre la nouvelle commande.

Le simple clic sur une recette ne publie rien. En absence de recette
échangeable, un message explique la configuration manquante.

## Vérifications manuelles

- Ouvrir le panneau sur client FR, EN et ES.
- Vérifier le texte, l'espacement, le contraste et l'alignement des champs.
- Ajouter des recettes artisanales avec `CanTrade = true`, sans activer
  `CanSellInMarketplace`; elles doivent maintenant apparaître.
- Vérifier l'exclusion des objets `CanTrade = false`.
- Saisir une recherche sans résultat : le message doit apparaître.
- Cliquer sur une recette : aucun paiement et aucune commande avant confirmation.
- Saisir des valeurs invalides : l'interface bloque l'envoi et explique l'erreur.
- Publier une commande : vérifier la déduction sécurisée des Aureons et sa
  présence dans **Mes commandes**.
- Avec un second joueur, accepter puis livrer la quantité demandée :
  contrôler paiement, livraison et remboursements.
