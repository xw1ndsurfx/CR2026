# Corps Royaux — Logiklik 2D Engine

**Corps Royaux** est un MMORPG 2D de fantasy médiévale développé par **Studio Logiklik**. Son moteur, **Logiklik 2D Engine**, est une version fortement personnalisée de l'open source **Intersect Engine**. Ce dépôt contient le code du jeu, du client, de son serveur et de ses outils de création.

[![Development](https://img.shields.io/badge/Status-En%20d%C3%A9veloppement-orange)](ROADMAP.md)
[![.NET 8](https://img.shields.io/badge/.NET-8-512bd4)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Steam](https://img.shields.io/badge/Steam-Corps%20Royaux-1b2838)](https://store.steampowered.com/app/4317960/)
[![Roadmap](https://img.shields.io/badge/Roadmap-2026%E2%80%932027-blue)](ROADMAP.md)

> **État au 7 novembre 2026 : développement actif, playtests en préparation.** La présence d'une fonctionnalité dans le code ne garantit pas sa validation complète en jeu ou en production. Voir l'[état de développement vérifié sur GitHub](PROJECT_STATUS.md), qui distingue le travail fusionné des PR ouvertes et des tests manuels restants.

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

La publication locale complète avec `-r all-x64` a été exécutée avec succès le 2026-11-12 : serveur/client Linux, serveur/client/éditeur Windows et APK Android ARM64 signé. Cette validation de compilation et de publication ne constitue pas un test d’exécution de tous les programmes. Les clés réseau, ressources et configurations de l'environnement sont nécessaires; les builds non Windows peuvent nécessiter [disable-windows-only.patch](disable-windows-only.patch). Le dépôt dispose également des scripts [build-steam-client.cmd](scripts/build-steam-client.cmd), [prepare-steam-runtime.ps1](scripts/prepare-steam-runtime.ps1) et [start-server-diagnostics.sh](scripts/start-server-diagnostics.sh).

### Publications Windows, Linux et Android

Exécuter ces commandes depuis la racine du dépôt. Les sorties de bureau ciblent
.NET 8. Android et `all-x64` nécessitent **.NET 10**, le workload Android, le SDK
Android API 36, Java 21 et **PowerShell 7**. La préparation des dossiers de
distribution utilise **Python 3.10 ou plus récent** (bibliothèque standard).

```powershell
# Windows x64 : client, serveur et éditeur
 dotnet publish Intersect.Client/Intersect.Client.csproj -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r win-x64
 dotnet publish Intersect.Server/Intersect.Server.csproj -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r win-x64
 dotnet publish Intersect.Editor/Intersect.Editor.csproj -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r win-x64

# Linux x64 : client et serveur
 dotnet publish Intersect.Client/Intersect.Client.csproj -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r linux-x64
 dotnet publish Intersect.Server/Intersect.Server.csproj -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r linux-x64

# Android ARM64 : téléphones, dont le Pixel 10
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r android-arm64

# Android x64 : émulateurs et appareils Intel
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r android-x64

# Tout compiler : Linux x64 + Windows x64 + Android ARM64
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r all-x64

# Vérifier le parcours sans compiler
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r all-x64 -p:CRPublishPlanOnly=true

# Regrouper les clients complets et les serveurs correspondants
 python scripts/package-cr2026-distribution.py
```

`all-x64` est un parcours de publication : il produit le serveur/client Linux,
le serveur/client/éditeur Windows et un **APK ARM64 pour les téléphones**.
L'éditeur reste réservé à Windows. Les dossiers natifs restent sous
`Intersect.Client/bin/Release/.../publish`, `Intersect.Server/bin/Release/.../publish`
et `Intersect.Editor/bin/Release/.../publish`. Les APK signés sont également
conservés sous `artifacts/android/distribution/`.

### Signature et ressources Android

Pour configurer une nouvelle machine de compilation :

```powershell
# Une seule fois, si aucune clé de distribution n'existe déjà
 ./scripts/initialize-android-signing.ps1

# APK visant un serveur existant : fournir SA clé publique réseau
 ./scripts/build-android-client.ps1 -Distribution -Resources 'C:/client/resources' -NetworkPublicKey 'C:/serveur/network.handshake.bkey.pub'
```

Sur la machine actuelle, les chemins locaux sont enregistrés dans
`.local/android-client.json`. Sauvegarder **le dossier privé `.local/android-signing`**,
avec le keystore et son mot de passe : la même clé doit signer toutes les mises à
jour Android. Ce dossier n'entre jamais dans les paquets joueurs. `versionCode`
augmente automatiquement, séparément du numéro visible `0.8.0`.
Voir le [guide Android complet](Documentation/Android.md).
Pour garder une sortie de test séparée, le script accepte aussi
`-OutputDirectory 'C:/chemin/apk-test'`, notamment avec la clé publique du serveur
encore en ligne. Cela ne modifie pas le paquet regroupé destiné au nouveau serveur.

### Dossier regroupé et distribution

Le script de regroupement part du dernier APK de distribution et des publications
Windows/Linux. Il complète les ressources, vérifie leur cohérence et reconstruit
l'APK si les ressources graphiques ou la clé du serveur doivent être synchronisées.
Les bibliothèques natives Steamworks.NET **2024.8.0** correspondantes sont ajoutées
aux clients de bureau. La première récupération demande une connexion Internet.

Le résultat est créé dans `artifacts/distribution/CR2026-0.8.0-pack<versionCode>/` :

```text
Joueurs/
  Windows-x64/      Client complet, resources, bibliothèques et Jouer.cmd
  Linux-x64/        Client complet, resources, bibliothèques et jouer.sh
  Android-arm64/    APK signé et instructions d'installation
Archives-joueurs/
  CR2026-Windows-x64.zip
  CR2026-Linux-x64.tar.gz
  Android.txt      Chemin de l'APK prêt à envoyer
Serveur/
  Windows-x64/      Binaires à copier sur l'installation serveur existante
  Linux-x64/        Binaires à copier sur l'installation serveur existante
  LIRE-MOI.txt
LIRE-MOI.txt
manifest.json
SHA256SUMS.txt
```

Distribuer uniquement `Joueurs` ou les téléchargements de `Archives-joueurs`.
Le sous-dossier **Serveur est réservé à l'administration**. Il ne contient ni
bases de comptes ni données du monde. Les configurations fournies à titre d'exemple
sont rangées dans `Configuration-exemples` : conserver les ressources, bases et
configurations de l'installation serveur lors de l'application des binaires.
L'archive Linux conserve les permissions des lanceurs et exécutables.

Options utiles : `--output 'C:/chemin/du/paquet'` pour choisir le dossier,
`--android-apk 'C:/chemin/client.apk'` pour sélectionner l'APK, et `--resume`
pour reprendre un regroupement interrompu. Par défaut, un dossier existant n'est
pas écrasé. Les instructions joueurs sont livrées en français, anglais et espagnol.

**Compatibilité réseau :** les nouveaux clients issus de `all-x64` ciblent la clé
du serveur nouvellement compilé. Déployer cet ensemble avec les serveurs fournis.
L'APK 16 précédemment validé sur Pixel reste conservé pour l'ancien serveur encore
en ligne; la préparation du dossier ne déploie aucun serveur et ne remplace pas
l'application du téléphone.

### Dernières informations mobiles

La version Android dispose du joystick, du pilotage des bateaux par cap, du menu
et des contrôles adaptés à l'écran tactile, des noms agrandis et de l'icône du jeu.
Les corrections de gel/flash du rendu et de visibilité des vaisseaux de ligne ont
été validées sur Pixel 10 avec l'APK de distribution 16.

Les nouvelles fiches tactiles proposent, selon le contenu, **Utiliser**, **Équiper**,
**Déséquiper**, **Ouvrir**, **Apprendre** ou **Lancer le sort**, avec un bouton
**Fermer**. Elles restent ouvertes après un toucher; les raccourcis de la barre
s'activent d'un seul toucher sur Android. Les libellés sont disponibles en
français, anglais et espagnol. Les boutons utilisent explicitement Source Sans
Pro Black en taille 14, du texte blanc et un fond sombre bordé pour leur lisibilité.
Les contrôles de disponibilité/cooldown et
l'autorité du serveur restent appliqués. Le paquet regroupé synchronise aussi
les ressources graphiques entre les clients.

Sur Android, les fenêtres de jeu (boutiques, naval, personnage, quêtes, inventaire,
etc.) utilisent désormais un cadre plein écran limité à la zone sûre du téléphone.
Le contenu conserve sa taille lisible et défile au doigt lorsqu'il dépasse, avec
une fermeture fixe dans l'en-tête. Quand plusieurs fenêtres sont ouvertes, le
sélecteur de l'en-tête permet de passer entre elles, notamment banque/inventaire.
Le bouton mobile **Fermer** reste visible même si la croix du thème PC est masquée.
Depuis l'inventaire lié à une transaction, il termine la session avant de fermer
l'inventaire. La boutique Android utilise sa colonne de vente et n'ouvre plus
automatiquement une seconde fenêtre d'inventaire devant elle.
Un balayage ne déclenche pas l'action de la ligne touchée. Les tests d'interface
couvrent les clics après défilement, les écrans étroits et la navigation entre
fenêtres. L'ouverture directe de la boutique et le retour au jeu avec **Fermer**
ont été confirmés sur Pixel 10 avec l'APK 27. Le défilement des autres fenêtres
et le sélecteur entre fenêtres restent à vérifier séparément sur téléphone.

Le menu des émotes Android utilise une cible tactile agrandie et évite les zones
du joystick et des boutons d'action. La fermeture explicite déconnecte le client,
retire la tâche Android et termine ce runtime après nettoyage; Home et la mise
en arrière-plan conservent leur comportement habituel. Le menu des émotes dégagé,
la fermeture complète et la relance normale ont été confirmés sur Pixel 10 avec
l'APK 29. Le dossier regroupé `artifacts/distribution/CR2026-0.8.0-pack36` contient
les correctifs dans l'APK 36, compatible avec ses exécutables serveur.

Le rafraîchissement des émotes conserve désormais la liste ouverte dès le premier
clic sur PC et Android; ce comportement a été confirmé sur Pixel avec l'APK 31.
Les clients Windows/Linux ont été republiés et leurs archives mises à jour.
Sur Android, toucher le champ de clavardage appelle le clavier natif. Son ouverture
a été confirmée sur Pixel avec l'APK 33. Le bouton de cette saisie devient
**Envoyer** et transmet directement par le chemin normal du chat; **Annuler**
n'envoie rien et conserve le brouillon initial. Le focus de chat est relâché à
la fermeture pour rendre les commandes tactiles accessibles. Cet ajustement est
inclus dans les APK 35 (serveur actuel) / 36 (serveur compilé). L'APK 35 est
installé sur le Pixel; l'envoi direct et l'absence d'envoi avec **Annuler** ont
été confirmés par le joueur sur cette version.

La compilation complète `all-x64` a réussi avec les cinq publications de bureau
et l'APK signé; les formats ELF/PE x86-64, la signature Android et les archives ont
été vérifiés. Les exécutables de bureau n'ont pas été lancés pendant ce contrôle.
Les nouvelles actions mobiles font l'objet de tests de clics d'interface.
L'action d'inventaire a été confirmée sur Pixel 10 avec l'APK 21, puis la
lisibilité de tous les boutons d'objet, de sort et de fermeture avec l'APK 23.
Le lancement effectif d'un sort reste à confirmer séparément.


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

## Calendrier marketing — novembre 2026 à mars 2027

La promotion de Corps Royaux est maintenant **synchronisée avec les playtests, le Steam Next Fest et l'Open BETA envisagée**, plutôt qu'avec une sortie générale le 7 décembre. **La préparation initialement prévue pour octobre 2026 est reportée et concentrée à partir du 2 novembre.** Ce calendrier est un plan à exécuter, pas un historique de publications effectuées.

| Période | Phase | Actions marketing essentielles |
| --- | --- | --- |
| **2–8 novembre 2026** | **Préparation accélérée** | Audit Steam et concurrence, press kit, vrais visuels de gameplay, présentation FR/EN, liste de créateurs, calendrier des publications |
| **9–15 novembre 2026** | **Début de la promotion + Playtest #1** | Présenter le jeu et son univers, recruter **20–50 testeurs ciblés**, couvrir le test fermé des **14–15 novembre** |
| **16–30 novembre 2026** | **Intensification** | Clips et articles sur donjons, invasions, combat, familiers, progression et naval **lorsque suffisamment stable**; partager les correctifs du premier test |
| **1er–7 décembre 2026** | **Campagne du Playtest #2** | Teaser, bande-annonce, compte à rebours du **3 au 7 décembre**, instructions d'accès, suivi communautaire de l'événement du **7 décembre** |
| **8–31 décembre 2026** | **Fidélisation** | Bilans joueurs, correctifs, contenu de développement, préparation du test de charge |
| **Janvier 2027** | **Playtest #3 et Next Fest** | Recruter des testeurs, documenter la stabilité, préparer la démo et gérer l'inscription à Steam Next Fest selon admissibilité |
| **1er–21 février 2027** | **Pré-campagne Next Fest** | Démo et visuels vérifiés, contacts presse/streamers approuvés, contenus programmés et FAQ |
| **22 février–1er mars 2027** | **Steam Next Fest (si accepté)** | Mettre la démo publique à disposition, la présenter et analyser retours et wishlists |
| **Mars 2027** | **Open BETA (si prête)** | Communiquer la date uniquement après décision de lancement, puis suivre les retours de la communauté |

**Priorité :** développement des **wishlists Steam**; objectifs complémentaires : demandes d'accès aux playtests, participation effective, retours des joueurs et intérêt des créateurs de contenu. La campagne se prépare d'abord en **français et en anglais**, avec adaptation aux plateformes (Steam, Discord, YouTube, réseaux sociaux et communautés pertinentes).

**ChatGPT Work** peut être chargé de l'audit, de la production de textes et visuels, de la prospection préparatoire et du suivi **si les outils et les données nécessaires sont accessibles**. Publications, prises de contact, dépenses, ouvertures de playtest et changements Steamworks nécessitent l'approbation du responsable Studio Logiklik.

**Point à ne pas confondre :** le **7 décembre 2026** est un **playtest communautaire**, **pas le lancement de l'Open BETA**. Les dates restent prévisionnelles et dépendent de la qualité technique et des autorisations Steam.

[**Voir les tâches détaillées, la cadence éditoriale, les indicateurs et les règles de validation dans ROADMAP.md**](ROADMAP.md#calendrier-marketing--novembre-2026-à-mars-2027).

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
