# Vue canon : aperçu en perspective des vraies cartes

## Utilisation

Installer client, serveur et éditeur issus de la même branche. Aucun changement de base ni de naval.json n’est requis pour ouvrir cet aperçu. Les icônes de la barre ordinaire sont restaurées à la sortie du bateau comme auparavant.

1. Embarquer dans un bateau personnel sur Ocean, comme capitaine ou équipier.
2. Utiliser le troisième raccourci naval (3 avec les touches par défaut), ou cliquer son icône de viseur.
3. Choisir Bâbord ou Tribord. La caméra se place sur ce flanc, à la position précise interpolée du bateau, et regarde perpendiculairement à son cap réel. Une rotation du capitaine déplace aussi cette vue.
4. Le capitaine peut utiliser Tirer la bordée, ou les raccourcis 1/2 existants. Les équipiers disposent désormais de postes individuels décrits dans [NavalCrewGunnery.fr.md](NavalCrewGunnery.fr.md). Le bouton tire sur le flanc affiché ; les touches 1/2 gardent toujours leur propre flanc. Les rechargements sont ceux du serveur. Le viseur est un repère visuel et ne permet pas de réorienter un projectile.
5. Fermer avec la croix, Fermer ou le troisième raccourci. Débarquement, naufrage, déconnexion ou perte du snapshot ferment l’aperçu automatiquement. Les équipiers prennent un poste individuel pour tirer à la souris, sans prendre les commandes du capitaine.

## Données réellement utilisées

- Les buffers graphiques natifs des cartes déjà chargées dans la grille du joueur : mêmes tuiles, couches, découpages d’autotiles et frame d’animation que le rendu normal. Aucun panorama ou îlot fictif.
- Les positions flottantes interpolées des navires reçus du serveur, les ressources Ocean avec leur découpage vérifié, leurs animations et leurs directions relatives à l’observateur. Le bateau de l’observateur est exclu ; un équipier voit depuis le même bateau que son capitaine.
- Boulets et effets de tir/impact reçus dans les snapshots, avec les découpages explicites déjà utilisés par le jeu. Les sons existants ne sont pas rejoués une deuxième fois par l’aperçu.
- Un changement de map conserve les positions dans la grille réelle. Les cartes déchargées sont retirées ; une position indisponible efface l’image précédente.

Polonya apparaît uniquement si sa carte fait partie de cette grille, qu’elle se trouve devant le flanc choisi et dans le champ de vision. Son emplacement n’est ni déduit de son nom ni inventé. Aucune nouvelle demande réseau ne charge des cartes distantes ; aucun ennemi hors des snapshots autorisés n’est révélé.

## Limites explicites du prototype

Il s’agit d’un aperçu 2,5D : le sol réel est projeté en perspective et les navires/effets sont des images orientées vers la caméra. Les couches de décor sont actuellement projetées sur le sol, sans extrusion automatique. Les cartes Intersect ne donnent pas la hauteur des arbres, falaises ou bâtiments ; ils ne deviennent donc pas des volumes comme sur l’image de référence.

Les personnages/NPC à pied, animations d’attributs, lumières, météo et brouillards natifs ne sont pas encore projetés dans cette fenêtre. La luminosité ne reproduit pas encore le cycle jour/nuit. Les décors ne masquent pas un navire par leur hauteur ; les véritables collisions et lignes de tir continuent d’être décidées par le serveur. Les postes individuels ajoutent une visée à la souris bornée par le serveur ; les bordées du capitaine conservent leur direction fixe.

La portée graphique maximale est de 96 cases, et seules les 25 cartes chargées les plus proches sont considérées. Le fond sombre signifie géographie non rendue, sans océan inventé. La vision à longue distance de toute une île nécessite ensuite des données de terrain distantes, des niveaux de détail et une politique serveur distincte de la visibilité des ennemis.

## Performance et isolation

Image réutilisée de 480 × 270, au maximum 20 mises à jour par seconde pendant l’ouverture. Buffers de terrain natifs réutilisés, sans lecture des pixels GPU vers le processeur. L’image est libérée à la fermeture. La passe restaure cible graphique, viewport, matrices, texture, shader, découpage et états de mélange. Le rendu 2D normal reste actif derrière la fenêtre.

Les moteurs graphiques qui ne prennent pas en charge cette passe affichent un message localisé. Les erreurs prises en charge désactivent uniquement l’aperçu et produisent un avertissement dans le journal client. Les erreurs graves du périphérique graphique conservent le comportement du moteur natif.

## Test connecté à effectuer

- Devant une vraie côte : comparer sa position dans la vue classique et dans chacun des flancs. Tester les caps N/NE/E/SE/S/SW/W/NW ; la côte doit sortir du champ quand elle passe derrière la caméra.
- Placer un deuxième bateau dans la même map puis dans une voisine chargée. Observer déplacement, animation et orientation depuis un bateau tournant. Confirmer que sa coque est unique et que le bateau observateur ne se dessine pas dans son propre aperçu.
- Traverser une frontière Ocean en gardant la fenêtre ouverte : aucune ancienne île figée, pas de téléportation supplémentaire, suivi identique pour un équipier.
- Tirer des deux côtés : mêmes délais, dégâts et obstacles qu’en vue classique ; les tirs individuels des équipiers ont leur propre recharge.
- Débarquer, couler puis se reconnecter : fermeture, retour des raccourcis normaux, réouverture sans ancienne image.
- Vérifier sur Intel HD 620 : fluidité et mémoire avec fenêtre fermée/ouverte, fermeture/réouverture répétées, puis redimensionnement et plein écran.

Les tests automatisés couvrent les axes, les directions relatives, l’ancrage du bon bateau pour capitaine/équipier, les conversions de tailles de tuiles, les contrôles natifs et les traductions. Ils ne remplacent pas cette validation GPU/connectée et ne prouvent pas encore le rendu visuel sur le poste du joueur.

## Suite technique

1. Définir dans l’éditeur des objets de décor avec hauteur/ancrage et variantes latérales, sans tenter de deviner leur volume à partir des pixels.
2. Distinguer le terrain lointain statique des entités dynamiques : portée/chargement et niveaux de détail, sans étendre implicitement les droits de visibilité des ennemis.
3. Ajouter occlusion, éclairage et météo pour compléter les postes de canon d’équipier.
