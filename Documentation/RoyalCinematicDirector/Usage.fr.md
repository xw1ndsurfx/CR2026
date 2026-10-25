# Royal Cinematic Director — première chaîne intégrée

Cette branche part de `main` et conserve les deux références originales dans `References/`. L’interface suit leur organisation (explorateur, vraie carte, inspecteur, timeline et commandes), avec le rendu 2D du moteur. Elle n’utilise pas l’illustration comme décor de jeu.

## Créer et tester une scène

1. Ouvrir une carte dans le Game Editor, puis **Royal Cinematic Director** dans le Hub ou le menu des éditeurs.
2. Modifier les propriétés de scène : nom, saga, chapitre, séquence et durée. Ces champs organisent le contenu ; ils ne débloquent aucune quête automatiquement.
3. Définir la case de départ du joueur. Ajouter les acteurs depuis les Events personnels de la carte. Les NPCs partagés et Events globaux ne sont pas déplacés par cette première version.
4. Ajouter une piste Mouvement pour l’acteur sélectionné. Le premier point est sa position de départ ; cliquer sur la vraie carte pour ajouter les suivants. Glisser un point pour le déplacer, clic droit pour le supprimer. La liste permet aussi Suppression et Alt+Haut/Bas pour réordonner. Les déplacements utilisent des pas cardinaux natifs, horizontal puis vertical ; le trait affiché suit ces mêmes axes.
5. Ajouter les clips Caméra : cliquer sur la carte pour choisir leur cible, puis régler début, durée, zoom et courbe. Activer Aperçu caméra pour vérifier un cadrage de référence 1280×720. Les limites natives de carte s’appliquent dans le client ; le cadrage exact peut donc différer près des bords.
6. Ajouter dialogue ou choix. Les quatre réponses et leurs événements communs sont éditables. Le sélecteur de ressources d’un clip Choix permet de choisir un événement existant puis sa réponse associée.
7. Ajouter animations/effets et sélectionner les IDs existants ; placer l’effet sur la carte. Les images d’animation sont échantillonnées avec leur découpage natif dans l’aperçu. Le client utilise la commande d’animation existante, ses boucles, sa durée et ses effets natifs.
8. Choisir musique, sons et voix parmi les fichiers existants. **Importer des fichiers audio existants** ouvre le Sound Import déjà présent. Aucun doublage IA, Inworld ou service payant n’est ajouté.
9. Déplacer les clips dans la timeline ; étirer leur bord droit pour modifier leur durée. Ctrl+molette règle le zoom temporel. Annuler/Rétablir, duplication, lecture/pause, arrêt, pas de 50 ms et déplacement du curseur sont disponibles.
10. **Enregistrer scène et carte** compile les commandes et utilise la sauvegarde réseau normale de la carte. La confirmation passe par le suivi de sauvegarde existant. La scène devient un Event local dédié, initialement en (0,0), que l’Event Editor permet de placer et configurer.
11. Dans un Event existant, ajouter **Jouer une cinématique**, puis choisir la scène de la carte chargée. La commande attend la fin de la scène. Pour une quête, placer cette commande dans son événement commun de début, de tâche ou de fin. Les conditions de la page de scène restent vérifiées par le serveur.
12. F5 sauvegarde et lance le client configuré dans l’éditeur. Il faut s’y connecter, se placer aux départs prévus puis déclencher l’Event. Cette version ne connecte pas automatiquement un compte de test et ne contourne pas les conditions du serveur.

## Interaction du joueur

Les dialogues attendent une confirmation native et les choix attendent une réponse du joueur. Pour une action jouable, utiliser un clip **Événement commun / interaction**, choisir l’événement et cocher **Autoriser le contrôle pendant l’Event**. L’événement commun peut attendre une variable/condition, un objectif ou une interaction selon les commandes historiques ; la scène reprend lorsqu’il rend la main.

Le temps affiché après un dialogue, choix, événement commun ou attente de fin de trajet reprend **après** cette barrière. Les mouvements et la caméra déjà lancés peuvent terminer pendant l’interaction. L’aperçu de création s’arrête au début des clips interactifs ; reprendre la lecture simule leur fin, sans exécuter leur gameplay. Les branches et validations réelles se testent dans le client connecté.

## Compatibilité et limites de cette étape

* Une scène conserve son source éditable dans sa page d’Event ; les commandes générées restent des commandes natives. L’Event Editor avancé permet de consulter/modifier les Move Routes. Une nouvelle sauvegarde depuis le Director régénère les commandes : les changements manuels des commandes ne sont pas réimportés dans la timeline. Les conditions, graphiques et déclencheurs natifs de la page sont conservés.
* Le joueur garde sa vitesse de gameplay native (`Speed=2` dans le Director). Les cinq vitesses du Director concernent les acteurs Event. Émettre les commandes de vitesse propres aux Events sur un joueur bloquerait les routes historiques, d’où cette restriction explicite.
* La prévisualisation de carte réutilise le moteur graphique de l’éditeur, avec une lecture GPU vers bitmap au maximum cinq fois par seconde pendant la lecture. Ce n’est pas une instance complète du client : la position du joueur est un repère, les timings de déplacement sont estimés, et les conditions choisissant les pages d’Events se vérifient en jeu. Les lumières animées et couches d’occlusion propres aux effets ne sont pas entièrement reproduites dans l’aperçu.
* Les scènes sont locales à une carte chargée. Les acteurs doivent partir des cases prévues. Les cases bloquées sont refusées à l’enregistrement ; collisions dynamiques, vitesses, délais et traversées de carte restent ceux du serveur. Un trajet bloqué finit par annuler la scène avec nettoyage, plutôt que retenir indéfiniment le joueur.
* Le cadrage est temporaire, envoyé seulement au joueur concerné et ne modifie pas son zoom enregistré. Fin, libération native, changement de carte, interruption et délais de sécurité rétablissent le cadrage. La scène ne masque pas le HUD dans cette étape.
* Les scènes ne disposent pas encore d’un système de skip, de reprise persistante de timeline, de synchronisation de groupe, d’acteurs temporaires, de formation navale/terrestre ou de cinématique de canon en perspective. Ne pas utiliser cette branche comme promesse de ces fonctions. Les choix, variables, conditions et récompenses restent gérés par les systèmes serveur existants ; aucune récompense n’est accordée par une déclaration de fin du client.
* Les modèles de production et les démonstrations « L’Héritage d’Alector » restent à construire avec les cartes, personnages et animations choisis par l’équipe. La base jouable et les ressources audio sont externes au dépôt ; les copies locales de validation ne sont pas distribuées dans cette branche. Aucun lore permanent ni nom royal définitif n’a été ajouté.

## Vérification reproductible

Exécuter `scripts/Verify-Cinematics.ps1` sur Windows avec le SDK .NET et les sous-modules initialisés. Le script compile les projets, lance les tests ciblés puis un test WinForms caché qui vérifie l’agencement et les entrées souris de la timeline/du viewport. Son éventuelle image est une **fixture synthétique d’interface**, pas une capture du jeu connecté.

Avant publication, effectuer aussi le parcours connecté décrit dans `Validation.md`. Une compilation et des tests unitaires ne remplacent pas ce test moteur/GPU/multijoueur.

Le test GPU optionnel utilise un export natif produit par `scripts/Export-CinematicMapFixture.py`, puis le mode `--native-map` de `CinematicEditorSmoke`. Il rend les tuiles et Events à partir des données de carte et des textures existantes, sans utiliser le cache d’images. Voir `Validation.md` pour les entrées et limites.
