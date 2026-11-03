# Diagnostic des arrêts du serveur

## Automatique après mise à jour du serveur

Le dossier `logs` conserve maintenant :

- `server-console.log` : copie de stdout et stderr, y compris les messages ASP.NET et les erreurs qui ne passent pas par le logger du jeu. Rotation à environ 4 Mio, avec trois archives `.1` à `.3` (environ 16 Mio au total). Les écritures sont fermées immédiatement ; une panne de disque reste possible.
- `crash-Intersect Server-*.log` : journal indépendant avec version, commit, exceptions complètes, thread, code de sortie lorsque .NET peut le fournir, et état mémoire/CPU/threads chaque minute. Une exception non gérée déclenche aussi un relevé immédiat. Les erreurs de traitement des paquets et de mise à jour des cartes indiquent désormais l'opération et les identifiants concernés.
- `previous-session.txt` : bilan du dernier journal au lancement. L'absence de `ProcessExit` est signalée comme une cause inconnue, jamais comme une preuve de manque de mémoire. Un second serveur simultané peut aussi expliquer un journal encore ouvert. Un code zéro indique une sortie du processus, sans garantir une fermeture complète du jeu.

Ces fichiers sont écrits localement ; aucun envoi automatique. Les permissions doivent réserver leur accès aux administrateurs. La console peut contenir les données que les composants du serveur y écrivent déjà. Les nouveaux diagnostics de paquets ne sérialisent ni mots de passe, ni jetons, ni contenu des conversations. Les journaux d'exception existants gardent leur politique de conservation actuelle.

## Linux / hébergement en conteneur

Pour conserver aussi le résultat observé par un processus extérieur au jeu, copier `scripts/start-server-diagnostics.sh` à côté de l'exécutable puis remplacer la commande de démarrage par :

```bash
bash start-server-diagnostics.sh ./"Intersect Server"
```

Pour une distribution avec DLL :

```bash
bash start-server-diagnostics.sh dotnet "Intersect Server.dll"
```

Conserver les arguments de démarrage habituels après la commande. Lancer depuis le dossier de données du serveur. Le lanceur conserve stdin, transmet SIGTERM/SIGINT, attend la fermeture et retourne le code de sortie du serveur. `logs/host-exit-*.log` enregistre ce code et les compteurs mémoire du conteneur avant/après. Le code 137 peut correspondre à SIGKILL ; il ne prouve pas un OOM. Comparer les compteurs `oom_kill` et les événements de l'hébergeur.

Le dossier `logs` doit être sur le volume persistant de l'hébergement. Si le conteneur entier est tué, le lanceur meurt aussi : seul l'hébergeur peut alors fournir le code final et la raison de l'arrêt. Une panne de l'hôte ou SIGKILL ne peut pas être interceptée par le jeu. Ne pas promettre une cause certaine dans ce cas.

Option pour les erreurs natives .NET (à activer explicitement, puis désactiver après investigation) :

```bash
DOTNET_DbgEnableMiniDump=1 bash start-server-diagnostics.sh ./"Intersect Server"
```

Les dumps `logs/server-dump-*.dmp` peuvent être volumineux et contenir des données privées ; ne pas les publier. Ils nécessitent de l'espace libre et les permissions du système ; SIGKILL/OOM ne garantit pas un dump. Aucune collecte n'est activée par défaut.

## Au prochain incident

Conserver rapidement les quatre catégories de fichiers ci-dessus, l'heure avec fuseau, la version du serveur et l'action effectuée juste avant. Les archives de console tournent : éviter d'attendre plusieurs jours. Un journal nommé `crash-*` est créé à chaque lancement et ne signifie pas à lui seul qu'un crash a eu lieu.

## Vérification

```bash
dotnet run --project Utilities/CrashDiagnosticsTests/Intersect.CrashDiagnosticsTests.csproj
python3 scripts/test-server-diagnostics.py
```

Les tests lancent des processus isolés, dont une exception non gérée et un arrêt forcé. Ils n'utilisent aucune base de données de production.
