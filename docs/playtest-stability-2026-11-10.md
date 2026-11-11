# Stabilisation avant playtest — 10 novembre 2026

## Sources et limites

Analyse des deux archives `logs.zip` (distribution Steam Intel HD 3000 et client/éditeur courant), puis de `archive-2026-11-11T012116Z.tar.gz` (54 fichiers serveur). Journaux examinés comme données de diagnostic. Les répétitions entre journaux normaux, erreurs et console ne constituent pas des incidents indépendants. Correctifs basés sur main `0debe9251`.

## Correctifs

- Éditeur : exclure les contrôles internes `PropertyGridInternal` du thème. La pile du crash Win32 1400 montre la modification du `BorderStyle` d'une liste du menu de propriétés pendant la boucle modale.
- Éditeur : réutiliser sans écriture une bibliothèque SQLite identique déjà chargée; publier les extractions nécessaires via un fichier intermédiaire unique. Une bibliothèque différente encore verrouillée reste une erreur réelle, jamais ignorée.
- Client/éditeur : isoler les shaders temporaires par processus et session; tronquer correctement le fichier lors d'une réextraction.
- Éditeur : vérifier toute la chaîne de propriétaires avant `Show(owner)` pour empêcher une propriété circulaire.
- Éditeur : rendre idempotente la réception de suppressions d'objets absents (pile observée sur un PNJ).
- Serveur : corriger le calcul du délai dans `User.SaveWithDebounce`. L'ancienne comparaison `lastSave < now + delay` ignorait pratiquement tous les appels. La nouvelle comparaison utilise le temps écoulé. Ce défaut est confirmé par lecture et tests, sans preuve qu'il cause les arrêts de processus.

## Résultats serveur

23 journaux de diagnostic : 21 contiennent un appel de `ExitCommand.HandleValue`, 19 un `ProcessExit code=0`, aucun un événement `UnhandledException`. Le journal de la session encore active au moment de l'archive n'a naturellement pas de sortie finale.

Le 10 novembre à 19:55:16 (Toronto), la dernière session interrompue reçoit également une commande de sortie. Son dernier heartbeat est à 19:55:19; la fermeture n'est pas terminée dans le journal. À ce moment, `memory.current=567459840`, `memory.max=2469605376`, `oom=0`, `oom_kill=0`. Cela ne démontre pas une panne mémoire. Le journal seul ne permet pas de trancher entre arrêt externe et fermeture bloquée; vérifier l'historique du panneau d'hébergement et le code de sortie du conteneur.

Le journal de base de données du 8 novembre à 21:09:04 contient une sauvegarde en échec avec SQLite Error 5 (`database is locked`). Il faut vérifier les accès concurrents à la même base et qu'un seul serveur utilise ces fichiers. Le traitement existant des sauvegardes journalise l'échec; ne pas considérer cette sauvegarde comme réussie. Aucun changement de format de base ou migration n'est appliqué ici.

## Problèmes restant à traiter

- Trois ressources du dépôt sont réellement invalides : `resources/gui/table/button_cards.png`, `button_exit.png`, `button_portraits.png`. Les données PNG échouent à la vérification des chunks/CRC. Elles étaient déjà invalides lors de leur introduction dans l'historique disponible. Restaurer les originaux graphiques sains; les ZIP fournis ne contiennent que les logs. Le chargeur intercepte les exceptions, mais ces boutons peuvent mal s'afficher.
- Les erreurs `NoSuitableGraphicsDeviceException` indiquent l'absence des fonctions framebuffer OpenGL nécessaires. Valider la distribution et le pilote sur la machine concernée; aucun contournement matériel n'est revendiqué.
- Le crash de CommandPrinter possède déjà un affichage de secours pour les commandes non spécialisées dans main. Steam possède déjà une vérification de l'initialisation optionnelle. Les couches de carte manquantes sont déjà complétées au chargement. Les anciens logs ne prouvent pas une récidive sur cette version.

## Validation

Tests de régression de l'éditeur : contrôles internes PropertyGrid avec handles réels, propriété circulaire des fenêtres, DLL identique verrouillée, remplacement plus court, nettoyage du fichier intermédiaire, extraction temporaire et suppression répétée d'un PNJ absent.

```powershell
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj --no-restore -p:RuntimeIdentifiers=win-x64 -p:IntersectConfiguration=Debug -- --crash-stability-only
dotnet run --project Utilities/CrashDiagnosticsTests/Intersect.CrashDiagnosticsTests.csproj --no-restore -- --save-debounce-only
```

Les deux séries passent. Les avertissements existants du dépôt subsistent. Les tests ne remplacent pas une session multijoueur sur l'hébergement et la machine Intel HD 3000. Les exécutables de production et les clés de distribution ne sont pas remplacés.
