# Validation de la PR #249 — interactions du guide royal

Date : 21 novembre 2026. Base : `main` après fusion de la PR #248 (`a194a45eb`).

## Corrections

- Le contrôle de proximité `IsEventOneBlockAway` était un `return true`. Il vérifie maintenant la position serveur, l'élévation, l'instance et le propriétaire de l'événement. Les voisins sont résolus par les vraies connexions entre cartes, pas par des coordonnées identiques sur des cartes indépendantes.
- L'activation, la collision et l'attribution de progression sont sérialisées sous le verrou du joueur. Une pile déjà démarrée ne donne pas un second crédit.
- Les événements sans page active, les déclencheurs automatiques, les autres cibles, les guides inactifs et les révisions périmées ne valident pas la leçon. Le paquet `Next` ne valide jamais `InteractEvent`.
- Un événement commun, absent de la carte ou sans déclencheur d'interaction est refusé à l'enregistrement.
- Le repère suit les pieds et le haut de l'événement exact, masque les événements cachés et réutilise les images de la PR #248. Le message d'interaction s'adapte à la largeur de la fenêtre.
- L'éditeur charge les événements d'une carte à l'ouverture de leur liste, distingue les homonymes, réinitialise la cible après changement de carte et conserve les choix traduits EN/FR/ES.

## Recette automatisée

Commandes depuis la racine du dépôt, configuration Release :

```text
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter FullyQualifiedName~Tutorial
dotnet test Intersect.Tests/Intersect.Tests.csproj -c Release --filter FullyQualifiedName~Tutorial
dotnet run --project Utilities/TutorialUiTests/TutorialUiTests.csproj -c Release
dotnet run --project Utilities/TutorialEditorTests/TutorialEditorTests.csproj -c Release
dotnet build Intersect.Client/Intersect.Client.csproj -c Release
```

Les tests serveur appellent les vraies entrées `TryActivateEvent`, `TryBumpEvent`, `HandleEventCollision` et `HandleTutorial` avec des joueurs, cartes et événements de test. Ils vérifient l'attribution, le refus des fausses interactions, l'unicité, la sérialisation de progression, le rechargement de cette progression et le redémarrage du guide. Ils n'ouvrent pas de connexion au serveur hébergé et n'écrivent pas sa base.

Les tests de fenêtre utilisent les contrôles Gwen avec des métriques de rendu synthétiques sur cinq tailles et trois langues. Les tests d'éditeur construisent les vrais contrôles WinForms et dessinent l'aperçu avec GDI. Ils couvrent les choix d'événement, les libellés traduits, le clic dans l'aperçu et la conservation des identifiants.

Résultats locaux : **27 tests serveur et 14 tests de configuration réussis**, **19 groupes de contrôles client réussis**, contrôles d'éditeur réussis en **EN/FR/ES**, transparence des deux images vérifiée. Les compilations Release du serveur, de l'éditeur et du client standard ont réussi. Après correction du nettoyage des joueurs de test signalé par NUnit, les 27 tests serveur ont été relancés avec succès avec `RunAnalyzers=true`. Les autres vérifications locales ont utilisé `RunAnalyzers=false`, et toutes ont utilisé `NuGetAudit=false` ; le workflow GitHub conserve ses commandes normales. Les avertissements existants du dépôt ne sont pas traités par cette PR.

## Limites et essai en jeu restant

La réussite correspond au **démarrage** de l'événement, pas à la fin du dialogue ni à l'acceptation d'une quête. Utiliser une variable de joueur pour ces conditions supplémentaires. Les pages conditionnelles restent soumises au moteur d'événements : prévoir une page accessible au personnage de test.

La recette en jeu doit encore confirmer l'affichage avec les vrais graphismes, le chargement réseau des cartes dans l'éditeur, les trois déclencheurs, la reprise après déconnexion réelle et l'enregistrement après redémarrage serveur. Aucun essai de ce type n'est revendiqué ici. La PR reste en brouillon en attendant cette recette.

Les branches et exécutables Intel HD 3000 restent séparés. Cette PR n'installe ni ne remplace le client normal ou le client Intel.
