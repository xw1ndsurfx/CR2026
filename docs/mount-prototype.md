# Prototype de montures

## Essayer dans le jeu

1. Compiler et mettre à jour le serveur, le client et l’éditeur avec cette branche.
2. Copier les quatre spritesheets du cheval dans les ressources du **client et de l’éditeur**, sous `resources/images/mounts/prototype_horse/` :
   - `Farm_Horse_Adult_Idle.png`
   - `Farm_Horse_Adult_Move_1_Start.png`
   - `Farm_Horse_Adult_Move_2_Loop.png`
   - `Farm_Horse_Adult_Move_3_End.png`
3. Redémarrer le client après l’ajout d’images : elles sont chargées avec les autres images du jeu.
4. Ouvrir **Game Editors → Mount Editor**. Le cheval d’exemple est disponible par défaut si le serveur n’a pas encore de `resources/mounts.json`.
5. Sauvegarder la configuration. Les réponses du serveur confirment la sauvegarde ; les erreurs conservent le brouillon.
6. Dans le jeu, ouvrir le personnage puis **Montures**, choisir le cheval et cliquer **Monter**.
7. Définir une favorite, puis utiliser **H** pour monter/descendre. Ce raccourci se reconfigure dans les commandes.

La vitesse d’exemple est ×1,35. Le serveur valide la propriété et les restrictions ; le client applique le même multiplicateur pour sa prédiction.

## Voir son personnage et ses paperdolls

Le moteur conserve la boucle de rendu existante : sprite, apparence cosmétique, équipements masculins/féminins, teintes et ordre par direction. Il applique une transformation commune à toutes ces couches. Les couches ne sont pas remplacées par une image de cavalier.

L’animal est dessiné avant le personnage. Une partie de l’animal repasse ensuite devant le cavalier. Le masquage facultatif du bas des jambes utilise une limite commune au corps et aux paperdolls. Le visage, les vêtements et les accessoires sont donc conservés, mais les parties cachées par l’animal ou le masque ne restent pas visibles.

Il s’agit d’une simulation de posture avec les dessins actuels, pas d’une nouvelle pose assise. Selon la taille de l’animal et du personnage, il faudra ajuster la selle, l’échelle et le masque.

## Réglages dans l’éditeur

- Catalogue de plusieurs montures : nouvelle entrée, duplication indépendante et suppression.
- Quatre images distinctes et nombres de frames indépendants : repos, départ, boucle, arrêt.
- Import de spritesheets PNG : reconnaissance des noms Idle/Start/Loop/End et détection de cellules carrées réparties sur quatre lignes. Les dimensions restent modifiables.
- Sélecteurs d’images avec recherche, via le sélecteur commun de l’éditeur.
- Point au sol, échelle de l’animal, échelle du cavalier, position relative des hanches et oscillation.
- Selle, avant-plan et masquage des jambes indépendants pour bas/gauche/droite/haut.
- Corrections X/Y par direction, animation et frame. Désactiver « Aperçu en mouvement » pour régler exactement la frame choisie.
- Aperçu avec chargement d’une spritesheet de joueur à quatre colonnes et quatre directions, puis de ses paperdolls. Les couches de cet aperçu suivent l’ordre de sélection ; en jeu, l’ordre réel de l’équipement est utilisé.
- L’import JSON fusionne les entrées par identifiant et conserve les autres montures du brouillon.
- Le mode plein écran F11/Échap et l’ouverture au-dessus de la fenêtre parente utilisent le comportement commun de l’éditeur.

## Déblocage et sauvegarde

« Débloquée pour tous » sert notamment à tester les montures sans modifier les comptes.

Pour une acquisition individuelle, choisir un **objet de déblocage réutilisable**. L’utilisation de cet objet dans l’inventaire débloque la ou les montures qui le référencent, sans le consommer. Il peut être distribué par les boutiques ou les récompenses de quêtes existantes. Employer un objet dédié à cet usage : sa logique de monture prend la priorité sur ses autres effets d’utilisation.

La collection et la favorite sont stockées dans une nouvelle section du JSON existant des familiers. Aucune migration de base n’est nécessaire et les données des familiers sont conservées. La monture active n’est pas sauvegardée : une reconnexion commence à pied. Les échecs de sauvegarde annulent les changements de collection/favorite.

## Restrictions du prototype

- Descente lors d’une attaque, d’un lancement de sort/blocage, d’une entrée en combat ou d’un décès.
- Montures interdites dans les instances, donjons et à bord des navires.
- Intérieurs interdits, sauf autorisation explicite par monture.
- Les collisions restent celles du personnage sur sa case : la taille dessinée de l’animal n’ajoute pas une empreinte de plusieurs cases.
- Pas de combat monté, élevage, endurance, selles équipables ni passager dans cette version.
- Les PNG restent des fichiers de ressources locaux ; ils ne sont pas transmis par les paquets du serveur.
- Les changements de configuration actualisent les joueurs connectés, y compris leur apparence montée.
- Le premier exemple est activé et accessible à tous : décocher l’activation globale ou l’accès public pour un déploiement hors prototype.

## Aperçu autonome

Le fichier local `mount-prototype.html` permet d’essayer le cheval sans lancer le jeu. Charger son sprite et ses paperdolls dans le panneau de droite. Flèches/ZQSD/WASD déplacent le personnage ; H monte ou descend. Le fichier n’envoie pas les images à un service externe.

Les réglages JSON exportés sont compatibles avec l’import du Mount Editor. Importer aussi les PNG correspondants dans l’éditeur. Cet aperçu est un outil de calibration ; il ne simule pas le serveur, les collisions complètes ou l’inventaire.

Les spritesheets d’exemple fournies pour ce prototype restent dans le pack local ; la PR contient le code, les tests et cette documentation.

## Vérification

Les tests dédiés couvrent les transitions d’animation, l’alignement commun du corps et des paperdolls, les couches de tailles différentes, le masque des jambes, le rendu à pied inchangé, les actions verrouillées, les restrictions serveur, la persistance, le retour arrière sur erreur, l’import d’images, les corrections indépendantes et les traductions en/fr/es.
