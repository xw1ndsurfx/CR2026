# Serveur de recette limité au poste local

Le serveur LiteNetLib accepte INTERSECT_LOCAL_TEST_ONLY=1 comme variable de processus : ses sockets de jeu écoutent exclusivement sur IPv4 127.0.0.1 et IPv6 ::1. Sans cette variable, le comportement réseau normal est conservé. Les clients et le protocole ne changent pas.

## Préparer une copie

1. Restaurer une archive dans un dossier indépendant. Vérifier les chemins avant extraction et refuser les liens et traversées de répertoires. Conserver l’archive originale.
2. Vérifier l’intégrité des bases restaurées. Utiliser uniquement les bases SQLite copiées ; MySQL demande un export et une restauration séparés. Une intégrité SQLite réussie ne prouve pas la cohérence temporelle entre fichiers.
3. Dans resources/config.json de la copie, choisir un port libre, désactiver UPnP et OpenPortChecker, vider les identifiants SMTP et Logiklik et diriger les intégrations vers une destination locale de test. Examiner les plugins et les autres intégrations avant lancement.
4. Désactiver Api.Enabled et UpdateServer.Enabled dans les appsettings applicables, avec les éventuelles adresses Kestrel limitées à la boucle locale. La variable de processus ne sécurise que le transport de jeu LiteNetLib.
5. Compiler le serveur avec la clé de handshake correspondant au client de test. Garder les clés privées, configurations, données personnelles et archives hors du dépôt.
6. Lancer depuis le dossier de données isolé avec INTERSECT_LOCAL_TEST_ONLY=1, --no-upnp et --no-port-check. Ne pas définir la variable globalement. Autoriser les migrations uniquement sur cette copie.
7. Contrôler les sockets réellement ouvertes et l’absence d’API publique avant de connecter le client. Copier le client et ses ressources sans lien vers les ressources installées ; configurer Host=127.0.0.1 et le même port.
8. Arrêter par la commande exit, vérifier les journaux et les bases, puis redémarrer la copie. Tester ensuite la sauvegarde d’une action réelle du joueur et sa persistance après reconnexion.

Le démarrage avec une sortie redirigée sous Windows évite désormais de lire la taille ou d’effacer un tampon de console absent. La console interactive conserve son comportement habituel.

La recette Steam/Logiklik nécessite son propre environnement d’intégration. Une copie privée avec les identifiants de production neutralisés ne valide pas cette intégration.
