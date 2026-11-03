# Métier Pêche — état de l’intégration

## Livraison

La première version du métier a été intégrée à `main` par la [PR #218](https://github.com/xw1ndsurfx/CR2026/pull/218), au commit `210114fd695fa937ea33b92037e38cb9daef272b`. La [PR #219](https://github.com/xw1ndsurfx/CR2026/pull/219), fusionnée au commit `1bfdcf6be2aefbcbbd4444aea6b55b9cfa49d316`, traite l’accès depuis le hub, les zones à plusieurs cartes et la sauvegarde d’un travail incomplet. Le correctif `fix/fishing-activation-feedback` part de ce dernier commit pour rendre visibles les refus d’application et le dernier état confirmé par le serveur. Le [guide de mise en route](README.fr.md) explique les liaisons de contenu à effectuer. Le [mandat](Work-Mandate.fr.md) conserve les demandes et les critères de recette.

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

## Diagnostic des zones restant désactivées

La case **Activé** appartient au brouillon. L’Event affiche la valeur du même GUID dans la configuration active reçue du serveur. Les deux états sont maintenant comparés dans un panneau permanent de Fishing Editor ; la lecture active est demandée à l’ouverture en plus du brouillon. Un état inconnu, une absence de zone, une actualisation et un échec de mise à jour restent distincts.

Un refus d’application apparaît dans un bandeau dédié, avec un texte complet, multiligne et défilable. Le message reste visible pendant les corrections et les sauvegardes du brouillon. Les 16 diagnostics serveur précisent le métier, l’équipement et son emplacement, l’appât, l’animation, la carte, le rayon, la prise, la récompense ou la variable qui bloque. Les contrôles de validité sont conservés. Une absence de confirmation est présentée comme telle, sans affirmer que le serveur n’a rien écrit.

Les erreurs d’actualisation sont également transmises au dialogue Event. La dernière configuration confirmée reste disponible et explicitement identifiée ; une erreur de lecture ne remplace pas ce cache. Le sélecteur conserve le GUID courant et retire **Désactivé** dès qu’une configuration active avec `Enabled=true` est reçue, même s’il est resté ouvert.

Une confusion entre lecture et sauvegarde a aussi été corrigée : une erreur de lecture reçue pendant une application en attente pouvait terminer cette attente et faire ignorer son accusé. Le serveur identifie désormais l’opération de configuration dans sa réponse. Le champ nullable est ajouté en clé MessagePack 8, sans renuméroter les anciennes clés ni les actions ; les anciens paquets gardent le traitement compatible. Il ne s’agit pas d’un réordonnancement du transport réseau.

Les [preuves locales de ce correctif](activation-feedback-evidence.json) couvrent la transition serveur réellement sauvegardée de `Enabled=false` à `true`, les refus détaillés dans les trois langues, la conservation des deux fichiers, le protocole et la compilation de l’éditeur. Les tests WinForms ajoutés passent par le thème et le parcours réel d’ouverture, gardent l’Event ouvert pendant l’application et vérifient le message long à la taille minimale. Leur exécution native est confiée au workflow Windows de la révision ; une compilation croisée ne constitue pas cette exécution.

Les deux configurations transmises pendant le diagnostic passent les règles structurelles partagées et conservent leur valeur `Enabled` après sérialisation. Leurs noms de fichiers et les définitions des ressources de production n’étaient pas fournis : cela ne permet pas d’attribuer avec certitude le refus à une ressource particulière. Les tests utilisent des références synthétiques ; aucun identifiant de contenu de production n’est ajouté au dépôt.

## Ouverture depuis un événement

Le parcours d’interaction du client examinait les ressources avant les événements. Lorsqu’une ressource et **Fishing Spot** partageaient la case visée, il envoyait une attaque de récolte puis quittait la méthode avant **Ouvrir la pêche**. Le message demandant une hache pouvait donc apparaître même avec un filet correctement équipé. Les ressources sont désormais différées jusqu’à la recherche d’un événement **Action Button** sur cette même case ; les cibles de combat conservent leur priorité. Les ressources seules et les autres déclencheurs ne changent pas de comportement.

Les régressions ajoutées exécutent les vraies classes du moteur :

- `FishingEventInteractionTests` : interaction avec filet équipé, événement sans image et inspecteur désactivé, superposition de ressource, autre case, autres déclencheurs et priorité du combat.
- `ServerSessionChecks` : événement sauvegardé puis rechargé, activation réseau, affichage et réponse au dialogue, ouverture de pêche, fin de l’événement et tick suivant, refus explicites et interruption par déplacement.
- `FishingWindowTests` : registre réseau, sérialisation du paquet, vraie fenêtre à 1600 × 900 et 800 × 600, rendu sans ressources optionnelles, clic de lancement, fermeture et réouverture, refus dans le chat.

Le script Windows exécute ces contrôles client en plus des contrôles serveur et éditeur. Les tests de fenêtre utilisent un renderer de test sans GPU ; ils ne remplacent pas une recette visuelle sur les cartes de production. La superposition est un défaut reproduit, mais les captures seules ne prouvent pas qu’elle explique un essai ultérieur sur une case libre. La présence de **Test pêche** dans la liste des commandes ne prouve pas non plus que ce dialogue est apparu dans le client.

Validation locale sur la base `2a3d594edf501fb50ed8d192c77a53c0dde950c5` : **695 contrôles serveur réussis**, dont le parcours réel d’ouverture, et **15 tests client réussis**. Avant la correction, les deux variantes événement + ressource envoyaient une attaque au lieu d’activer l’événement ; le cas de superposition avec un PNJ vérifiait aussi la priorité du combat. Les 12 autres cas passaient déjà. Le client et le serveur ont été compilés en .NET 8 ; les tests client ont été exécutés par le runner NUnit dans le processus car l’hôte VSTest de l’environnement local échoue avant leur lancement. Le workflow Windows utilise `dotnet test` normalement.

## GFX et audio

### Suivi du blocage d’ouverture après la PR #222

Les nouvelles captures montrent une zone confirmée active, un objet **Knife Wood** associé au type **Filet**, **Auragems** comme appât, **Aureons** comme prise et le métier personnalisé **Fishing**. Ces choix ne sont pas invalides du seul fait de leurs noms. Les captures ne montrent pas l’exécution du dialogue dans le client ni ses journaux : elles ne permettent donc pas d’attribuer le blocage à une cause certaine.

Le correctif de suivi apporte des refus par le chat standard, avec le nom de l’objet attendu, et des traces entre l’appui d’interaction, l’activation, la commande serveur et l’affichage. Les exceptions de création ou de mise à jour de l’interface produisent un message visible ; le nettoyage libère les contrôles et envoie la sortie de session avant l’arrêt audio facultatif. La fermeture par Échap utilise également ce traitement. Aucun contrôle d’équipement, de zone ou d’admissibilité n’est contourné.

Un défaut audio distinct a été confirmé : le chargeur des WAV ne parcourait que le premier niveau de `resources/sounds`, alors que les ressources de pêche sont livrées dans `sounds/fishing`. Il parcourt désormais les sous-dossiers en conservant le chemin relatif et le chemin réel du fichier. Les noms historiques, les sons vocaux et la priorité des fichiers ordinaires sur les packs restent couverts par les tests. Cette correction audio n’est pas présentée comme une preuve de résolution du blocage d’Event.

Une seconde omission a été confirmée dans les archives de distribution : les listes **patch**, **full** et **upgrade** incluaient les exécutables et le pack océan, mais aucun GFX ou WAV de pêche. Les neuf variantes des trois plateformes livrent désormais les sources de pêche dans `Client and Editor/resources/gui/fishing/` et `Client and Editor/resources/sounds/fishing/`. Le vérificateur dérive les chemins attendus des manifestes d’assets et vérifie leur couverture. Le workflow de PR contrôle également les 57 fichiers et leurs empreintes dans le ZIP Windows réellement produit avant de publier l’artifact.

La suite client couvre les sons absents, une exception de création sonore, une exception pendant la construction de la fenêtre et une erreur à l’arrêt d’une boucle déjà active. Elle vérifie le message, la sortie de session avec une séquence correcte, la libération des contrôles et la possibilité de rouvrir. Les tests restent exécutés sans GPU ; le diagnostic de l’installation réelle demande une interaction et les journaux décrits dans le [guide](README.fr.md#si-linteraction-nouvre-pas-la-fenêtre).

Validation locale sur la base `1b8b4f4` : compilations serveur et client réussies, **729 contrôles serveur**, **23 tests client** et **1 220 contrôles de ressources et de distribution** réussis. Le scénario serveur utilise l’initialisation native de l’inventaire à la connexion puis le véritable équipement d’un objet depuis la case 9, avec métier personnalisé et progression enregistrée. Les refus par le chat sont vérifiés en anglais, français, espagnol et avec une langue inconnue. Les 45 entrées du catalogue serveur et les 61 entrées du catalogue d’interface conservent leur couverture EN/FR/ES et leurs paramètres. La [CI Windows de la première révision](https://github.com/xw1ndsurfx/CR2026/actions/runs/41641269228) confirme les 729 contrôles serveur, 23 tests client et suites WinForms Fishing Editor/Editor Hub ; elle précède le complément de packaging. La vérification des archives appartient à la révision suivante et son résultat doit être lu dans les contrôles de la PR.

Le [manifeste](../../resources/fishing/assets-manifest.json) répertorie **20 PNG de jeu** : les deux planches d’objets, les cinq décors, les neuf planches du filet et quatre nouveaux GFX. Les fichiers reçus sont conservés octet pour octet. L’archive originale du filet et la capture de référence sont également conservées dans ce dossier.

Les quatre créations sont le cadre sans texte, l’atlas des composants d’interface, l’atlas d’effets et le premier plan transparent de coraux/ruines. Le client les utilise dans son rendu. Les libellés et les nombres sont dynamiques. Le [catalogue](../../resources/fishing/asset-catalog.json) décrit les variantes de décor, les 140 cellules des deux planches et les clips du filet ; les [rectangles d’atlas](../../resources/fishing/atlas-layout.json) sont fournis séparément.

Le pack contient **32 WAV originaux** et **58 associations sonores**. Le catalogue runtime comprend aussi des alias et événements de cycle de vie, pour un total de 68 clés documentées dans la [matrice audio](Audio-hooks.md). Chaque événement actif accepte l’héritage global → zone → outil → prise, un remplacement ou un silence explicite. Les réglages réellement pris en charge sont le fichier, le volume, la boucle, le délai minimal, le nombre de voix, la spatialisation et la portée. La préécoute est disponible dans l’éditeur.

Deux clés de défis futurs sont signalées comme réservées ; le défi implémenté est la séquence de directions. Les métadonnées de hauteur, bus et fondu du pack ne sont pas présentées comme des réglages déjà actifs dans le moteur. Le [contrat audio](Audio.fr.md) précise ces limites.

## Vérifications locales du correctif du hub et des cartes (PR #219)

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
