# Recette locale de la PR 248 — 21 novembre 2026

La phase 2 a été remise à jour avec `main` après la fusion de la PR 247.
Les conflits ont été résolus en conservant les correctifs de disposition,
de reprise, de modalités et de destruction des contrôles.

## Vérifications exécutées

- Compilation Release du client, du serveur et de l'éditeur Windows.
- 12 tests NUnit : configuration, traductions EN/FR/ES, compatibilité des anciens
  JSON, identifiants/progression, protocole et exercices de combat.
- 4 tests serveur : arrivée mesurée sur la position serveur, mauvaise carte,
  coordonnées hors carte et référence PNJ inexistante.
- 19 groupes de tests Gwen : cinq résolutions de 320×480 à 1920×1080 dans trois
  langues ; commandes, reprise, premier plan, fenêtres modales, nettoyage ;
  PNJ exact et mobile/masqué, case, cible hors écran, carte absente,
  projection caméra/zoom/échelle d'interface et dessin de secours sans images ;
  sons uniques lors des transitions, répétition de paquets, pause, reprise,
  redémarrage du guide et modification de configuration.
- Trois groupes éditeur : construction du vrai formulaire, listes localisées,
  modifications des propriétés et des contrôles visuels, coordonnées limitées
  à la carte, conservation après sérialisation, dessin de l'aperçu.
- Deux PNG contrôlés : fond transparent (tolérance d'une unité alpha sur les
  coins), centre transparent de l'anneau et contenu non vide.
- Trois WAV contrôlés : PCM16 mono, 44,1 kHz, début/fin silencieux, crête inférieure
  à 0,32 de pleine échelle. Création originale par synthèse, sans fichiers externes.
- `git diff --check` et contrôle de l'absence des commits privés Intel dans
  l'historique de cette branche.

## Commandes reproductibles

```text
dotnet build Intersect.Client/Intersect.Client.csproj -c Release
dotnet build Intersect.Server/Intersect.Server.csproj -c Release
dotnet build Intersect.Editor/Intersect.Editor.csproj -c Release
dotnet test Intersect.Tests/Intersect.Tests.csproj -c Release --filter "FullyQualifiedName~TutorialTests|FullyQualifiedName~TutorialCombatLessonsTests"
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter FullyQualifiedName~TutorialWorldGuidanceTests
dotnet run --project Utilities/TutorialUiTests/TutorialUiTests.csproj -c Release
dotnet run --project Utilities/TutorialEditorTests/TutorialEditorTests.csproj -c Release
python tools/generate_royal_guide_sounds.py
```

Les exécutions locales utilisent `-p:RunAnalyzers=false -p:NuGetAudit=false`.
Les avertissements existants du dépôt ne sont pas présentés comme corrigés.
Les journaux sont conservés dans `validation-local/`, exclu du dépôt.

## Limites et essai restant

Les tests Gwen utilisent des métriques de police synthétiques ; l'aperçu WinForms
reste illustratif. Aucun essai connecté au serveur, aucune validation visuelle
en situation réelle ni écoute sur le matériel du joueur n'est revendiqué.
Configurer une étape sur une vraie carte avec un vrai PNJ, puis vérifier les
scénarios de `RoyalGuidePhase2.fr.md` et écouter les trois signaux en jeu.

Le guidage PNJ n'atteste pas encore un dialogue ou une interaction : utiliser
une variable d'événement comme condition, ou poursuivre ensuite avec la PR 249.
Il n'indique pas d'itinéraire vers une carte non chargée.

Le client Intel HD 3000 conserve sa branche et ses binaires privés. Aucun
exécutable installé, profil utilisateur ou serveur en service n'a été remplacé
pendant cette validation.
