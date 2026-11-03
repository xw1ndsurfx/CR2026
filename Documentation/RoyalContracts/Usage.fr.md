# Tableau des contrats royaux

## Mise en route

1. Compiler et distribuer ensemble le serveur, le client et l’éditeur modifiés. Les nouveaux paquets réseau nécessitent les trois composants de la même version.
2. Dans le hub de l’éditeur, ouvrir **Contrats royaux**, catégorie jeu / progression du monde. Le menu des éditeurs contient aussi **Gestion des contrats royaux**.
3. Le serveur propose par défaut trois contrats quotidiens et deux contrats hebdomadaires : chasses, exploration et chasse collective. Ajouter des modèles pour varier davantage la sélection.
4. Pour une livraison, ajouter un modèle, choisir **Livrer les ressources**, sélectionner un objet empilable existant et régler les quantités. Aucun identifiant d’objet fictif n’est installé automatiquement.
5. Dans un événement de carte ou un événement commun, ajouter la commande **Open Royal Contracts** (libellé français : **Ouvrir les contrats royaux**) dans la catégorie magasins / banques. Elle ne nécessite aucun paramètre. Un PNJ intendant ou une interaction avec un tableau peut ainsi ouvrir le panneau.
6. En jeu, ouvrir le tableau, sélectionner un parchemin, **Accepter le contrat**, accomplir l’objectif puis **Réclamer la récompense**. Les livraisons se font avec le bouton **Livrer les ressources**.

## L’intendant automatique

Il s’agit d’un moteur de règles intégré. Il ne contacte aucun service d’IA et ne nécessite ni clé ni abonnement. Il choisit les modèles actifs par tirage pondéré sans doublon, puis tire une quantité entre le minimum et le maximum du modèle. Les récompenses sont celles du modèle, afin que leur économie reste sous le contrôle du créateur du jeu.

- Quotidiens et hebdomadaires : de 0 à 8 contrats chacun, avec pagination de quatre parchemins par page.
- Renouvellement par défaut à **05:00 UTC** ; nouvelle semaine le lundi à cette heure. L’UTC évite les ambiguïtés de changement d’heure.
- Les contrats affichés sont des copies des modèles. Modifier un modèle, son poids ou les emplacements ne modifie pas les missions déjà proposées. Les changements s’appliquent au prochain renouvellement concerné.
- Modifier l’heure de renouvellement peut provoquer un nouveau calcul de période ; le faire lors d’une maintenance, pas pour ajuster une récompense en cours.
- La rotation se met à jour lors du prochain accès ou événement de progression pertinent. Elle ne nécessite pas de tâche planifiée externe et retrouve directement la période actuelle après un arrêt prolongé.
- Désactiver la rotation met le tableau en pause : aucune acceptation, livraison, progression ou réclamation pendant la pause. Le calendrier continue de s’écouler.

## Gestion des objectifs et récompenses

Chaque modèle dispose d’un nom et d’une description, d’une période, d’un niveau minimum, d’un objectif, d’une cible facultative, d’une quantité minimum / maximum, d’un poids de sélection et d’un choix personnel / collectif.

**Chasse** : chaque mort de PNJ est comptée uniquement pour le joueur crédité de la victoire par le moteur. Les familiers sont exclus. Choisir un PNJ précis ou laisser « Aucun / tous » pour une chasse générale. Le joueur doit avoir accepté le contrat avant les éliminations.

**Exploration** : visiter des cartes distinctes après acceptation. Les entrées sont validées par le serveur, y compris les transitions normales et les téléportations. Revenir sur une carte, changer d’instance ou se reconnecter sur une carte déjà comptée n’ajoute pas de progression. Une cible précise permet de représenter l’entrée d’une île ; dans ce cas, régler minimum et maximum sur 1.

**Livraison** : le bouton remet les objets présents dans l’inventaire principal, jusqu’à la quantité personnelle restante. Les livraisons partielles sont possibles. Les coffres et sacs imbriqués ne sont pas débités. Une fois livrés, les objets ne sont pas remboursés si le contrat expire.

**Collectif** : les contributions de tous les personnages du serveur s’additionnent, y compris celles des personnages déconnectés. Chacun doit accepter et contribuer au moins une fois pour réclamer sa propre récompense. Le total collectif repose sur les contributions sauvegardées : les éliminations et visites peuvent prendre quelques secondes pour apparaître, selon la sauvegarde normale du compte.

**Récompenses** : sceaux royaux et, au choix, un objet empilable avec sa quantité. Les sceaux sont un compteur dédié au personnage, visible sur le tableau ; ils ne remplacent pas une monnaie existante et aucun magasin d’échange n’est ajouté. La remise d’objets respecte les tailles de pile natives. Si l’inventaire est plein, la récompense reste disponible jusqu’à l’expiration du contrat.

Le serveur valide les cibles et objets à l’enregistrement. Une suppression ultérieure d’un objet déjà utilisé par un contrat affiché peut bloquer sa récompense ; conserver les objets référencés jusqu’à la fin de la période.

## Sauvegarde et sécurité

- `resources/royal-contracts/configuration.json` : configuration et modèles, créés au premier accès si absents.
- `resources/royal-contracts/rotation.json` : contrats affichés et échéances. **Conserver ce fichier lors des déploiements et sauvegardes** pour préserver les missions de la période en cours.
- Base joueurs existante : profil des contrats, cartes visitées, sceaux et reçus de récompense sont ajoutés au document JSON `Pets`, déjà utilisé par d’autres progressions du projet. Les contributions collectives utilisent des variables joueurs internes dérivées de l’identité de la mission. Aucune migration de schéma n’est requise.
- Les contributions internes de périodes anciennes restent dans les variables joueurs, comme l’historique des succès périodiques existant ; aucun nettoyage automatique de ces lignes n’est inclus.
- Une livraison sauvegarde son débit, sa progression et sa contribution collective dans la même sauvegarde de compte. Une récompense sauvegarde ses objets, ses sceaux et son reçu ensemble. L’échec ou une exception de sauvegarde restaure l’état en mémoire.
- Les paquets clients ne contiennent ni score, ni quantité de progression, ni montant de récompense. Le serveur décide de tout et bloque les doubles réclamations.
- Seule une connexion d’éditeur peut modifier la configuration. Le tableau doit être ouvert par un événement avant les actions client ; la session dure 15 minutes et les actions nécessitent de rester sur la même carte, à huit cases maximum du point d’ouverture. Revenir au tableau et l’ouvrir de nouveau renouvelle l’accès.
- Les fichiers de configuration invalides sont conservés et le tableau se met en pause. Une rotation illisible ne déclenche pas un remplacement silencieux qui pourrait réinitialiser des récompenses.

Sauvegarder la base joueurs et le dossier `resources/royal-contracts` ensemble. Ne pas supprimer la rotation active pour « relancer » les missions pendant que les joueurs les accomplissent.

## Images et langues

Les deux images fournies sont conservées dans `resources/gui/royal-contracts/board.png` et `parchment.png`. Le panneau utilise la scène comme fond et un cadrage intérieur du parchemin pour éviter son pourtour blanc. Les fichiers sont copiés dans les sorties de compilation et de publication du client et de la version solo via `Shared/RoyalContractsAssets.props`.

Les textes d’interface et les exemples disposent d’un catalogue anglais, français et espagnol partagé. Les noms et descriptions personnalisés passent par le catalogue de traduction du contenu existant, avec retour au texte source lorsque la traduction manque. Le service de traduction du projet n’est utilisé que s’il a été explicitement configuré ; les contrats n’intègrent aucun nouveau service externe.

## Vérification

Compilation locale :

```powershell
dotnet build Intersect.Server.Core/Intersect.Server.Core.csproj -c Debug -p:IntersectConfiguration=Debug
dotnet build Intersect.Client/Intersect.Client.csproj -c Debug -p:IntersectConfiguration=Debug
dotnet build Intersect.Editor/Intersect.Editor.csproj -c Debug -p:IntersectConfiguration=Debug
dotnet run --project Utilities/RoyalContractsTests/RoyalContractsTests.csproj -c Debug -p:IntersectConfiguration=Debug
dotnet run --project Utilities/RoyalContractsUiTests/RoyalContractsUiTests.csproj -c Debug -p:IntersectConfiguration=Debug -- <racine-du-depot> <dossier-des-apercus>
```

La première compilation nécessite les sous-modules du dépôt et les dépendances .NET habituelles. Le test d’interface fonctionne sous Windows et rend les vrais contrôles Gwen avec un moteur de dessin de vérification et des polices Windows ; cet aperçu n’est pas une capture d’une partie en cours. Le formulaire de gestion est également rendu à partir de son fichier source natif, avec un réseau de test qui n’envoie aucun paquet.

Les contrôles automatiques couvrent les frontières UTC, les semaines, les rotations et redémarrages, les copies des modèles, les cibles et quantités, les cartes déjà visitées, les contributions, les réclamations, les retours arrière d’inventaire sur échec, la sauvegarde native du profil, les paquets réseau, la couverture des langues et les dispositions 1024×768, 1280×720 et 1920×1080.

Avant une mise en production, effectuer une partie de validation sur une copie du serveur et de sa base : ouvrir le tableau par le PNJ, compléter une chasse, explorer une île, livrer les objets réels du jeu, tester à deux personnages le contrat collectif, réclamer puis se reconnecter. La partie complète et l’équilibrage de l’économie réelle ne sont pas remplacés par les tests automatiques.
