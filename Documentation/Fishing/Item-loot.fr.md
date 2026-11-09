# Apparence et collecte des prises

L’apparence des prises vient de l’icône de l’item sélectionné dans l’éditeur de pêche (Reward item). Créer le saumon dans Items, lui attribuer une icône, puis le choisir dans les prises de la zone et sélectionner ses outils autorisés. Les poissons nagent avec une oscillation et une pulsation; leur image est agrandie dans l’eau. Les anciens champs Sprite restent lisibles dans le JSON pour compatibilité, mais ne remplacent plus l’icône de l’item.

Après une capture réussie, l’hameçon et la prise remontent vers la surface. La phase Retrieving dure 1,2 seconde, puis la fenêtre Loot présente Tout prendre et Tout jeter, avec une icône de poubelle. Tout prendre sauvegarde le poisson, les récompenses de niveau éventuelles et l’XP ensemble; Tout jeter ne donne aucun item ni XP. Il faut résoudre ce choix avant de relancer. Fermer la session abandonne la prise non collectée.

La pêche automatique prend le loot sans intervention après la remontée. Ses probabilités d’échec et son multiplicateur d’XP restent ceux de la zone. Si la sauvegarde échoue ou si tout le loot ne tient pas, elle s’arrête et conserve le choix de loot pour réessayer ou jeter; le multiplicateur d’XP de la capture automatique reste appliqué lors des nouvelles tentatives.

Un compte premium ayant déjà ouvert sa banque peut choisir Butin en banque, entre les prises ou dans la fenêtre de loot. Ce choix est valable pour la session de pêche. Le serveur contrôle à nouveau le premium lors de la collecte, respecte les restrictions bancaires de chaque item, la capacité et les limites de piles. Si le premium expire, la destination redevient l’inventaire. Les items, l’XP et le reçu anti-duplication sont sauvegardés ensemble; un échec annule la modification.

Les nouvelles phases et actions sont ajoutées à la fin des enums pour préserver les valeurs existantes. Déployer le client, le serveur et l’éditeur ensemble.
