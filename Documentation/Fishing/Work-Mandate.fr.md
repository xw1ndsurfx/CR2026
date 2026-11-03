# Corps Royaux — Mandat de développement du métier Pêche

**Statut : intégration en cours ; vérifier le rapport final d’intégration avant de considérer une fonction comme livrée.** Ce document définit le résultat demandé, les ressources à employer et les critères de recette. La présence d’un modèle, d’un bouton ou d’un fichier graphique ne constitue pas une preuve que son comportement fonctionne dans le client, le serveur et l’éditeur.

## 1. Objectif et décisions retenues

Créer un métier Pêche et son mini-jeu pour Corps Royaux. La pêche active demandée s’inspire de TLOPO : observer les prises sous l’eau, présenter un appât, accrocher une créature et gérer sa remontée. Ajouter une pêche automatique lorsque le joueur reste connecté, ainsi que de véritables objets de prise qui s’accumulent dans l’inventaire par espèce.

La [maquette de gameplay approuvée](references/gameplay-approved.png) définit la direction visuelle. La [maquette de l’éditeur](references/editor-concept.png) illustre l’organisation proposée des réglages. Ce sont des références de présentation : leurs nombres, textes d’exemple et positions ne remplacent pas les données du jeu.

La fenêtre de pêche ne contient **ni personnage ni quai**. La ligne entre depuis le bord de la vue, permettant de réutiliser cette présentation depuis une rive, un port ou un bateau. L’avatar réel demeure dans le monde normal et joue l’animation de son outil. Ces deux représentations doivent rester distinctes : aucun pêcheur générique ne doit être réintroduit dans le mini-jeu.

## 2. Déclenchement par Event et équipement obligatoire

L’ouverture passe par le système d’événements habituel. L’auteur ajoute une commande d’ouverture de pêche et choisit une zone enregistrée. L’événement peut être associé à un PNJ, une interaction ou un emplacement conforme aux possibilités existantes du moteur. Le mini-jeu ne contourne ni les conditions de l’événement ni les contrôles serveur.

Avant l’ouverture, le serveur vérifie la zone, la position, l’état du personnage, l’outil réellement équipé et les éventuels appâts. Posséder une canne dans le sac ne suffit pas. Un refus doit expliquer clairement son motif dans la langue du joueur et ne doit pas consommer d’appât.

Le terme générique « équipement de pêche » couvre trois familles : `Rod`, `Cage`, `Net`. La canne permet les poissons qui lui sont associés ; une cage peut permettre les homards ; le filet permet ses propres prises. Ce sont des associations éditables, pas des règles figées sur le nom affiché de l’objet. Plusieurs objets de la même famille peuvent proposer des puissances, des appâts et des compatibilités différentes.

Un changement d’outil, une sortie de zone, une téléportation, la mort, une interruption de l’événement ou une déconnexion doivent provoquer une transition contrôlée. Aucune session précédente ne doit continuer à donner des objets en arrière-plan. La fermeture du mini-jeu restitue les contrôles ordinaires et arrête ses sons et animations temporaires.

## 3. Configuration dans Fishing Editor

L’éditeur doit permettre de créer, rechercher, dupliquer, désactiver et modifier les zones et leurs prises. Les références vers objets, cartes, professions et animations se sélectionnent parmi les ressources existantes. Afficher une erreur identifiable lorsqu’une référence a disparu ; ne pas remplacer silencieusement un objet par un autre.

### Zones

Chaque zone porte un identifiant stable, un nom traduit et une sélection de cartes, sur le modèle de Dungeon Editor. Elle couvre par défaut les cartes entières ; une restriction commune à X/Y et à un rayon reste disponible en option. Les anciennes zones à carte unique conservent leur périmètre lors de leur migration. Elle choisit son arrière-plan, son éventuel premier plan, ses délais d’attente et ses autorisations de pêche automatique. Elle contient une liste explicite des prises possibles.

L’éditeur doit figurer dans l’accueil du hub et dans les résultats de recherche. **Enregistrer le brouillon** conserve la saisie même si les équipements ou prises ne sont pas encore liés. **Appliquer au jeu** valide les références et active la configuration pour les événements et les joueurs. Le fichier de brouillon est séparé de la configuration active ; une saisie incomplète ne doit pas modifier les zones déjà utilisables.

Une zone peut avoir son propre décor et sa propre population. Une eau côtière peut contenir plusieurs petits poissons ; une zone marine peut réserver certaines créatures à un outil supérieur. Les exemples n’imposent aucune distribution sur les cartes existantes : les cartes et les identifiants doivent être liés dans l’éditeur.

### Outils

Pour chaque objet de pêche, configurer sa famille, son éventuel appât, la quantité consommée, sa puissance et ses animations de lancer, d’utilisation et de capture. Les sons peuvent être remplacés à cette portée. Les valeurs doivent rester éditables sans recompilation.

Le fichier de configuration partagé emploie notamment `ItemId`, `BaitItemId`, `CastAnimationId`, `UseAnimationId` et `CatchAnimationId`. Ces valeurs désignent les GUID des ressources du projet. Un nom lisible ne doit pas être traité comme un identifiant. Le filet fourni n’autorise pas à inventer les GUID de ses animations.

### Prises

Chaque entrée associe un objet de récompense, une liste d’objets de pêche autorisés, un poids de tirage, un niveau minimal, une quantité minimale et maximale, une expérience et une difficulté. Ajouter les conditions proposées par le modèle : profondeur, plage horaire et éventuelle variable requise. Une créature légendaire possède aussi les paramètres de son défi.

La sélection doit respecter l’intersection suivante : **prise présente dans la zone, outil équipé compatible, conditions remplies**. Filtrer avant d’appliquer les poids. Un poids de 10 représente une importance relative parmi les prises admissibles, pas une probabilité universelle de 10 %. Une cage ne doit jamais sélectionner un poisson uniquement autorisé pour une canne.

La zone possède une probabilité de rencontre légendaire distincte. Les poids départagent ensuite les prises admissibles de la catégorie retenue ; augmenter le nombre de poissons ordinaires ne doit pas modifier implicitement cette probabilité. Le réglage initial de 2 % reste modifiable et doit être équilibré en jeu.

Une table devenue vide doit produire un état explicite. Rejeter les quantités inversées, poids invalides, références absentes et fenêtres de temps incohérentes. L’éditeur doit prévenir lorsque sa configuration rend toute prise impossible pour un outil pourtant autorisé.

### Exemple de liaison sans GUID inventé

| Champ | Saisie attendue |
| --- | --- |
| `ProfessionId` | Sélectionner une profession existante ou laisser « Détection automatique / progression intégrée » |
| Commande Event `ZoneId` | Choisir la zone enregistrée dans Fishing Editor |
| Outil `ItemId` | Sélectionner l’objet Cage réellement équipable |
| Prise `ItemId` | Sélectionner l’objet Homard existant ou créé dans l’éditeur d’objets |
| Prise `ToolItemIds` | Ajouter le GUID de cette cage et des autres cages autorisées |
| Animations de l’outil | Lier les animations importées et validées dans le projet |

Cette table décrit une procédure de saisie ; elle n’est pas un fichier importable. Aucune carte, profession ou récompense fictive ne doit être activée par défaut dans une partie existante.

Le choix automatique recherche une profession de pêche reconnue ; sans correspondance, la progression intégrée persiste l’expérience du joueur sur 25 niveaux. Ce repli évite d’inventer un identifiant de profession. Vérifier en recette la courbe retenue et la conservation de l’XP après reconnexion.

## 4. Pêche active, compétences et progression

Le déroulement visé est : lancer, attente, morsure, lutte, résultat puis nouvelle tentative. La scène présente la profondeur, l’état de la ligne et une réaction lisible de la créature. Maintenir le contrôle de remontée fait progresser la prise ; relâcher pendant sa résistance préserve la ligne. La caméra peut suivre l’hameçon dans un décor vertical, sans déplacer les panneaux de contrôle.

Les quatre compétences de la maquette sont configurables : **Stabiliser**, **Traction**, **Réparer**, **Plonger**. Définir pour chacune son activation, son niveau requis, son délai de recharge, sa durée et sa puissance. Traction ne doit pas ignorer un coût de tension prévu par la configuration ; Réparer ne doit pas rendre une prise automatiquement gagnée. Le serveur décide de la disponibilité réelle et refuse les activations répétées hors délai.

La barre du métier affiche le niveau, l’XP gagnée et la progression vers le niveau suivant. Réutiliser les conventions du système de professions. Le niveau 12, les quantités et les valeurs de la maquette sont illustratifs ; ils ne constituent pas l’équilibrage final.

À la réussite, remettre l’objet configuré dans l’inventaire normal. Les prises identiques augmentent une pile existante selon les limites de cet objet ; des espèces ou variantes différentes ne sont pas fusionnées. La pêche ne vend pas automatiquement les prises. Leur utilisation en cuisine, commerce, quêtes ou exportation repose sur les systèmes déjà présents et sur des contenus à lier séparément.

Le serveur attribue les objets, consomme les appâts et accorde l’XP une seule fois. Une répétition de paquet ou de clic ne doit pas répéter la récompense. Si la capacité change entre le début et la fin d’une tentative, appliquer une issue définie et visible ; aucune perte ou duplication silencieuse n’est acceptable.

## 5. Mode automatique et créatures légendaires

La pêche automatique fonctionne uniquement pendant la connexion du joueur. Elle enchaîne les tentatives avec les mêmes contrôles de zone, d’équipement, de niveau, d’appât et de capacité. Aucun calcul rétroactif ne doit attribuer des prises pendant une déconnexion.

Le passage en automatique peut être demandé pendant une prise active, mais ne s’applique qu’après sa résolution. Un seul mode contrôle la tentative. Arrêter ou mettre en pause lorsque l’outil est retiré, les appâts sont épuisés, la place manque ou le personnage n’est plus admissible. Afficher le motif et permettre un redémarrage cohérent après correction.

Les créatures légendaires sont des prises rares configurées par zone et outil, avec une lutte plus exigeante et de petits défis. L’auteur choisit la difficulté, le nombre d’étapes, le temps de réponse, le visuel et les récompenses. Les consignes doivent être courtes et lisibles : par exemple répondre à une direction ou agir dans une fenêtre de temps. Documenter précisément les types effectivement implémentés dans le rapport final ; un réglage générique ne prouve pas que plusieurs familles de défis existent.

Si une légendaire est rencontrée pendant l’automatique, suspendre l’automatique et proposer une reprise manuelle pendant un délai configurable. Le joueur doit accepter et réussir le défi pour obtenir la créature. Sans réponse à cette invitation issue du mode automatique, la créature fuit sans capture ni XP légendaire et la pêche automatique reprend après 1 500 ms. STOP pendant l’invitation annule cette reprise sans empêcher une acceptation manuelle. Dès que le joueur accepte l’invitation, la lutte, le défi et leur issue restent manuels ; aucune reprise automatique n’a lieu après réussite ou échec. Une invitation issue du mode manuel reste manuelle si elle expire. Ce comportement doit être explicite, testé et visible dans l’éditeur.

Les fenêtres de défi et les étapes sont décidées et validées par le serveur. Le client affiche le défi reçu ; il ne choisit ni la créature, ni la difficulté, ni le résultat. Prévoir une indication visuelle même lorsque tous les sons sont désactivés.

## 6. Ressources graphiques et conservation des originaux

Les sources utilisateur doivent être conservées octet pour octet. Les recadrages et index de sprites sont des données d’utilisation ; ils ne justifient pas de réécrire les planches. Le [manifeste des ressources](../../resources/fishing/assets-manifest.json) conserve les chemins, dimensions et empreintes disponibles.

### Inventaire de tous les fichiers reçus

| Fichier reçu | Destination ou correspondance | Rôle |
| --- | --- | --- |
| `Item_Sheet_Fishing.png` | `resources/fishing/items/` | Poissons, outils, hameçons et accessoires, 640 × 192 |
| `Item_Sheet_Flora&Fauna_1.png` | `resources/fishing/items/` | Crustacés et autres icônes, 160 × 128 |
| `Water_Background1.png` | `resources/fishing/backgrounds/` | Composition avec colonne, épave, coraux et sable, 1920 × 1360 |
| `Water_Background2.png` | `resources/fishing/backgrounds/` | Composition verticale surface/profondeur, 907 × 2048 |
| `Water_Background3.png` | `resources/fishing/backgrounds/` | Variante sous-marine avec épave, 1920 × 1080 |
| `Water_Background4.png` | `resources/fishing/backgrounds/` | Variante horizontale ciel, mer et épave, 1920 × 1080 |
| `Water_Background5.png` | `resources/fishing/backgrounds/` | Bande de sable avec transparence, 1920 × 1080 |
| `Fishing_Net.zip` | [Archive source conservée](source/Fishing_Net.zip) | Neuf planches d’animation de filet |
| `image(20261103-030337).png` | [Référence approuvée](references/gameplay-approved.png) | Capture de maquette fournie, 1670 × 941 |
| `image-edit-target-271c79af201cc68c.png` | Même contenu que `Item_Sheet_Fishing.png` | Copie de sélection d’image ; même SHA-256, aucun nouveau sprite |

Les fonds 1 à 4 sont des **variantes composites**, pas quatre calques transparents à empiler. Le cinquième fournit une véritable transparence au-dessus du sable. Ne pas traiter le blanc de prévisualisation comme une couleur à rendre. Préserver les proportions et prévoir un cadrage adapté au format vertical.

### Contenu exact de Fishing_Net.zip

| Fichier dans `resources/fishing/paperdolls/` | Dimensions |
| --- | --- |
| `B_Catch_1H_Tools_Fishing_Net-sheet.png` | 768 × 512 |
| `B_Fishing_1H_Tools_Fishing_Net.png` | 512 × 512 |
| `B_Move_1H_Tools_Fishing_Net.png` | 768 × 512 |
| `B_Throw_1H_Tools_Fishing_Net.png` | 768 × 512 |
| `F_Catch_1H_Tools_Fishing_Net.png` | 768 × 512 |
| `F_Fishing_1H_Tools_Fishing_Net.png` | 512 × 512 |
| `F_Idle_1H_Tools_Fishing_Net.png` | 512 × 512 |
| `F_Move_1H_Tools_Fishing_Net.png` | 768 × 512 |
| `F_Throw_1H_Tools_Fishing_Net.png` | 768 × 512 |

### GFX complémentaires demandés

La livraison graphique complémentaire prévoit `resources/fishing/gui/fishing_shell.png`, `resources/fishing/gui/fishing_components.png` et `resources/fishing/fx/fishing_effects.png`. Une couche complémentaire `resources/fishing/gui/coral_foreground.png` fournit les coraux transparents. La coque reprend le cadre bois/laiton ; les composants couvrent boutons, états, barres, cases et indicateurs ; les effets couvrent ondulations, éclaboussures, bulles et signal légendaire. Les rectangles des atlas et les états doivent être décrits dans leur manifeste et contrôlés à l’importation.

Vérifier leur présence, leur alpha et leur utilisation dans le rapport final. Ne pas annoncer un raccordement uniquement parce que le PNG existe. Les libellés, nombres, barres variables et traductions sont rendus par le code ; ne pas les figer dans les textures. Aucun nouveau sprite de boss légendaire n’est promis : utiliser les créatures disponibles ou enregistrer séparément une demande graphique précise.

## 7. Animation de l’avatar dans le monde

Le joueur conserve son apparence, ses vêtements et ses couches d’équipement. L’animation de filet concerne cet avatar dans le monde ; elle ne recrée pas un personnage dans la fenêtre de pêche.

L’audit montre une grille cohérente de cellules 128 × 128 : six colonnes pour Throw/Catch/Move et quatre pour Fishing/Idle, sur quatre rangées. Les fichiers B remplissent la dernière rangée, tandis que les F correspondants remplissent les trois premières. Cela indique probablement des couches **Back/Front**, à confirmer dans l’importeur ; ne pas interpréter B/F comme masculin/féminin. F_Idle remplit les quatre rangées, donc l’absence de B_Idle n’est pas nécessairement une anomalie.

Confirmer les directions, points d’ancrage, FPS et animations corporelles compatibles dans le moteur. Conserver les marges transparentes. La séquence visée est lancer, attendre/utiliser, récupérer puis repos. Un échec ne joue pas la récompense de capture. Les autres joueurs doivent voir un état cohérent ; une animation locale ou un simple effet positionné ne suffit pas à valider le système de paperdolls.

## 8. Contrat audio et langues

Le [contrat audio](Audio.fr.md) décrit les 32 WAV et les 58 bindings du pack. Tous les sons demandés doivent pouvoir être remplacés ou désactivés : interface, outil, eau, ligne, compétences, auto, légendaires, progression et ambiance. Les surcharges comportent quatre portées : global, zone, outil et prise. La résolution applique la portée la plus précise, puis le défaut du catalogue si aucune valeur n’est définie.

Le nombre de bindings du pack ne prouve pas le nombre de hooks raccordés. Vérifier chaque événement à l’exécution, les arrêts de boucle et l’absence de double lecture locale/réseau. Les alertes de gameplay disposent aussi d’une représentation visuelle.

Conformément à `AGENTS.md`, tous les nouveaux textes destinés aux joueurs existent en anglais, français et espagnol. Utiliser les catalogues de localisation et de traduction de contenu, conserver les paramètres de substitution, tester le repli anglais et ne jamais traduire les identifiants de ressources.

## 9. Plan de travail et critères de recette

### Lot 1 — Données et éditeur

Relier la profession et les objets existants, enregistrer les zones, exposer les compatibilités et valider les références. Créer une configuration de recette avec au moins une canne, une cage et un filet, puis des prises exclusives à chaque outil et une prise commune à deux outils. Désactiver les exemples non liés à des GUID réels.

La sauvegarde puis réouverture doit préserver toutes les valeurs. Tester duplication, suppression référencée, noms traduits, table vide et erreur de ressource. Les erreurs doivent être compréhensibles et ne pas corrompre la dernière configuration valide.

### Lot 2 — Serveur et récompenses

Raccorder la commande Event, les contrôles d’admissibilité et les états de session. Tester outil simplement possédé, outil équipé incompatible, appât absent, hors-zone, changement de carte et déconnexion. Chaque refus conserve les ressources qui ne doivent pas être consommées.

Vérifier l’intersection zone/outil/conditions avec des tables déterministes, puis le tirage pondéré sans prétendre démontrer une distribution exacte par quelques captures. Rejouer une demande de récompense et une étape de défi : aucun objet ni XP supplémentaire ne doit apparaître. Tester pile existante, pile pleine, dernière case libre et capacité modifiée pendant la lutte.

### Lot 3 — Client, commandes et ressources

Afficher les véritables données de session, les icônes choisies et les nombres d’inventaire. Tester les quatre compétences, leurs recharges, l’arrêt de remontée et les messages de résistance. Contrôler les sprites par rectangle source et le cadrage des cinq fonds sans déformer les images.

Sur Windows, lancer le client graphique et l’éditeur, vérifier plusieurs résolutions et facteurs d’échelle, le focus clavier, les interactions souris, la fermeture et la réouverture. Comparer la scène à la référence approuvée : absence de personnage/quai, lisibilité, cadre bois/laiton, valeurs dynamiques. Une compilation seule ne couvre pas ces critères.

### Lot 4 — Auto, légendaires et multijoueur

Tester une série automatique jusqu’à épuisement d’appât, remplissage d’inventaire et interruption. Déconnecter puis reconnecter : aucune prise hors ligne. Faire apparaître une légendaire en auto, accepter, réussir, échouer et laisser expirer l’invitation. Vérifier la consommation, les récompenses et le mode après chaque issue.

Avec deux clients, observer le filet sur l’avatar réel, les directions, les couches devant/derrière, les changements de tenue et la fin de l’animation. Vérifier qu’un joueur ne pilote pas la session d’un autre et que ses sons locaux de défi ne deviennent pas une alerte globale. Tester latence, paquets répétés et fermeture pendant une boucle sonore.

### Lot 5 — Livraison vérifiable

Contrôler les empreintes des sources, les atlas, les chemins copiés au client, les traductions et les réglages audio. Écouter les effets dans le jeu : l’analyse numérique des WAV ne remplace pas le mixage. Fournir les commandes exécutées, résultats, captures pertinentes et limites restantes.

Le rapport final doit séparer : ressources présentes, fonctions raccordées, tests automatisés réussis, essais Windows effectués et scénarios multijoueurs réellement validés. Toute fonction non éprouvée reste explicitement à vérifier. Le mandat demande un système exploitable et configurable ; il n’autorise pas à présenter une maquette ou une intégration partielle comme une livraison terminée.
