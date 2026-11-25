# Quartier général de guilde

Version de développement du 21 novembre 2026, sur `codex/guild-headquarters`, issue de `main` après la PR 277. Pas de déploiement dans le serveur ou les clients installés. Le client privé Intel HD 3000 reste séparé.

## Fonctionnement

Le bouton **Quartier général de guilde** du menu Guilde permet aux membres d’entrer dans une salle privée partagée. Chaque guilde conserve son identifiant d’instance. Le coffre est le coffre de guilde existant, avec ses permissions habituelles ; il faut se tenir près de son emplacement configuré pour l’ouvrir.

Les responsables autorisés améliorent la salle avec des ressources déposées dans le coffre. Une amélioration peut changer l’intérieur, imposer un effectif minimum et augmenter la capacité du coffre. Les ressources et le palier sont enregistrés ensemble. Un double clic ou une proposition devenue périmée ne peut pas acheter deux fois la même amélioration. L’interface demande une confirmation avant la dépense.

Les visiteurs reçoivent une invitation nominative à accepter, valable deux minutes. La visite dure au maximum 30 minutes par défaut, réglables de 1 à 120 minutes. Un visiteur ne peut ouvrir aucune banque, améliorer le QG ni inviter d’autres visiteurs. La déconnexion de l’hôte, sa sortie de la guilde, la perte de son droit d’invitation, un blocage entre les deux joueurs, l’expiration ou la révocation mettent fin à la visite. Limite : 20 visiteurs actifs et 20 invitations en attente par guilde. Une personne sans guilde peut être invitée.

Le lieu de retour est enregistré avant l’entrée. Après une reconnexion ou un redémarrage, le personnage retrouve l’extérieur ; il doit entrer à nouveau ou recevoir une nouvelle invitation. Si le lieu de retour n’existe plus, le serveur cherche une position extérieure praticable. Les données de retour ne constituent jamais une autorisation d’entrée.

## Configuration dans l’éditeur

1. Préparer une **carte intérieure isolée par palier**, sans liens entrants ou sortants avec les autres cartes. Utiliser des cartes dédiées au QG, sans point de départ de classe, événement de téléportation automatique ou autre système d’instance. Les visuels fournis ci-dessous habillent l’interface ; ils ne construisent pas les cartes intérieures à la place du concepteur.
2. Ouvrir **Quartier général de guilde** depuis le hub ou les éditeurs de contenu.
3. Ajouter le premier palier : nom, description, carte, case d’entrée, emplacement du coffre et icône. Le premier palier est gratuit et préserve la capacité actuelle du coffre.
4. Ajouter les paliers suivants, par exemple Pavillon → Manoir → Citadelle. Choisir une ressource empilable autorisée dans le coffre de guilde, son coût, l’effectif minimum et la capacité minimale du coffre après amélioration. Les identifiants et l’ordre des paliers enregistrés sont conservés ; de nouveaux paliers peuvent être ajoutés à la fin, jusqu’à 12.
5. Régler les rangs autorisés à améliorer et inviter : **0 = chef**, les nombres plus petits correspondent aux rangs les plus élevés. Activer les visiteurs et régler leur durée maximale.
6. Dans **Images et sons** (selon la traduction de l’éditeur), choisir les PNG, WAV et OGG, ainsi que les volumes. Un choix vide désactive l’élément. Les sélections personnalisées sont préservées même si le fichier manque sur la machine de l’éditeur.
7. Activer le système, enregistrer et attendre la confirmation du serveur. En cas d’absence de confirmation, rouvrir l’éditeur pour relire la configuration réellement enregistrée avant une nouvelle tentative.

La configuration est conservée dans `resources/guild-headquarters.json` sur le serveur. Elle est **désactivée par défaut**. Les noms et descriptions saisis suivent le système de traduction de contenu mis en cache. Les commandes et messages système disposent des trois langues EN/FR/ES.

## Pack graphique et sonore

Source des médias : `Intersect.Client/resources/`. Les compilations client et éditeur incluent les 15 fichiers dans leur dossier `resources`, grâce à `Shared/GuildHeadquartersAssets.props`. L’ajout à une installation existante consiste à copier ces seuls fichiers dans les mêmes sous-dossiers ; aucun ancien média n’est supprimé.

| Dossier | Fichiers | Usage |
| --- | --- | --- |
| `gui` | `guild_hq_hall_v1.png` | Salle royale, bandeau de l’interface |
| `gui` | `guild_hq_lodge_v1.png`, `guild_hq_manor_v1.png`, `guild_hq_citadel_v1.png` | Trois icônes transparentes de progression |
| `gui` | `guild_hq_bank_v1.png`, `guild_hq_invitation_v1.png` | Coffre et invitation |
| `music` | `guild_hq_theme_v1.ogg` | Thème original de 48 secondes, stéréo, en boucle |
| `sounds` | `guild_hq_hearth_v1.wav` | Ambiance de cheminée de 24 secondes, en boucle |
| `sounds` | `guild_hq_open_v1.wav`, `guild_hq_enter_v1.wav`, `guild_hq_leave_v1.wav` | Fenêtre et passages |
| `sounds` | `guild_hq_bank_v1.wav`, `guild_hq_invitation_v1.wav`, `guild_hq_upgrade_v1.wav`, `guild_hq_error_v1.wav` | Coffre, invitation, amélioration et refus |

Images créées pour ce module avec l’outil de génération d’images : [prompts et provenance](GuildHeadquarters-ArtPrompts.json). Audio synthétisé localement, sans échantillon externe, par `Utilities/GuildHeadquartersAssets/generate_audio.py` (Python, numpy et soundfile). WAV mono PCM 16 bits et OGG Vorbis stéréo, 44,1 kHz. [Dimensions, durées, mesures et empreintes des fichiers](GuildHeadquarters-Assets.json).

Le thème respecte le volume musical du joueur ; les musiques de combat/invasion gardent leur priorité. L’ambiance s’arrête à la sortie et à la déconnexion. Un thème absent laisse fonctionner la musique de carte ; les images absentes ne bloquent pas les commandes.

## Sauvegarde et validation

Migrations SQLite et MySQL `20261121010000_GuildHeadquarters` : deux colonnes texte facultatives, `Guilds.Headquarters` et `Players.GuildHeadquartersVisit`. Elles préservent les guildes, joueurs et coffres existants. Sauvegarder la base avant le déploiement habituel ; distribuer des versions compatibles du serveur, client et éditeur.

Vérifications automatisées : validation de configuration, ressources et rangs, achats concurrents, refus sans débit, retour arrière après erreur de sauvegarde, migration et transaction SQLite avec échec simulé, invitations expirées/révoquées/à usage unique, récupération à la reconnexion, refus d’accès aux cartes privées, sérialisation des paquets, langues, interface et édition. Les tests d’interface utilisent les vrais contrôles, avec un rendu logiciel aux tailles 640×480, 1280×720 et 1920×1080 ; ils ne remplacent pas un essai GPU en jeu.

Résultats locaux du 21 novembre : 17 tests serveur du QG réussis ; 399 tests de régression naval/déplacements/reconnexion réussis ; interface contrôlée aux trois résolutions dans les trois langues ; éditeur contrôlé dans les trois langues, y compris l’absence de réponse à une sauvegarde. Compilation Release serveur, client et éditeur réussie. Les 15 ressources présentes dans les sorties client et éditeur correspondent aux empreintes du manifeste. La migration MySQL compile ; elle n’a pas été exécutée sur une base MySQL de test dans cette session.

Commandes de vérification :

```text
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter FullyQualifiedName~Headquarters
dotnet run --project Utilities/GuildHeadquartersUiTests/GuildHeadquartersUiTests.csproj -c Release
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Release -p:RuntimeIdentifiers=win-x64 -- --headquarters-only
python Utilities/GuildHeadquartersAssets/validate_assets.py
```

Avant activation publique, recette encore à réaliser avec deux guildes et un visiteur : vérifier l’isolation des salles, l’acceptation/refus/expiration, les droits du coffre, les améliorations en présence de plusieurs joueurs, la sortie par événement, la déconnexion de l’hôte et le redémarrage du serveur. Écouter les boucles et contrôler les volumes sur le client réel. Le profil Intel HD 3000 n’est ni modifié ni incorporé au client normal par ce travail.
