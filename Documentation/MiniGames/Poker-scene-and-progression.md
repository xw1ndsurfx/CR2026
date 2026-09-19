# Poker : scene 2D et progression par mini-jeu

Branche de developpement `feature/poker-minigame-core`, PR en brouillon.
**Jetons de test et progression de test uniquement. Aucun Aureon preleve ou verse.**

## Scene sans cadre de fenetre

Le client utilise maintenant une scene Gwen `Base`, et non `WindowControl`. La barre de titre et
le cadre ont disparu. Une table ovale en bois, des chaises et des personnages de remplacement sont
dessines en 2D; le jeu reste visible, assombri, derriere la scene. Le joueur local est toujours en bas,
avec les autres places dans l'ordre relatif autour de la table. Le pot et les cartes communes sont
au centre; les mises, boutons, experience et selection des dos occupent le bas de l'ecran.

Ce n'est pas une scene 3D TLOPO ni une camera dans une taverne. Aucun PNJ de carte n'est cree ou deplace
automatiquement. Les personnages affiches appartiennent a la presentation du mini-jeu. Les pions
proceduraux sont des remplacements simples : les illustrations definitives restent a fournir.
Les controles utilisent des positions adaptees a la taille du canevas; la validation visuelle avec
les vraies ressources, polices et resolutons du jeu reste necessaire.

## PNJ et decisions visibles

Le croupier s'appelle **Marlow**. Les places d'adversaires sont attribuees a **Nora, Silas, Iris,
Bastien et Oren**, selon le nombre configure dans l'evenement. Ces noms sont actuellement fixes dans
`Framework/Intersect.Framework.Core/MiniGames/PokerTableTheme.cs`, pas editables dans le dialogue.

La derniere decision de chaque participant et les trois dernieres actions de la table sont affichees :
distribution, se coucher, passer, suivre, relancer, depart et victoire nette. Un asterisque indique une
action automatique signalee par le serveur. Le journal public transporte au maximum 18 entrees,
sans les cartes privees des adversaires. Le nom du participant actif est mis en evidence.

Les gains des PNJ sont affiches localement a la table. Le chat global conserve l'option precedente,
reservee aux gains nets positifs des joueurs humains. Les animations de distribution et de victoire
restent deux reglages independants; la celebration privee ne s'affiche que chez le gagnant humain.

## Experience et niveaux

Le serveur attribue **25 XP par main terminee avec un benefice net positif**, une seule fois par
personnage et par main. Une defaite, une restitution de mise ou une egalite sans benefice n'attribue
pas d'XP de victoire. Les PNJ ne gagnent pas la progression d'un joueur humain.

La progression est indexee par identifiant de personnage ET cle de mini-jeu (`poker`). Elle ne modifie
pas le niveau RPG, les statistiques, les objets ou l'inventaire. Le meme stockage peut accueillir
un autre mini-jeu sous une autre cle, mais le blackjack ou d'autres jeux ne sont pas ajoutes ici.

Le niveau commence a 1 et va jusqu'a 25. Le seuil d'XP cumulee du niveau L est `50 * L * (L - 1)`.
L'XP est plafonnee a 30 000; le nombre de victoires peut continuer d'augmenter apres le niveau 25.
La barre affiche l'avancement dans le niveau courant et un texte apparait lors d'une montee de niveau.
Cet equilibrage est propre a ce prototype, pas une reproduction verifiee de la courbe TLOPO.

## Dos B1 a B6

Placer les fichiers dans le **client**, directement sous `resources/misc`, puis redemarrer :

| Fichier | Niveau requis | XP cumulee | Victoires a 25 XP |
| --- | ---: | ---: | ---: |
| B1.png | 1 | 0 | 0 |
| B2.png | 5 | 1 000 | 40 |
| B3.png | 10 | 4 500 | 180 |
| B4.png | 15 | 10 500 | 420 |
| B5.png | 20 | 19 000 | 760 |
| B6.png | 25 | 30 000 | 1 200 |

Le bouton Card backs ouvre les six apercus avec leur niveau requis. Les boutons verrouilles sont
grises; le serveur refuse aussi une requete forgee pour un dos non debloque. Le choix est sauvegarde
par personnage. Les deux cartes cachees utilisent le meme dos, visible aux autres participants.
Un changement pendant une main s'applique a la suivante, sans modifier les cartes ou les mises.

Une image absente utilise `B1.png`, puis l'ancien `back.png`, puis le texte si aucune image n'existe.
Les faces gardent les noms `AC.png`, `TD.png`, etc. Les anciennes variantes back_royal/back_pirate/
back_halloween ne sont plus le catalogue actif. L'option Dealer / NPC card back accepte B1 a B6;
le niveau du joueur ne limite pas le modele choisi pour les PNJ par le concepteur de la table.

Aucune illustration n'est incluse dans cette modification. Garder les memes dimensions et contours
pour les dos et les faces; 48 x 64 pixels reste un format de dessin propose.

## Illustrations facultatives des participants

Le client cherche aussi, directement dans `resources/misc` :

- `poker_dealer.png` : Marlow.
- `poker_npc_1.png` a `poker_npc_5.png` : Nora, Silas, Iris, Bastien et Oren.
- `poker_player.png` : illustration generique des participants humains.

Il s'agit de PNG statiques, proportionnes dans un cadre de 64 x 74 pixels de reference, pas de feuilles
de sprites animees. Sans ces fichiers, les petits personnages proceduraux restent visibles.
Le visuel humain n'utilise pas encore l'apparence/paperdoll personnelle de chaque personnage.
Le fond de table est actuellement dessine par le code; aucun fichier poker_table.png n'est requis.

## Sauvegarde et limites de recuperation

Le serveur cree automatiquement **`resources/minigames-test.db`**, relatif a son dossier de travail.
Il s'agit d'une base SQLite distincte, sans migration de la base des personnages. Les profils contiennent
l'XP, le nombre de victoires et le dos selectionne. Quitter la table, se reconnecter ou redemarrer le
serveur conserve les donnees deja enregistrees.

Un recu unique (personnage, mini-jeu, instance de table, numero de main) et l'increment d'XP sont valides
dans la meme transaction. Un doublon reseau ou une nouvelle tentative apres un resultat incertain ne
credite pas deux fois la meme victoire. Les dos sont verifies avant leur sauvegarde.

Si le profil ne peut pas etre lu, l'entree est refusee plutot que de remplacer le profil par un niveau 1.
Si l'XP ne peut pas etre sauvegardee, la table attend et le serveur retente avec le meme recu. Les sommes
de jetons, les mains en cours et les demandes d'XP non encore validees restent en memoire : une coupure
avant la transaction peut perdre la derniere attribution. Cette version ne pretend pas recuperer une
partie en cours apres un crash. La base n'est pas un portefeuille d'Aureons.

Sauvegarder la base et les ressources de developpement **serveur arrete**. Ne pas supprimer cette base
pour mettre a jour les executables. Ne pas copier la progression acquise avec ces jetons renouvelables
vers un futur environnement de production; il n'existe pas de migration automatique test -> production.
Les limites anti-collusion, le financement des PNJ et l'economie Aureons restent un chantier separe.

## Recuperer et verifier

Fermer les applications pour eviter de verrouiller les executables de sortie. Sauvegarder les changements
locaux, puis depuis la racine de CR2026 :

```powershell
git fetch origin
git switch feature/poker-minigame-core
git pull --ff-only origin feature/poker-minigame-core
git submodule update --init --recursive

dotnet build Intersect.Server/Intersect.Server.csproj --configuration Debug
dotnet build Intersect.Client/Intersect.Client.csproj --configuration Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug
```

Client, serveur, editeur ET leurs DLL doivent provenir du meme commit : des champs de protocole ont ete
ajoutes. L'evenement Poker existant ouvre la nouvelle scene sans devoir etre recree. Les evenements qui
accedent a une meme table doivent toujours partager leurs reglages. La nouvelle XP commence au premier
lancement de cette version : aucune victoire de l'ancien prototype n'est reconstruite retrospectivement.

Tests automatiques dedies :

```powershell
dotnet run --project Utilities/PokerSmokeTests/Intersect.PokerSmokeTests.csproj -c Release
dotnet run --project Utilities/PokerNetworkTests/Intersect.PokerNetworkTests.csproj -c Release
dotnet run --project Utilities/PokerProgressTests/Intersect.PokerProgressTests.csproj -c Release
dotnet run --project Utilities/PokerUiTests/Intersect.PokerUiTests.csproj -c Debug
```

La suite de persistence utilise une vraie base SQLite temporaire : redemarrage, concurrences, transaction
interrompue, recu repete, profil invalide et repertoire inaccessible. Elle ne touche pas aux donnees du jeu.
Les controles de scene utilisent le vrai code Gwen avec des metriques de police synthetiques. Ils ne
remplacent pas la verification graphique manuelle avec deux executables.

Essai manuel attendu : jouer contre Marlow et des invites; observer leurs noms, leurs decisions et un
resultat PNJ; gagner une main et constater +25 XP; quitter/revenir puis redemarrer pour verifier la
sauvegarde. Confirmer B1 disponible et B2-B6 verrouilles au niveau 1, et le debloquage de B2 au niveau 5.
Deux clients doivent voir les dos autorises de l'autre sans recevoir ses cartes privees. Refaire les tests
de partage de pot, Refresh, depart/tapis, animations et redimensionnement. Ne pas fusionner dans main
avant cette validation visuelle et fonctionnelle.
