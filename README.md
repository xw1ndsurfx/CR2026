# Corps Royaux — Logiklik 2D Engine

**Corps Royaux** est un MMORPG 2D de fantasy médiévale développé par **Studio Logiklik**. Son moteur, **Logiklik 2D Engine**, est une version fortement personnalisée de l'open source **Intersect Engine**. Ce dépôt contient le code du jeu, du client, de son serveur et de ses outils de création.

[![Development](https://img.shields.io/badge/Status-En%20d%C3%A9veloppement-orange)](ROADMAP.md)
[![.NET 8](https://img.shields.io/badge/.NET-8-512bd4)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Steam](https://img.shields.io/badge/Steam-Corps%20Royaux-1b2838)](https://store.steampowered.com/app/4317960/)
[![Roadmap](https://img.shields.io/badge/Roadmap-2026%E2%80%932027-blue)](ROADMAP.md)

> **État au 2 novembre 2026 : développement actif, playtests en préparation.** La présence d'une fonctionnalité dans le code ne garantit pas sa validation complète en jeu ou en production.

## Corps Royaux : univers et objectifs

Dans un univers médiéval fantastique, le royaume de **Polonya** résiste à l'invasion d'**Herbonia**. Corps Royaux vise un monde persistant où les joueurs explorent, combattent, accomplissent des quêtes, forment des groupes, perfectionnent leurs métiers et découvrent des activités coopératives ou compétitives.

- **Genre :** MMORPG 2D fantasy médiéval
- **Studio :** Studio Logiklik
- **Moteur / éditeur :** Logiklik 2D Engine (issu d'Intersect)
- **Steam App ID principal :** 4317960
- **Langues joueurs :** français, anglais et espagnol (règles dans [AGENTS.md](AGENTS.md))
- **Documentation développeurs :** [dev.corpsroyaux.com](https://dev.corpsroyaux.com)
- **Wiki du jeu :** [wiki.corpsroyaux.com](https://wiki.corpsroyaux.com)
- **Calendrier et jalons de validation :** [ROADMAP.md](ROADMAP.md)

## Logiklik 2D Engine : présentation

Le **Logiklik 2D Engine** s'appuie sur les fondations du moteur Intersect, tout en développant ses propres éditeurs de contenu, systèmes MMORPG, interfaces, outils de création et intégrations réseau. Il ne s'agit **pas** d'un moteur entièrement écrit à partir de zéro.

### Outils de création et d'édition

| Outil | Fonctions observées dans le dépôt | Documentation |
| --- | --- | --- |
| **Editor Hub** | Fenêtre de lancement centralisée, recherche, catégories, fiches illustrées | [Editor Hub](Documentation/EditorHub/README.txt) |
| **Éditeurs de jeu** | Événements, NPC, classes, objets, sorts, projectiles, quêtes, ressources, artisanat et magasins | [Code des éditeurs](Intersect.Editor/Forms/Editors) |
| **Smart Map Builder** | Outils de peinture, formes, remplissage, modèles et couches de carte | [Notes](docs/SmartMapBuilder.txt) |
| **Royal Cinematic Director** | Édition de scènes, caméra, timeline, mouvements, dialogues et séquences interactives — **validation de certains parcours à poursuivre** | [Architecture](Documentation/RoyalCinematicDirector/Architecture.md) |
| **Character Generator / imports** | Génération de personnages, export des animations, import de sprites et animations | [Code de l'éditeur](Intersect.Editor/Forms) |
| **Outils de création automatique** | Aides à la création de NPC, objets, sorts et projectiles; outils d'équilibrage | [Code de l'éditeur](Intersect.Editor/Forms/Editors) |
| **Éditeurs spécialisés** | Donjons, invasions, boss mondiaux, naval, montures, joutes, contrats, maisons, récompenses, métiers et boutiques | [Code](Intersect.Editor/Forms/Editors) |

### Systèmes de jeu présents dans le code

Ces systèmes sont représentés dans les sources client/serveur; leur niveau de maturité et leur disponibilité en jeu diffèrent :

- **Progression et combat :** expérience, professions, artisanat, maîtrises d'armes, PvE/PvP, récompenses, succès, quêtes et boutiques de quêtes.
- **Multijoueur :** groupes, guildes, donjons et instances, invasions, boss mondiaux et activités coopératives.
- **Économie :** banques, marketplace joueur, magasins, services et intégration LogiCoins.
- **Monde et personnages :** exploration et carte du monde, naval et canons, montures, familiers, maisons et mobilier.
- **Mini-jeux :** blackjack, poker, cuisine, potions, crochetage, roulette et archerie.
- **Systèmes spéciaux :** Royal Jousting, Royal Contracts, Royal Stylist, tutoriel de première connexion et Royal Cinematic Director.
- **Services et intégrations :** Steamworks.NET selon build, serveur web/API et pont Logiklik.

**Modules à ne pas considérer comme finalisés sans nouveaux essais :** joutes, compétition d'archerie, montures, cinématiques avancées et scénarios navals. La documentation expose les limites et validations encore nécessaires.

Références de suivi : [Royal Combat / Archery](Documentation/RoyalCombat/README.md) · [Joutes](Documentation/Jousting/AUDIT.md) · [Cinématiques](Documentation/RoyalCinematicDirector/Progress.txt) · [Montures](docs/mount-prototype.md) · [Maisons](Documentation/features/housing.md) · [Familiers](Documentation/features/pets.md) · [Contrats royaux](Documentation/RoyalContracts/Usage.fr.md) · [Tutoriel](docs/tutorial-guide.md) · [Diagnostics serveur](docs/server-crash-diagnostics.md).

## Architecture du moteur

La solution [Intersect.sln](Intersect.sln) utilise **C# / .NET 8**.

| Composant | Projet | Rôle |
| --- | --- | --- |
| **Éditeur Logiklik 2D Engine** | [Intersect.Editor](Intersect.Editor) | Application Windows WinForms, DarkUI/DockPanel, MonoGame WindowsDX, édition de contenu |
| **Client** | [Intersect.Client](Intersect.Client) et [Intersect.Client.Core](Intersect.Client.Core) | Joueur, interfaces, rendu 2D MonoGame DesktopGL, logique client |
| **Serveur** | [Intersect.Server](Intersect.Server) et [Intersect.Server.Core](Intersect.Server.Core) | Simulation, joueurs, réseau, sauvegardes, API ASP.NET Core |
| **Bibliothèques partagées** | [Intersect.Network](Intersect.Network), [Intersect (Core)](Intersect%20(Core)), [Shared](Shared) | Protocoles, modèles et rendu partagé |
| **Outils, tests et automatisation** | [Framework](Framework), [scripts](scripts), [.github/workflows](.github/workflows) | Compilation, packaging, tests automatisés et diagnostics |
| **Documentation** | [Documentation](Documentation), [docs](docs) | Guides de modules, utilisation, validation et limites |

Le serveur s'appuie notamment sur EF Core et des fournisseurs de stockage documentés dans le dépôt; le client comprend une intégration optionnelle à Steamworks.NET. Maintenir des versions **compatibles du client, serveur et éditeur** lors des modifications réseau. Ne jamais placer des secrets Logiklik, Steam ou des clés privées dans un client distribué.

### Développement et compilation

Prérequis : **.NET 8 SDK**, Git et sous-modules; **Windows pour l'éditeur**. Voir [REQUIREMENTS.md](REQUIREMENTS.md).

Cloner puis initialiser les sous-modules :

~~~bash
git clone https://github.com/xw1ndsurfx/CR2026.git
cd CR2026
git submodule update --init --recursive
~~~

Exemple de compilation Debug :

~~~bash
dotnet build -p:Configuration=Debug -p:PackageVersion=0.8.0-beta -p:Version=0.8.0
~~~

Exemple de publication du projet serveur pour Linux x64 :

~~~bash
dotnet publish "Intersect.Server/Intersect.Server.csproj" -c Release -r linux-x64 -p:PackageVersion=0.8.0-beta -p:Version=0.8.0
~~~

Ces commandes décrivent une procédure, **pas une compilation validée pour la dernière révision**. Les clés réseau, ressources et configurations de l'environnement sont nécessaires; les builds non Windows peuvent nécessiter [disable-windows-only.patch](disable-windows-only.patch). Le dépôt dispose également des scripts [build-steam-client.cmd](scripts/build-steam-client.cmd), [prepare-steam-runtime.ps1](scripts/prepare-steam-runtime.ps1) et [start-server-diagnostics.sh](scripts/start-server-diagnostics.sh).

## Roadmap de tests et lancement 2026–2027

**Le calendrier est prévisionnel** et soumis à la stabilité, à l'admissibilité Steam et à la décision de Studio Logiklik. [Voir tous les critères et livrables dans ROADMAP.md](ROADMAP.md).

| Date visée | Étape | Objectif |
| --- | --- | --- |
| **14–15 novembre 2026** | **Steam Playtest fermé #1** | 20–50 joueurs ciblés; connexion, personnages, combats, serveur |
| **7 décembre 2026** | **Steam Playtest communautaire #2** | 100–300 joueurs ciblés, accès progressif; systèmes multijoueurs validés |
| **Janvier 2027** | **Playtest #3** | Test de charge, persistance et correctifs |
| **22 février–1er mars 2027** | **Steam Next Fest envisagé** | Démo publique spécifique, si admissible et approuvée par Valve |
| **Mars 2027** | **Open BETA envisagée** | Lancement plus large après revue de stabilité et du Next Fest |

**Important :** un Steam Playtest ne constitue ni la publication du jeu principal, ni la démo exigée pour Steam Next Fest. Les dates du jeu principal dans Steamworks sont distinctes. La présence de ce calendrier dans le dépôt **ne signifie pas que les événements Steamworks sont déjà créés**.

## Contribuer et signaler un problème

- Respecter [AGENTS.md](AGENTS.md) : toute nouvelle interface joueur doit disposer de ses versions **EN/FR/ES**.
- Suivre les recommandations de [CONTRIBUTING.md](CONTRIBUTING.md).
- En cas de bogue, fournir le scénario de reproduction, la révision et les journaux sans secrets; différencier un résultat de test automatisé d'une vérification multijoueur en jeu.
- Lire [LICENSE.md](LICENSE.md) et les licences des sous-projets. Les obligations et attributions héritées d'Intersect sont conservées.

## Crédits et héritage open source

**Logiklik 2D Engine** est une personnalisation de **[Intersect Engine](https://github.com/AscensionGameDev/Intersect-Engine)** par Ascension Game Dev. Il utilise notamment MonoGame et d'autres bibliothèques tierces, chacune soumise à ses propres licences. Voir [AUTHORS.md](AUTHORS.md), [LICENSE.md](LICENSE.md) et les licences présentes dans les répertoires.

- [Documentation officielle du moteur d'origine](https://docs.freemmorpgmaker.com)
- [Communauté Ascension Game Dev](https://ascensiongamedev.com)
- [Matrice des plateformes](REQUIREMENTS.md)

---

*Corps Royaux et les extensions Logiklik 2D Engine sont développés par Studio Logiklik. Les fonctionnalités et dates restent sujettes à validation.*
