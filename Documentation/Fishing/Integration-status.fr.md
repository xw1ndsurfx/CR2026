# Métier Pêche — état de l’intégration

## Livraison

La première version du métier a été intégrée à `main` par la [PR #218](https://github.com/xw1ndsurfx/CR2026/pull/218), au commit `210114fd695fa937ea33b92037e38cb9daef272b`. Le correctif `fix/fishing-editor-zones` part de cette version et traite l’accès depuis le hub, les zones à plusieurs cartes et la sauvegarde d’un travail incomplet. Le [guide de mise en route](README.fr.md) explique les liaisons de contenu à effectuer. Le [mandat](Work-Mandate.fr.md) conserve les demandes et les critères de recette.

La fenêtre du mini-jeu reprend le cadre bois et dorures, la vue sous-marine, les équipements, les piles de prises, les barres et les quatre compétences de la référence approuvée. Elle ne contient ni avatar ni quai. Dans le monde, le système de présentation dessine les couches de l’outil autour de l’apparence réelle du joueur, avec réplication aux joueurs proches.

## Fonctions raccordées

| Partie | Comportement intégré |
| --- | --- |
| Event | Commande **Ouvrir la pêche**, avec sélection d’une zone appliquée au jeu. L’Event serveur crée la session. |
| Équipement | Un objet de pêche doit être réellement équipé. Les familles Rod, Cage et Net regroupent des profils liés aux GUID des objets. |
| Zones et prises | Plusieurs cartes par zone ; cartes entières par défaut, rayon optionnel. Compatibilité zone–outil, niveau, profondeur, plage UTC, variable joueur, poids de tirage, quantité, difficulté et XP. |
| Pêche active | Maintenir/relâcher la remontée, surveiller la résistance, la tension et l’état de la ligne. |
| Compétences | Stabiliser, Traction, Réparer et Plonger ; conditions, effets et recharges contrôlés par le serveur. |
| Pêche automatique | Tentatives successives pendant la connexion, contrôles d’appâts et de capacité, interruption sur perte d’admissibilité. |
| Légendaires | Rencontre rare indépendante des poids ordinaires, invitation puis lutte manuelle et séquence de directions chronométrée. |
| Inventaire | Remplissage des piles natives, limites de l’objet, récompense et XP enregistrées avec un reçu anti-rejeu. |
| Animation | Profil de filet à neuf sources, couches B/F, choix des animations corporelles, directions, FPS, échelle et offsets. |
| Éditeur | Carte Pêche sur l’accueil du hub ; zones, outils, prises, légendaires, compétences, animations et sons ; brouillon serveur séparé de l’application au jeu. |

## Correctif du hub, des cartes et de l’enregistrement

La commande existait dans le menu, mais les listes de cartes affichées sur l’accueil du hub omettaient Pêche. Elle figure désormais parmi les nouveautés dès l’ouverture du hub, avec recherche en anglais, français et espagnol. Le test passe par l’initialisation réelle du menu et du hub, puis ouvre la vraie fenêtre depuis une réponse serveur.

La liste cochable des cartes conserve les sélections masquées par le filtre. Ajouter la carte courante n’efface pas les autres choix. Les anciennes zones à carte unique sont migrées sans élargissement de leur rayon ; décocher la restriction au rayon permet de couvrir les cartes entières. Une session en cours reste attachée à sa carte de départ.

**Enregistrer le brouillon** écrit `resources/fishing.draft.json`, même lorsque les objets de certains équipements ou prises ne sont pas encore choisis. **Appliquer au jeu** reste soumis à la validation complète et met à jour `resources/fishing.json`. Seule cette configuration active alimente les Events et les joueurs. Un refus de validation renvoie une erreur à l’éditeur ; les erreurs de données invalides sont maintenant interceptées explicitement. Les écritures se font par fichier temporaire et remplacement atomique. Un brouillon illisible est conservé et signalé.

Une invitation légendaire ignorée en AUTO laisse fuir la créature sans récompense, puis reprend AUTO après 1 500 ms. Accepter l’invitation engage une capture manuelle. STOP pendant l’invitation annule la reprise prévue. Une créature légendaire ne peut donc pas être gagnée en laissant l’automatique répondre au défi.

La consommation d’appât est enregistrée au lancer. Une capture ensuite échouée conserve ce coût. La récompense regroupe les poissons, l’XP, les éventuels objets de niveau et le reçu dans le même checkpoint du compte ; un échec du checkpoint restaure l’inventaire et les variables. Les tests de cette transaction utilisent les véritables classes d’inventaire avec une sauvegarde injectée, sans connexion à une base de production.

## GFX et audio

Le [manifeste](../../resources/fishing/assets-manifest.json) répertorie **20 PNG de jeu** : les deux planches d’objets, les cinq décors, les neuf planches du filet et quatre nouveaux GFX. Les fichiers reçus sont conservés octet pour octet. L’archive originale du filet et la capture de référence sont également conservées dans ce dossier.

Les quatre créations sont le cadre sans texte, l’atlas des composants d’interface, l’atlas d’effets et le premier plan transparent de coraux/ruines. Le client les utilise dans son rendu. Les libellés et les nombres sont dynamiques. Le [catalogue](../../resources/fishing/asset-catalog.json) décrit les variantes de décor, les 140 cellules des deux planches et les clips du filet ; les [rectangles d’atlas](../../resources/fishing/atlas-layout.json) sont fournis séparément.

Le pack contient **32 WAV originaux** et **58 associations sonores**. Le catalogue runtime comprend aussi des alias et événements de cycle de vie, pour un total de 68 clés documentées dans la [matrice audio](Audio-hooks.md). Chaque événement actif accepte l’héritage global → zone → outil → prise, un remplacement ou un silence explicite. Les réglages réellement pris en charge sont le fichier, le volume, la boucle, le délai minimal, le nombre de voix, la spatialisation et la portée. La préécoute est disponible dans l’éditeur.

Deux clés de défis futurs sont signalées comme réservées ; le défi implémenté est la séquence de directions. Les métadonnées de hauteur, bus et fondu du pack ne sont pas présentées comme des réglages déjà actifs dans le moteur. Le [contrat audio](Audio.fr.md) précise ces limites.

## Vérifications locales du correctif

| Vérification | Résultat |
| --- | --- |
| Sources partagées de règles et de simulation | **319 contrôles réussis**, incluant les cartes multiples, la migration et la validation des brouillons. |
| Variante native : règles, inventaire, XP, reçus, persistance et protocole | **393 contrôles réussis**, sur les DLL serveur reconstruites. Le total inclut les contrôles partagés. |
| Intégrité des images, atlas, profils et WAV | **698 contrôles réussis**. |
| Exécutable serveur | Compilation .NET 8 réussie. |
| Client et bibliothèque client | Compilation .NET 8 réussie avec le modèle et le protocole mis à jour. |
| Éditeur Windows et suite de tests de contrôles | Compilation croisée réussie. L’exécution WinForms est confiée au workflow Windows décrit ci-dessous. |
| Textes de l’éditeur et du serveur | **224 et 28 entrées** respectivement ; couverture EN/FR/ES et paramètres de substitution contrôlés. |

Les [preuves locales du correctif](editor-fixes-evidence.json) identifient les sources et assemblages testés. Elles couvrent la sauvegarde et la réouverture des saisies incomplètes, la séparation actif/brouillon, les références de cartes, la corruption, les échecs d’écriture et le refus des actions d’édition aux clients de jeu. La fixture des cartes utilise le registre réel des `MapController`.

`scripts/Verify-Fishing.ps1` et `.github/workflows/fishing.yml` exécutent les compilations, les tests serveur et les deux suites WinForms **`--fishing-only` et `--hub-only`** sous Windows. Les résultats natifs de la révision se consultent dans les contrôles de sa demande de revue GitHub. La compilation croisée locale ne constitue pas une exécution WinForms.

Les [preuves de la version initiale](test-evidence.json), [ses essais CI](windows-ci-attempts.json) et le [rapport des ressources](asset-verification.json) restent conservés comme historique. Les 236/269 contrôles de cette première version ne désignent pas les effectifs du correctif.

Le suivi des autres workflows distingue les régressions Fishing des [deux échecs déjà présents sur le contenu de base](CI-baseline.fr.md). Le statut global de la PR doit être lu avec ces preuves, sans assimiler une réussite du workflow Fishing à une réussite de tous les contrôles du dépôt.

La vérification locale utilise le SDK .NET 8 et MSBuild directement. Une adaptation temporaire hors dépôt exécute la tâche existante de génération des clés dans le processus MSBuild, car son hôte séparé échoue ici. Un numéro de build explicite évite la tâche locale de compteur. Aucune clé privée, sortie binaire ni modification de ces tâches du moteur n’est incluse dans la livraison. Les avertissements de dépendances existantes n’ont pas été traités dans ce lot.

## Recette restant à faire dans le jeu

1. Lier les vrais objets équipables, appâts, prises empilables, profession et cartes dans Fishing Editor. Les identifiants de contenu de production ne sont pas inventés par cette livraison.
2. Ouvrir le mini-jeu par un Event, puis comparer le rendu à la référence aux résolutions utilisées par les joueurs.
3. Vérifier visuellement les directions et les points d’ancrage du filet sur les tenues du projet, avec un second client observateur.
4. Écouter les sons, ajuster leur mixage et vérifier les préécoutes, les boucles et leurs interruptions.
5. Exécuter la recette de pêche active, AUTO, invitation légendaire, STOP, inventaire plein, déconnexion et reconnexion avec un serveur de test et une vraie sauvegarde.

Les données de production et une session graphique multijoueur ne sont pas disponibles dans cette vérification. Les tests automatisés établissent des comportements précis ; ils ne constituent pas une validation visuelle ou sonore du jeu. La branche est destinée à cette recette avant mise en service.
