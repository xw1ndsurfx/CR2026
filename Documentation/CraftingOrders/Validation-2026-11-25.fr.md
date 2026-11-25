# Commandes d’artisanat — validation locale du 25 novembre 2026

Branche `codex/crafting-orders-validation`, issue de `main` après la PR 240
(`fce73d3688428ac120ed0323701fae6d48f34adb`). Les clients installés et le serveur
de production ne sont pas modifiés. Le client Intel HD 3000 conserve sa branche
et son installation séparées.

## Corrections

- Le dépôt des commandes, l’inventaire et la banque du compte concerné sont
  enregistrés dans **une transaction de la base joueurs**. Un échec annule aussi
  les changements en mémoire. Les sauvegardes périodiques du compte utilisent
  le même verrou : elles ne peuvent pas enregistrer un prélèvement intermédiaire.
- Une requête de publication répétée avec le même identifiant ne prélève pas
  de nouveaux Aureons. Deux artisans ne peuvent pas accepter la même commande.
- La livraison transfère les exemplaires réels, avec leurs statistiques et
  leurs améliorations de rareté, de sort et d’événement. Les objets équipés ou
  devenus non échangeables restent exclus. Les recettes d’amélioration d’un
  exemplaire existant ne sont pas présentées comme des recettes de création.
- Les remboursements, paiements et objets impossibles à placer restent en
  attente. Le client indique les quantités restantes. La banque respecte son
  ouverture, ses emplacements accessibles et l’autorisation de l’objet.
- Une défaillance de notification après validation ne rembourse pas à tort une
  transaction déjà enregistrée. Les notifications d’inventaire, quêtes et
  événements interviennent après validation.
- Le panneau tient sur les petites résolutions, se redimensionne avec la fenêtre,
  améliore le contraste des champs et conserve un texte lisible sans l’atlas.
  Les doubles clics en attente sont bloqués et les erreurs sont traduites en
  anglais, français et espagnol. Le son de fermeture est maintenant déclenché.
- La compilation de l’éditeur inclut désormais le pack de quatre PNG et dix WAV,
  qui était déjà distribué avec le client. Une vérification des empreintes dans
  les deux sorties empêche de livrer un pack absent ou incomplet.

## Données et migration

La migration `20261125010000_CraftingOrdersLedger` ajoute une table à la base
joueurs SQLite ou MySQL. À la première utilisation, l’ancien fichier
`resources/crafting-orders-state.json` est importé si aucun registre n’existe
encore dans la base. L’import et la récupération d’un éventuel paiement font
partie de la même transaction. Un contenu invalide bloque l’opération sans
remettre silencieusement les commandes à zéro.

Le fichier historique est conservé, mais **il ne reçoit plus les transactions
nouvelles**. Un retour à l’ancien serveur exige donc la restauration coordonnée
de la base joueurs et du fichier de commandes depuis la même sauvegarde
antérieure à la migration. Ne pas simplement relancer un ancien exécutable avec
ce fichier devenu obsolète. Client et serveur doivent être livrés ensemble pour
les nouveaux champs du protocole.

## Vérifications effectuées

- 19 tests ciblés réussis sur des sauvegardes SQLite de la copie isolée restaurée :
  publication, acceptation concurrente, livraison, conservation des propriétés,
  annulation, expiration, erreurs injectées après écriture et avant validation,
  récupération différée, banque, import de l’ancien fichier et visibilité des
  commandes actives malgré un long historique.
- Suite serveur complète : 832 tests réussis, dont les 19 tests ciblés. Les
  scénarios optatifs dépendant d’installations externes non fournies ne sont pas
  assimilés à des recettes connectées réussies.
- Suite client complète : 352 tests réussis et six scénarios dépendant de médias
  externes ignorés. Aucun échec. Compilations du client standard et de l’éditeur
  réussies ; les avertissements existants de l’éditeur restent présents.
- Deux démarrages puis arrêts propres du serveur compilé, sur la restauration
  privée : écoute uniquement sur `127.0.0.1:55463`, migration du registre appliquée
  une seule fois et intégrité SQLite confirmée après chaque arrêt. Aucun client
  n’était connecté durant ce contrôle de démarrage.
- Les tests créent deux ou trois personnages techniques dans une **nouvelle
  copie de la base pour chaque cas**. Ils utilisent réellement `User.Save` et
  les migrations ; ils ne modifient pas les comptes de la base source.
- Neuf parcours des contrôles de production Gwen : EN/FR/ES à 800×600,
  1280×720 et 1600×900. Publication, sélection, actions, erreurs, argent en
  attente, signaux audio, redimensionnement, absence puis retour de l’atlas.
- Quatre PNG et dix WAV existants validés et réutilisés. Dimensions de l’atlas,
  formats PCM, durées et échantillons contrôlés ; aucun fichier muet ou saturé.
  `Shared/CraftingOrdersAssets.props` conserve la copie aux sorties client et
  éditeur. Le contrôle GitHub dédié conserve les captures comme artefacts.

Les captures du banc Gwen utilisent un rendu logiciel et des métriques Windows.
Elles ne prouvent pas le résultat du pilote graphique, de la police native ou
du périphérique audio du client. Les fichiers de données et journaux privés de
la copie isolée restent hors du dépôt.

Les 14 fichiers ont également été comparés par SHA-256 dans les sorties client
et éditeur : aucune absence ni différence. La migration MySQL compile, mais
aucune instance MySQL n’a été démarrée pour cette validation ; les essais de
migration exécutés ici utilisent SQLite.

## Recette connectée restant à effectuer

L’accès automatisé à la fenêtre du client a échoué avec
« Le chemin d’accès spécifié est introuvable ». Aucun essai de deux clients
connectés ni écoute en jeu n’est revendiqué. Le test de livraison prépare un
exemplaire en inventaire ; la fabrication via la table native reste à couvrir
pendant cette recette.

1. Avec deux comptes locaux, ouvrir le tableau par l’événement **Open Crafting
   Orders** ; publier depuis A et accepter depuis B.
2. Fermer le tableau, fabriquer l’objet sur une table puis revenir livrer depuis
   B. Contrôler le paiement et les propriétés reçues par A.
3. Reconnecter les deux personnages, redémarrer proprement le serveur local et
   confirmer le même état, sans deuxième livraison ni deuxième paiement.
4. Répéter avec inventaires pleins, annulation et expiration. Libérer une case
   puis actualiser pour récupérer le dépôt restant.
5. Écouter ouverture, fermeture, sélection, refus, publication, acceptation,
   livraison, annulation et paiement aux volumes habituels.

## Reproduction automatisée

```text
python scripts/crafting-orders/validate_assets.py
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter FullyQualifiedName~CraftingOrders
dotnet run --project Utilities/CraftingOrdersUiTests/CraftingOrdersUiTests.csproj -c Release
```

Pour exercer les migrations sur une restauration privée, définir
`CRAFTING_ORDERS_TEST_DB` vers son fichier `playerdata.db` avant la commande de
tests. Le test ouvre cette source en lecture seule et réalise une sauvegarde
SQLite distincte pour chaque cas. Ne jamais y fournir une base en production.
