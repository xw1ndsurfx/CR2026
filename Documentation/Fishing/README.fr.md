# Pêche — Mise en route dans Corps Royaux

Ce guide permet de relier le mini-jeu aux véritables cartes, objets, animations et événements du projet. **Consulter le [rapport d’intégration](Integration-status.fr.md) pour connaître les compilations et essais réellement effectués.** Les commandes et la recette ci-dessous constituent une procédure ; leur présence ne signifie pas qu’une validation Windows ou multijoueur a déjà réussi.

## 1. Préparer des versions compatibles

Utiliser le serveur, le client et l’éditeur provenant de la même révision de cette intégration. La pêche ajoute des paquets réseau et une commande Event ; mettre à jour un seul de ces programmes ne suffit pas. Construire les trois applications avec les mêmes clés de handshake, comme le prévoit le script de vérification.

Pour la recette native, préparer Windows, le SDK .NET 8, les sous-modules du dépôt et Python avec Pillow. Depuis la racine du dépôt, dans PowerShell :

```powershell
git submodule update --init --recursive
python -m pip install Pillow
./scripts/Verify-Fishing.ps1 -Configuration Debug
```

Le [script Verify-Fishing.ps1](../../scripts/Verify-Fishing.ps1) construit le réseau puis le serveur, le client et l’éditeur. Il lance ensuite les vérifications du domaine, des récompenses natives, des contrôles de l’éditeur et de l’intégrité des ressources. Il s’arrête sur un échec. Conserver sa sortie complète pour la recette.

Le [workflow Windows Fishing profession](../../.github/workflows/fishing.yml) exécute cette même procédure sur `windows-latest`, avec sous-modules et dépendances. Il peut être lancé par `workflow_dispatch` et se déclenche sur les modifications couvertes par ses filtres. Un workflow ajouté au dépôt n’est pas un résultat de CI : vérifier son exécution avant d’annoncer un succès.

Dans les dossiers de sortie ou de publication correspondant à cette construction, démarrer le serveur `Intersect Server`, puis l’éditeur `Logiklik 2D Engine` et le client `Corps Royaux`. Employer la configuration réseau et les comptes habituels du projet, sur un environnement de recette. Conserver le dossier de ressources de chaque application : les exécutables seuls ne contiennent pas tout le pack.

La règle [FishingAssets.props](../../Shared/FishingAssets.props) copie les GFX dans `resources/gui/fishing/` et les WAV dans `resources/sounds/fishing/` du client et de l’éditeur. Les sources graphiques restent dans `resources/fishing/` du dépôt. Ne pas déplacer les originaux pour corriger un chemin de lancement.

Les archives **patch**, **full** et **upgrade** des trois plateformes incluent maintenant ces ressources sous `Client and Editor/resources/`, avec leurs sous-dossiers. Les anciennes listes de distribution les omettaient, même lorsque la publication .NET les avait copiées : remplacer uniquement les exécutables d’une ancienne archive pouvait laisser les GFX et sons manquants. Copier aussi le dossier `resources` livré dans la nouvelle archive. Les fichiers de configuration actifs et brouillons du serveur ne sont pas fournis par ce pack.

Le vérificateur couvre les neuf listes de distribution. Le workflow de PR vérifie aussi les chemins et les empreintes des **57 fichiers de pêche** dans le ZIP Windows effectivement produit : 20 PNG, 32 WAV et 5 JSON de ressources. Pour reproduire ce contrôle, utiliser `python scripts/fishing/verify_assets.py --archive <chemin-du-zip-bundle>`. Cette option attend le ZIP de distribution produit dans `dist/`, pas l’enveloppe ZIP téléchargée depuis Actions.

## 2. Créer les objets et choisir la progression

Dans l’éditeur, ouvrir l’**Éditeur d’objets** et créer ou sélectionner les ressources réelles nécessaires :

| Ressource | Réglages à vérifier |
| --- | --- |
| Canne, cage ou filet | Type **Équipement**, emplacement d’équipement valide, conditions permettant au personnage de recette de l’équiper |
| Poisson, homard et autre récompense | Empilement activé et limite de pile d’inventaire supérieure à zéro ; type compatible avec l’empilement |
| Appât éventuel | Objet empilable, distinct de l’outil équipé, quantité disponible pour la recette |

Enregistrer ces objets dans le serveur avant de les sélectionner dans Fishing Editor. Les équipements et les sacs ne sont pas acceptés comme récompenses empilables. Le nom de l’objet n’établit aucune compatibilité automatique : un objet nommé « Homard » doit encore être associé aux cages autorisées.

Configurer aussi l’icône de l’objet dans son éditeur habituel. Le rectangle de sprite choisi plus loin contrôle sa représentation dans la fenêtre de pêche ; il ne remplace pas la définition de l’objet normal de l’inventaire.

Pour l’XP, deux possibilités : créer ou sélectionner une profession de pêche dans **Professions**, puis la choisir dans **Compétences et progression**, ou conserver **Détection automatique / progression intégrée**. Ce dernier choix recherche une profession de pêche reconnue et utilise sinon la progression intégrée persistante sur 25 niveaux. Ne pas saisir un GUID d’exemple : les sélecteurs utilisent les identifiants réels du serveur.

## 3. Ouvrir Fishing Editor et définir les outils

Ouvrir **Mini-Game → Éditeur de pêche**, ou la carte **Éditeur de pêche** affichée dans les nouveautés de l’accueil du hub. La recherche accepte « pêche », « peche », « fishing » et « pesca ». L’ouverture récupère le brouillon enregistré sur le serveur ; en l’absence de brouillon, elle reprend la configuration active.

Dans **Équipements**, cliquer sur **Nouvel équipement**, sélectionner **Objet à équiper**, puis choisir le **Type d’équipement**. Selon la langue, les valeurs correspondent à `Rod`, `Cage` et `Net` :

| Profil | Association de recette recommandée |
| --- | --- |
| `Rod` — canne | Un poisson autorisé uniquement avec cette canne |
| `Cage` — cage | Un homard autorisé uniquement avec cette cage |
| `Net` — filet | Une prise autorisée uniquement avec ce filet |

Ces associations seront définies dans chaque prise. Plusieurs objets de la même famille peuvent posséder des profils distincts. Un même objet équipable ne peut avoir qu’un profil de pêche.

Régler l’appât éventuel, sa consommation par tentative et la puissance. Pour un premier essai simple, utiliser **Aucun appât**, puis tester séparément la consommation d’un véritable appât. L’outil doit être porté dans un emplacement d’équipement : le laisser dans le sac ne permet pas de pêcher.

Dans **Animations**, sélectionner le même outil. Les champs de lancer, d’utilisation et de capture désignent des animations existantes du projet. Pour un filet, **Utiliser le profil Fishing Net fourni** remplit les neuf planches B/F livrées. Ajuster ensuite les directions, FPS, dimensions des cellules, animation corporelle, offsets et échelle sur l’avatar réel. Le preset n’est pas une preuve que les ancrages conviennent à tous les corps et tenues.

Les planches fournies sont des couches de filet devant/derrière l’avatar. Ne pas les traiter comme des variantes de sexe et ne pas supprimer leurs marges transparentes. Le mini-jeu reste sans personnage ni quai ; les animations se produisent dans le monde normal.

## 4. Créer une zone et choisir ses décors

Dans **Zones**, cliquer sur **Nouvelle zone**. Elle commence désactivée. Saisir son nom et ses variantes anglaise, française et espagnole. Cocher les cartes qui appartiennent à cette zone, comme dans Dungeon Editor : par exemple le port, la côte et les autres cartes de Polonya. Le filtre aide à retrouver une carte et conserve les cartes déjà cochées même lorsqu’elles sont masquées. Le bouton d’ajout de la carte actuelle conserve aussi la sélection existante.

Les nouvelles zones couvrent les **cartes entières sélectionnées**. La restriction à une position X/Y et à un rayon est une option explicite. Une ancienne configuration à carte unique est convertie en conservant son ancienne restriction ; l’ouverture ne transforme donc pas sa petite zone en carte entière. La liste est limitée à 256 cartes et n’accepte ni doublons ni identifiants vides. Un changement de carte pendant une partie interrompt la pêche, même si la nouvelle carte appartient à la même zone.

Choisir séparément **Arrière-plan**, **Premier plan** et **Décorations**. Voici comment employer les cinq images reçues :

| Image | Utilisation |
| --- | --- |
| `Water_Background1.png` | Arrière-plan composite avec récif, ruines, coraux et sable ; commencer avec premier plan et décorations vides |
| `Water_Background2.png` | Arrière-plan vertical, de la surface aux profondeurs ; commencer sans superposition supplémentaire |
| `Water_Background3.png` | Arrière-plan d’épave sous-marine ; peut recevoir le sable 5 et les coraux transparents |
| `Water_Background4.png` | Arrière-plan de pleine mer ; peut recevoir le sable 5 et les coraux transparents |
| `Water_Background5.png` | **Premier plan de sable avec alpha**, à superposer à un fond approprié |
| `coral_foreground.png` | **Décorations** de coraux transparents, dans `fishing/gui/` |

Les quatre premières images sont des variantes complètes ; elles ne doivent pas être empilées comme quatre calques transparents. Le [catalogue d’assets](../../resources/fishing/asset-catalog.json) donne les combinaisons proposées et les grilles de sprites. **Prévisualiser l’image** aide à identifier un fichier ; la recette dans le client vérifie son cadrage final.

Régler ensuite les attentes, la durée de résolution automatique, la limite de lutte active et le délai d’invitation légendaire. Autoriser ou non l’automatique dans cette zone. La **Probabilité légendaire (%)** est une chance indépendante ; les poids des prises départagent ensuite les candidates admissibles.

## 5. Ajouter des prises et une légendaire

Dans **Prises**, choisir **Nouvelle prise**, sélectionner l’**Objet remis** et cocher les **Équipements autorisés**. Définir le poids relatif, le niveau minimal, les quantités, l’XP et la difficulté. Pour la première recette, garder une prise ordinaire admissible sans condition supplémentaire.

Les conditions de profondeur, d’heure UTC et de variable joueur peuvent ensuite limiter l’entrée. Une plage de 22 à 4 couvre la nuit en UTC ; l’heure de fin est exclue. Une variable obligatoire doit exister dans le projet. Si aucun poisson ne remplit les conditions avec l’outil porté, le serveur ne peut pas sélectionner une prise.

Choisir la planche d’icônes et renseigner le rectangle en pixels. Les deux planches utilisateur utilisent des cellules de 32 × 32. Avec des indices commençant à zéro, la colonne `c` et la ligne `r` donnent `X = 32 × c`, `Y = 32 × r`, largeur 32, hauteur 32. Le catalogue liste les cellules disponibles : ne pas inventer la position d’une espèce à partir de son nom.

L’onglet **Créatures légendaires** filtre les prises marquées légendaires. Ajouter une entrée, choisir un objet de récompense réel, ses outils autorisés, son visuel et ses paramètres de défi. Le défi intégré est une suite de directions avec nombre d’étapes et fenêtre de réponse configurables. Les types de défis autonomes de timing ou de moulinet ne sont pas annoncés comme disponibles.

En automatique, une rencontre légendaire suspend la pêche et affiche une invitation. Si le joueur ne répond pas avant le délai, la créature fuit sans poisson ni XP légendaire, puis la pêche automatique reprend après 1 500 ms. Le joueur peut annuler cette reprise avec STOP pendant l’invitation ; celle-ci reste alors disponible en mode manuel. Une invitation acceptée engage une lutte et un défi manuels : leur réussite ou leur échec ne réactive jamais l’automatique. Une invitation déclenchée en mode manuel reste également manuelle après expiration. Tester ces cas séparément ; une créature légendaire ne doit jamais être créditée simplement parce que le joueur est absent.

Activer la zone lorsqu’elle possède au moins une carte et une prise correctement liée. Les profils d’équipement ou prises incomplets peuvent être conservés dans le brouillon. Ils doivent être complétés avant l’application au jeu ; la validation indique l’élément à corriger et sélectionne son onglet.

## 6. Régler les sons et enregistrer

Dans **Sons**, choisir la **Portée du réglage sonore** : global, zone sélectionnée, outil sélectionné ou prise sélectionnée. Sélectionner l’événement puis son **Remplacement local** :

- **Hériter** supprime la surcharge à cette portée.
- **Silence** bloque explicitement le son et le repli vers une autre portée.
- **Son personnalisé** sélectionne un fichier et permet de régler volume, cooldown, nombre simultané, boucle, spatialisation et portée.

La priorité est **prise → outil → zone → global → défaut**. Préférer une clé précise par action pour éviter des alias concurrents. La préécoute lit les WAV au volume système ; elle ne simule pas le mixage du jeu. Les OGG peuvent être assignés et demandent une écoute dans le client. **Arrêter l’aperçu** coupe une boucle de test.

Le pack contient 32 WAV et 58 bindings ; le catalogue runtime expose 68 clés, dont alias et signaux d’arrêt. Les deux clés de défis réservés restent prévisualisables mais leurs overrides sont désactivés. Le détail des paramètres et limites figure dans le [contrat audio](Audio.fr.md) et la [matrice des hooks](Audio-hooks.md).

**Enregistrer le brouillon**, ou `Ctrl+S`, conserve le travail dans le fichier serveur `resources/fishing.draft.json`, y compris les équipements et prises encore incomplets. Attendre l’accusé de sauvegarde. Fermer puis rouvrir l’éditeur permet de reprendre ce travail. Ce bouton ne remplace pas la configuration utilisée par les joueurs et ne publie pas les zones dans le sélecteur Event.

Quand la configuration est prête, utiliser **Valider**, puis **Appliquer au jeu**. Cette seconde action vérifie les objets équipables, les récompenses empilables, toutes les cartes et les autres références avant de modifier la configuration active `resources/fishing.json`. Une application réussie synchronise aussi le brouillon. Un échec conserve les données éditées ; aucune configuration invalide n’est activée.

### Si l’Event affiche toujours « Désactivé »

Le panneau au-dessus des onglets compare **Brouillon** et **Dernier état confirmé par le serveur** pour la même zone. La case **Activé** modifie le brouillon ; le suffixe de l’Event dépend de la configuration active reçue du serveur. L’ouverture de Fishing Editor demande les deux configurations séparément. Un état encore inconnu, une actualisation en cours et un échec de mise à jour sont affichés explicitement.

Après **Appliquer au jeu**, attendre **Appliqué au jeu.** En cas de refus, un bandeau affiche la raison complète et reste présent pendant les modifications et l’enregistrement du brouillon. Les refus serveur précisent maintenant la ressource concernée, son identifiant et le réglage à corriger : métier absent, emplacement d’équipement invalide, appât non empilable, animation ou carte absente, récompense incompatible, pile maximale invalide ou variable absente. Un refus conserve l’ancienne configuration active.

Si un métier vient d’être associé, vérifier qu’il a bien été enregistré dans **Professions** sur ce serveur. Si une prise légendaire vient d’être ajoutée, son **Objet remis** doit exister et accepter l’empilement, comme une prise ordinaire. Les contrôles portent sur toute la configuration, y compris les autres profils et zones.

**Actualiser la configuration active** consulte l’état utilisé par le serveur. L’Event se met aussi à jour lorsqu’il est déjà ouvert, en conservant le GUID sélectionné. Si l’actualisation échoue, les deux fenêtres le signalent et présentent explicitement le dernier état confirmé ; elles ne remplacent pas les données par un état supposé.

Pour examiner les fichiers, identifier clairement `fishing.json` et `fishing.draft.json` dans le dossier `resources` du serveur réellement lancé. Deux blocs JSON sans leur nom de fichier ne permettent pas de déterminer lequel est appliqué. Un fichier modifié directement sur disque n’actualise pas automatiquement le cache d’un serveur déjà lancé ; le parcours normal est **Appliquer au jeu** dans l’éditeur connecté à ce serveur.

Une absence de réponse ne prouve pas que le serveur n’a rien écrit. Après un doute réseau, vérifier séparément le brouillon et la configuration active avant de continuer. Le client, le serveur et l’éditeur doivent tous être mis à jour pour utiliser ces actions.

Les changements effectués après l’envoi restent signalés comme non enregistrés. Une sauvegarde en cours bloque la fermeture jusqu’à sa réponse ou son délai d’attente. `Échap` ferme une liste déroulante ouverte, puis demande la fermeture de l’éditeur ; un brouillon modifié demande confirmation. Dans le sélecteur Event, `Échap` annule le choix en cours.

## 7. Relier l’Event et essayer dans le client

Créer ou modifier l’Event habituel du point de pêche. Ajouter **Ouvrir la pêche**, sélectionner une zone **déjà appliquée au jeu**, puis enregistrer la commande et l’Event. **Actualiser les zones** demande la configuration active au serveur ; les brouillons restent exclus de cette liste. L’enregistrement de cette commande ne sauvegarde pas les modifications encore ouvertes dans Fishing Editor.

Dans le client compatible, placer le personnage dans la zone, équiper l’outil choisi et déclencher l’Event. Vérifier d’abord un lancer manuel et une récompense ordinaire. Les piles affichées doivent correspondre aux objets de l’inventaire. Maintenir puis relâcher la remontée, utiliser les compétences disponibles et fermer par la croix ou `Échap`.

### Si l’interaction n’ouvre pas la fenêtre

Un événement **Action Button** sur la case visée est prioritaire sur une ressource récoltable placée sur cette même case. Les PNJ et joueurs attaquables conservent la priorité du combat. Une ressource seule, un événement sur une autre case ou un déclencheur autre qu’**Action Button** conservent le comportement habituel de récolte. Cette correction est côté **client** : mettre à jour seulement le serveur ou l’éditeur ne modifie pas l’interaction dans un ancien client.

Un événement sans image et avec **Disable Inspector** reste utilisable par la commande d’interaction face à sa case. L’objet de pêche doit être équipé ; la présence d’un filet dans l’inventaire ne suffit pas. L’absence d’appâts ne bloque pas l’ouverture de la fenêtre : elle est signalée au lancer.

Pour distinguer une interaction qui n’atteint pas l’événement d’un refus de pêche, placer temporairement **Afficher un texte** avant **Ouvrir la pêche**. Enregistrer chaque commande, puis **Sauvegarder l’événement et la carte**. Le texte doit apparaître dans le client, puis être fermé pour laisser continuer la commande suivante. Le déclenchement doit être vérifié dans le client. Un refus de zone ou d’équipement s’affiche dans le chat ; vérifier également les conditions de la page si le texte ne paraît pas.

Si **Dernier état confirmé par le serveur : Activé** est déjà affiché, conserver cette configuration pendant le diagnostic. Le type **Filet** décrit le profil de pêche ; **Objet à équiper** désigne l’objet exact à porter. Un objet nommé **Knife Wood** peut être configuré comme filet : son nom ne le rend pas invalide. Les monnaies peuvent aussi servir d’appâts ou de récompenses lorsqu’elles satisfont les règles d’empilement.

Les refus d’ouverture utilisent désormais le chat normal, indépendamment de l’interface et du son de pêche. Le refus d’équipement donne le nom des objets configurés. Une exception de création ou de mise à jour de la fenêtre est signalée dans le chat et le journal du client ; les contrôles de pêche sont libérés et la session est quittée pour permettre une nouvelle tentative.

Pour un blocage persistant, faire **une interaction dans le client**, puis conserver le fichier le plus récent `logs/client-loop-*.log`, le journal `logs/Corps Royaux-*.log` et `logs/errors-Corps Royaux.log` s’il existe, dans le dossier du client lancé. Conserver également le journal `logs/Intersect Server-*.log` du serveur sur la même période. Le journal d’erreurs contient le détail d’une exception d’affichage ; les marqueurs permettent de localiser l’arrêt :

| Marqueur | Ce qu’il établit |
| --- | --- |
| `interaction-press` | Le client voit un nouvel appui ; les indicateurs précisent le focus, une activité bloquante, un dialogue, une attaque ou l’état naval. |
| `event-activate sent` | Le client a envoyé l’identifiant de l’Event visé. |
| `Event interaction received` | Le serveur a reçu l’activation d’un administrateur, avant ses filtres d’activité. |
| `Fishing opening requested` | L’Event a atteint la commande **Ouvrir la pêche**. |
| `Fishing opening refused` / `ignored` | Le journal donne la raison du refus ou le contexte invalide. |
| `Fishing opening ready` / `reopened` | Le serveur a envoyé l’ouverture de la session. |
| `fishing-packet received` puis `fishing-window displayed` | Le client a reçu la réponse puis construit et affiché la fenêtre. |
| `Fishing closing`, `fishing-window closing` ou `fishing-window failed` | La session a été fermée ou l’interface a rencontré une exception. |

Une absence de marqueur ne suffit pas à conclure si l’application n’utilise pas cette révision ou si son journal n’est pas celui de l’exécutable lancé. Les traces ne contiennent ni texte de dialogue, ni chat, ni configuration complète. Il n’est pas nécessaire de modifier les cartes pour les obtenir.

La mention **Son manquant : fishing/…wav** dans l’éditeur indique que le fichier n’est pas disponible dans ses ressources locales. Conserver le sous-dossier `resources/sounds/fishing/` livré avec le client et l’éditeur. Le client indexe désormais les WAV de ce sous-dossier en conservant leurs chemins relatifs ; un son absent reste facultatif et ne prouve pas la cause d’une absence de fenêtre.

Poursuivre la recette avec les trois familles d’outils : chaque prise exclusive doit rester inaccessible aux deux autres. Tester ensuite l’automatique connecté, le manque d’appâts, l’inventaire plein et les légendaires. Déconnecter puis reconnecter pour vérifier la persistance de l’XP et l’absence de récompenses hors ligne.

Avec un second client, contrôler les couches du filet, les directions, les tenues et la fin des animations. Écouter les boucles, leur arrêt à la fermeture et les volumes réellement appliqués. La spatialisation sonore est locale au mixeur du joueur ; elle ne constitue pas une diffusion automatique des effets de pêche à tous les observateurs.

## Références et preuves de recette

- [Mandat complet et critères d’acceptation](Work-Mandate.fr.md).
- [Rapport d’intégration et limites constatées](Integration-status.fr.md).
- [Contrat audio](Audio.fr.md), [hooks détaillés](Audio-hooks.md) et [matrice JSON](Audio-hooks.json).
- [Catalogue des fonds et cellules](../../resources/fishing/asset-catalog.json), [rectangles des atlas](../../resources/fishing/atlas-layout.json) et [manifeste des originaux](../../resources/fishing/assets-manifest.json).
- [Tests du domaine et des récompenses](../../Utilities/FishingRulesTests/) ; [smoke test de l’éditeur](../../Utilities/InterfaceEditorTests/FishingEditorTests.cs), option `--fishing-only`.
- [Script de vérification Windows](../../scripts/Verify-Fishing.ps1) et [workflow correspondant](../../.github/workflows/fishing.yml).

Consigner séparément les résultats automatisés, les captures Windows, l’écoute audio et les essais à deux clients. Une compilation seule ne couvre pas les trois derniers points.
