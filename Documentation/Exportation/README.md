# Corps Royaux — Exportation

## État de l'intégration

**PR #228 en brouillon. Le parcours complet est codé, mais non compilé ni testé en conditions réelles. Ne pas merger dans main avant vérification.**

### Fonctionnalités ajoutées au code

- Routes enregistrées côté serveur dans `resources/exportation.json`. Chaque route référence deux personnages **EventDescriptor**, deux maps d'îles distinctes, une **quête existante à une seule tâche**, les marchandises (identifiants, catégories, quantités), une profession personnalisée (`ProfessionId`), une récompense en objet monétaire empilable et de l'XP.
- Commande d'événement **Open Exportation**, accessible dans l'éditeur d'événements. La même route est liée au marchand de départ et au destinataire, chacun sur sa carte.
- Le serveur autorise les commandes grâce à un événement réel, un identifiant de cargaison, une position, une session et une séquence. Les objets sont fournis *virtuellement* par le marchand : les autres objets de l'inventaire du joueur ne sont pas consommés.
- Au scellement, l'instance de cargaison est liée au bateau sélectionné et la quête existante démarre. La sauvegarde de la cargaison utilise une extension JSON persistée dans la colonne de flotte navale de la sauvegarde du personnage, avec les autres états du compte.
- L'embarquement, la traversée et le débarquement doivent être confirmés par le système naval natif. Un voyage par téléportation seul ne satisfait pas l'objectif.
- Livraison au bon personnage : la récompense et l'XP du métier sont inscrites avec la conclusion de la quête; une répétition du même scellement est refusée, avec tentative de restauration en cas de sauvegarde échouée.
- Mort ou naufrage du bateau lié à la commande : cargaison perdue, retour au marchand pour refaire les caisses sans XP. Un abandon de la quête est aussi réconcilié, pour éviter de bloquer le joueur.
- Interface client avec les GFX approuvés, affichage dynamique des commandes et des récompenses, boutons **Préparer / Sceller / Livrer**, dix bruitages originaux, textes FR/EN/ES. **Le remplissage fonctionne par clic, pas encore par glisser-déposer.**
- Le hub de l'éditeur inclut un aperçu des GFX/sons et un éditeur de routes **JSON** soumis à la validation du serveur.

### Comment préparer une route de test

1. Dans l'éditeur des métiers, crée un métier personnalisé « Exportation », note son GUID, et fais-le apprendre au personnage (par exemple commande `Modify Profession → Learn` dans l'événement du marchand avant `Open Exportation`).
2. Place deux événements de personnages sur deux cartes d'îles distinctes et note les **ID des événements** (ce ne sont pas les identifiants des monstres ou des PNJ de combat).
3. Crée une quête standard comprenant **exactement une tâche** qui reste active jusqu'à la remise de la cargaison. Évite que d'autres événements la complètent automatiquement. N'ajoute pas de récompenses dupliquées dans les événements de quête : le métier Exportation crédite déjà ses propres récompenses.
4. Vérifie qu'un bateau appartenant au joueur peut être lancé au port de départ et amarré au port de destination.
5. Dans **Mini-Game Editors → Exportation route editor**, configure `Enabled: true`, `ProfessionId`, puis une route avec `Id`, `SourceNpcId`, `RecipientNpcId`, `SourceMapId`, `RecipientMapId`, `QuestId`, `MinimumProfessionLevel`, `Lines`, `RewardItemId`, `RewardQuantity`, `ExperienceReward`.
6. Dans `Lines`, les champs sont `ItemId`, `Group` (`Materials`, `Fragile` ou `Provisions`), `Quantity` et `Sprite` optionnel (`planches`, `lingots_fer`, `potion_rouge`, `tissu`, `pommes`). Choisis le véritable `ItemId` des Aureons comme `RewardItemId` pour cette monnaie.
7. Ajoute **Open Exportation** dans l'événement de chaque personnage, sélectionne la **même** route et applique la configuration.
8. Teste le cycle : préparer → sceller → vérifier le journal de quêtes → embarquer sur le bon bateau → traverser → accoster → remettre la commande → constater les Aureons et l'XP.

### Installer les ressources

Les **23 fichiers binaires** (13 images + 10 WAV) sont distribués dans le ZIP de cette conversation; ils **ne sont pas encore committés** dans la PR. La copie lors du build et du publish est configurée dans `Shared/ExportationAssets.props`.

Depuis la racine du dépôt, avec `Corps_Royaux_Exportation_Ressources_Pretes.zip` téléchargé :

```sh
python scripts/exportation/install_assets.py "C:/Downloads/Corps_Royaux_Exportation_Ressources_Pretes.zip" --check
python scripts/exportation/install_assets.py "C:/Downloads/Corps_Royaux_Exportation_Ressources_Pretes.zip"
```

L'installateur vérifie SHA-256, dimensions des PNG et format audio avant d'écrire dans `resources/gui/exportation/` et `resources/sounds/exportation/`. Pour des builds Steam/CI reproductibles, ajoute ces fichiers au dépôt ou provisionne le ZIP via un pipeline contrôlé.

### Vérifications obligatoires avant fusion

Dans un environnement avec le SDK .NET 8 et les dépendances du projet :

```sh
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release
dotnet build Intersect.Client/Intersect.Client.csproj -c Release
# Sur Windows :
dotnet build Intersect.Editor/Intersect.Editor.csproj -c Release
```

Puis effectuer des tests en jeu client + serveur + base de données : quête réelle, XP, inventaire plein, perte sur mort et naufrage, mauvais bateau, déconnexion/reconnexion, redémarrage du serveur, abandon de quête, double livraison et sauvegarde échouée.

**Aucun résultat de compilation ou de tests .NET n'a été obtenu dans cet environnement; cette PR reste donc en brouillon.**

### Limites connues

- Aucun glisser-déposer (clic pour mettre/retirer des objets).
- Choix automatique d'un bateau utilisable lors du scellement; une sélection manuelle serait préférable si le joueur possède plusieurs bateaux.
- Éditeur de routes encore en JSON, pas en interface à champs guidés.
- Les cas spéciaux de récompenses de la quête et d'événements de niveau de métier doivent être validés lors des tests d'intégration; éviter les paiements supplémentaires configurés dans la quête.

## Mise à jour — bateau possédé et rendu du client

Le marchand **n'exige plus que le bateau soit amarré sur la carte du marchand**.
Le serveur vérifie uniquement la possession d'au moins un bateau non coulé
avec des points de vie positifs, même si son port de retour est ailleurs.
On peut préparer, sceller puis rejoindre un port pour embarquer.

Pour éviter de lier par erreur une commande au premier bateau du joueur,
le scellement laisse l'identifiant du navire vide. Ce dernier est renseigné
uniquement lors de l'embarquement confirmé par le système naval natif,
et le bateau doit appartenir au joueur et être opérationnel. Une traversée
et un accostage sur le **même bateau** restent obligatoires pour la remise.
Une téléportation ne peut pas valider la livraison. Le naufrage du bateau
transporteur et la mort conservent les règles habituelles de perte.

La barre verte indique un **bateau possédé**, et non un bateau amarré.
Le client affiche « Naviguer vers la destination » après scellement si
le personnage est encore sur l'île de départ; l'action de livraison n'est
disponible qu'après la traversée et à proximité du destinataire.

Pour les polices Empire ou assimilées, le client calibre l'échelle sur la
hauteur **réelle** fournie par le renderer après l'éventuelle surcharge
`GlobalFontSize`. Les boutons « X » de retrait sont masqués quand aucune
quantité ne peut être retirée et les fonds de cargaison ne recouvrent plus
les caisses GFX.

Validation manuelle à faire après build : tester 1600×900 et 1280×720
avec de vrais GFX (jour et nuit); navire possédé et amarré ailleurs;
aucun bateau; bateau coulé; puis préparation, scellement, embarquement,
accostage, livraison et mort/naufrage.



## Interface Exportation — intégration de la maquette V2

La maquette est maintenant un **véritable écran C# jouable** dans
`Intersect.Client.Core/Interface/Game/ExportationWindow.cs`. Aucun nouvel
écran figé ni fausse monnaie/EXP n'est utilisé : le GFX médiéval approuvé est
conservé et enrichi par des contrôles dynamiques.

- Bon de commande : bandeau rouge, détails du marchand, noms de cartes,
  icônes des marchandises, quantités empaquetées et mini-barres réelles.
- Caisses : matériaux à gauche, autres groupes à droite. Jusqu'à quatre
  icônes par caisse représentent les objets réellement empaquetés, avec
  les compteurs exacts et « OK » uniquement si le groupe est complet.
- Rangée d'objets : sélection visible, icônes, état restant et info-bulles
  sur les boutons cliquables. Les boutons de retrait restent accessibles.
- Livraison : récompense (icône réelle de l'objet), EXP du métier
  serveur, statut bateau possédé et trois étapes dépendant de l'état réel
  de la cargaison (préparation, scellement, embarquement, accostage, remise).
- Navigation : bouton principal vert lorsqu'une action est disponible,
  avertissement de perte, bouton « ? » fonctionnel ouvrant une aide
  superposée et fermable.
- Multilingue : textes français / anglais / espagnol.

### QA visuelle / fonctionnelle

1. Installer le pack approuvé `assets.json` et lancer le client compilé.
2. Ouvrir Exportation à 1672×941, 1600×900 et 1280×720, de jour et de nuit.
3. Vérifier langues FR/EN/ES et textes longs : ni débordement ni recouvrement.
4. Vérifier un ordre à un objet puis à plusieurs objets avec pagination.
5. Cliquer sur les objets, retirer un objet et contrôler les compteurs,
   barres, caisses et info-bulles.
6. Tester « ? » puis « Fermer l'aide » et l'impossibilité de cliquer sur
   une caisse derrière l'aide.
7. Sceller, embarquer, accoster et livrer. L'EXP et les récompenses
   doivent provenir des données serveur, jamais du dessin d'exemple.
8. Tester la perte par mort ou naufrage et les étapes de réessai.

Les tests NUnit des compteurs et des étapes sont des garde-fous ; les
véritables GFX, absents du dépôt, nécessitent une inspection dans le jeu.
