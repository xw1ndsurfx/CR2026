# Royal Alchemist — images et sons

Le pack comprend le décor de l’atelier, une planche transparente de 16 ingrédients bruts et cinq effets sonores originaux : rotation, pose, fusion, recette terminée, plateau rempli.

## Configuration

Dans l’éditeur des mini-jeux, sélectionner Royal Alchemy/Potions puis **Images et sons**. Les deux sélecteurs PNG affichent un aperçu ; les cinq sélecteurs audio utilisent les WAV de resources/sounds. La liste des fichiers se rafraîchit à l’ouverture du sélecteur. Une sélection vide désactive la ressource correspondante. Les références absentes restent sélectionnées afin de ne pas perdre une configuration sur une machine sans le pack. Les choix sont un brouillon tant que « Save all changes » n’a pas été utilisé.

La configuration globale est enregistrée dans resources/reward-configuration.json côté serveur. Le serveur transmet les ressources choisies dans chaque état de session du mini-jeu (champ MessagePack ajouté à la fin, sans changer les clés existantes). Les sessions déjà ouvertes reçoivent les nouveaux choix à leur prochaine actualisation. Il faut déployer les versions compatibles du serveur, du client et de l’éditeur pour utiliser les choix personnalisés.

## Fichiers du pack

- resources/gui/royal_alchemist_background_v1.png
- resources/gui/royal_alchemist_pieces_v4.png
- resources/sounds/royal_alchemist_rotate_v1.wav
- resources/sounds/royal_alchemist_drop_v1.wav
- resources/sounds/royal_alchemist_merge_v1.wav
- resources/sounds/royal_alchemist_complete_v1.wav
- resources/sounds/royal_alchemist_game_over_v1.wav

Le décor remplit le canevas logique 1000 × 780, adapté à la fenêtre. L’interface dessine ses textes et contrôles par-dessus ; aucun texte n’est intégré à l’image. La planche utilise quatre lignes (Verdant, Ember, Arcane, Radiant) et quatre colonnes (niveaux 1 à 4), coordonnées normalisées, fond transparent et marges entre les sprites. Un remplacement doit conserver cette disposition. Les noms sont de simples noms de fichiers, sans sous-dossier. PNG pour les images ; WAV pour les sons.

Une image manquante ou corrompue utilise le dessin procédural antérieur. Une ressource audio manquante reste silencieuse. Le premier état de connexion ne rejoue pas les sons de résultat ; les révisions répétées ne les rejouent pas non plus. La pose et la rotation sont locales ; les résultats suivent les états acceptés du serveur.

Shared/RoyalAlchemistAssets.props copie le pack à côté des exécutables client et éditeur, y compris lors de la publication. Pour une distribution existante, copier les fichiers dans les mêmes sous-dossiers puis recharger les ressources ou redémarrer. Les binaires installés de Nicolas ne sont pas remplacés par ce changement ; le profil Intel HD 3000 reste séparé.

## Création et vérification

Images créées avec l’outil intégré image_gen ; prompts et correction des marges conservés dans [image-prompts.json](image-prompts.json). Les sons sont synthétisés sans échantillon tiers par tools/generate_royal_alchemist_sounds.py : mono PCM 16 bits, 44,1 kHz, marge avant saturation. Le script permet de les régénérer à l’identique.

Validation locale : 13 tests de configuration, de protocole, de localisation et de sons réussis ; 7 groupes de scénarios de gameplay réussis. Compilations client normal, serveur et éditeur réussies. Les sept fichiers copiés dans la sortie du client sont identiques aux sources. Les deux exécutables installés restent inchangés (SHA-256 vérifiés).

Tests : compatibilité de l’ancienne configuration, sélections personnalisées et vides, refus des chemins invalides, transmission MessagePack, paquet antérieur sans champ ajouté et traductions EN/FR/ES. Le test de présentation Windows vérifie les aperçus, les cinq sons, la conservation du brouillon lors de la navigation, l’absence de modification globale avant validation et les références manquantes et l’accès aux boutons de sauvegarde à taille normale et minimale. La recette manuelle dans le client de jeu reste à réaliser après déploiement.

## Révision des ingrédients — 16 novembre 2026

La planche v3 remplace les fioles par des composants bruts originaux : végétaux, poudre, pierre volcanique, corail, écailles, cristaux, coquillages, perle, hippocampe, pollen, plume, scarabée et ambre. La disposition 4 × 4 et les familles de couleurs restent identiques. Aucun changement des recettes ou des règles. La planche v2 est conservée comme ressource antérieure ; v3 devient la valeur par défaut des nouvelles configurations. Pour une configuration déjà enregistrée avec v2, choisir royal_alchemist_pieces_v3.png dans Images et sons puis enregistrer. Prompt : [ingredients-v3.prompt.json](ingredients-v3.prompt.json).

## Progression cohérente — planche v4

Chaque famille conserve le même ingrédient du niveau 1 au niveau 4. Les colonnes augmentent progressivement la taille et la complexité : feuille puis plante fleurie ; branche puis éventail de corail rouge ; éclat puis grappe de cristaux bleus ; petite plume puis grande plume dorée. Les silhouettes, matières et couleurs concordent au sein de chaque ligne. La planche v4 devient la valeur par défaut des nouvelles configurations. Pour une configuration existante, sélectionner royal_alchemist_pieces_v4.png dans Images et sons et enregistrer. Aucune modification des recettes, des sons ou des règles de fusion. Prompt : [ingredients-v4.prompt.json](ingredients-v4.prompt.json).

## Variante facultative : racines, champignons, coquillages et ambre

La planche royal_alchemist_ingredients_variant_roots_shells_v1.png propose quatre autres familles : racines médicinales vertes, champignons rouges, coquillages bleus et ambre doré. Chaque famille dispose de quatre formes cohérentes et progressives, avec des détails distincts à chaque niveau. Elle utilise la même disposition 4 × 4. La sélectionner dans Images et sons pour changer de thème. La planche v4 reste la valeur par défaut ; aucun changement de recettes ou de règles. Prompt et raffinement : [ingredients-variant-roots-shells.prompt.json](ingredients-variant-roots-shells.prompt.json).
