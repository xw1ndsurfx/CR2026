# Apparence et collecte des prises

L’apparence des prises vient de l’icône de l’item sélectionné dans l’éditeur de pêche (Objet poisson). Créer le saumon dans Items, lui attribuer une icône, puis le choisir dans les prises de la zone et sélectionner ses outils autorisés. Les poissons nagent avec une oscillation et une pulsation; leur image est agrandie dans l’eau. Les anciens champs Sprite restent lisibles dans le JSON pour compatibilité, mais ne remplacent plus l’icône de l’item.

Après une capture réussie, l’hameçon et la prise remontent vers la surface. La phase Retrieving dure 1,2 seconde, puis la fenêtre Loot présente Tout prendre et Tout jeter, avec une icône de poubelle. Tout prendre sauvegarde le poisson, les récompenses de niveau éventuelles et l’XP ensemble; Tout jeter ne donne aucun item ni XP. Il faut résoudre ce choix avant de relancer. Fermer la session abandonne la prise non collectée.

La pêche automatique prend le loot sans intervention après la remontée. Ses probabilités d’échec et son multiplicateur d’XP restent ceux de la zone. Si la sauvegarde échoue ou si tout le loot ne tient pas, elle s’arrête et conserve le choix de loot pour réessayer ou jeter; le multiplicateur d’XP de la capture automatique reste appliqué lors des nouvelles tentatives.

Un compte premium ayant déjà ouvert sa banque peut choisir Butin en banque, entre les prises ou dans la fenêtre de loot. Ce choix est valable pour la session de pêche. Le serveur contrôle à nouveau le premium lors de la collecte, respecte les restrictions bancaires de chaque item, la capacité et les limites de piles. Si le premium expire, la destination redevient l’inventaire. Les items, l’XP et le reçu anti-duplication sont sauvegardés ensemble; un échec annule la modification.

Les nouvelles phases et actions sont ajoutées à la fin des enums pour préserver les valeurs existantes. Déployer le client, le serveur et l’éditeur ensemble.

## Poisson, monnaie et objet rare dans la même prise

« Prise configurée » contient les prises déjà créées pour la zone, et non tous les objets du jeu. Pour ajouter un saumon, cliquer sur Nouvelle prise puis choisir le saumon sous Objet poisson. Tous les items existants sont proposés, y compris les poissons non empilables. Si un item vient d’être créé, l’enregistrer puis utiliser Actualiser les ressources. Le nom de la prise suit l’objet poisson choisi.

Sous Butin bonus, ajouter par exemple :

- Aureons : chance 100 %, quantité minimale 10 et maximale 30.
- Un équipement rare : chance 5 %, quantité minimale et maximale 1.

Le poisson principal reste garanti pour chaque capture réussie. Chaque bonus est tiré indépendamment; 0 % désactive le bonus, 100 % le garantit. Jusqu’à 16 bonus peuvent être configurés par prise. Les quantités minimales et maximales sont inclusives. Les anciennes configurations sans bonus continuent à ne donner que le poisson principal.

Le loot affiche chaque objet obtenu et sa quantité; des flèches permettent de voir les pages suivantes. Tout prendre attribue le poisson et tous les bonus ensemble; Tout jeter les abandonne ensemble. La pêche auto et la destination premium en banque s’appliquent à tout ce butin. Le tirage est conservé pendant les nouvelles tentatives de collecte, sans retirer de nouveaux bonus. Si une récompense ne tient pas, aucune récompense n’est attribuée partiellement.

## Poisson accroché et ligne visible

La bouche du poisson reste sur la pointe de l’hameçon pendant le combat et la remontée. Les marges transparentes de l’icône sont exclues du placement. Le corps se trouve à gauche de la pointe et regarde vers la droite; pour une image initialement orientée à gauche, cocher « L’icône du poisson regarde vers la gauche » dans l’éditeur de pêche. L’icône originale dans l’inventaire ne change pas. La légère animation du corps conserve le point d’attache fixe.

La ligne relie l’œillet de l’hameçon au coin supérieur gauche de la vue. Un trait clair avec contour sombre utilise des rectangles remplis, car la fonction GWEN DrawLine n’est pas implémentée dans ce rendu.
