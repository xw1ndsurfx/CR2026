# Erreurs réseau et arrêts du serveur — 19 octobre 2026

## Ce que les journaux établissent

Le journal transmis contient 20 erreurs de désérialisation interceptées par `MessagePacker`, six erreurs d’authentification AES-GCM interceptées par le déchiffrement, un refus de mise de roulette faute de fonds et plusieurs lignes de démarrage sur différentes versions. Ces démarrages ne prouvent pas, à eux seuls, autant de crashs.

Le journal `crash-Intersect Server-2026_10_19-23_02_59_639-13.log` montre le serveur `fc63d6858` sous Ubuntu 24.04.4 / .NET 8.0.29 atteignant sa boucle principale à 23:03 UTC, soit **19:03 au Québec**. La console complémentaire montre des connexions acceptées à **19:04 et 19:13**. Aucun extrait fourni n’indique l’exception ou le code de sortie du processus précédent. **La cause de l’arrêt signalé vers 19 h n’est pas confirmée.**

Le répertoire Windows du projet a été consulté en lecture seule : les journaux serveur qui s’y trouvent sont anciens et ne remplacent pas ceux du conteneur Ubuntu. Aucun fichier de la publication de production n’a été modifié. Les avertissements sur le dossier de plugins absent, UPnP et le certificat ne constituent pas une preuve de crash ; le serveur poursuit son démarrage dans la console fournie.

## Défauts corrigés et reproductions

### Lecture des trames chiffrées

Le transport utilisait une longueur reçue du réseau pour allouer un tableau avant de vérifier les octets réellement disponibles. Les marqueurs de trame étaient aussi lus sans vérifier leur présence. Les longueurs négatives, absentes ou supérieures au corps reçu sont maintenant rejetées avant allocation ; les marqueurs tronqués sont rejetés proprement.

AES-GCM vérifie maintenant l’ensemble en-tête/nonce/tag/corps avant toute découpe et allocation. Le tag doit avoir la taille attendue par l’instance. Les surcharges avec offset/longueur invalide retournent un échec au lieu de lever une exception. Le repli AES-CBC vérifie également les longueurs, la taille de l’IV et du corps chiffré, ainsi que la fin réelle du flux déchiffré avant d’exposer le résultat.

Un échec AES-GCM laissait auparavant l’instance cryptographique acquise hors du pool. Les opérations la rendent désormais dans un `finally`, y compris en cas d’authentification refusée. Le test enchaîne 200 échecs, vérifie une seule instance conservée et redéchiffre ensuite un message valide. Les tags incorrects restent refusés : aucun contrôle cryptographique n’est désactivé. Les clés de remplacement doivent avoir la longueur exacte ; la fermeture du pool est idempotente.

Le chemin de diagnostic Debug tentait de rechiffrer après un échec de déchiffrement, alors que cette opération auxiliaire peut ne pas être prise en charge. Ce traitement spéculatif est retiré ; le message en échec est simplement refusé, en Debug comme en Release.

### Compatibilité des types de messages

Les identifiants numériques de messages sont attribués selon une liste triée de types. Ajouter un type peut donc décaler les identifiants suivants. Le contrôle historique de version utilise une chaîne fixe et ne détecte pas ces différences. Un paquet binaire de connexion/statut peut alors être interprété comme un paquet de traduction ou de carte attendant du texte, ce qui correspond à la forme des erreurs du journal.

Chaque enveloppe inclut désormais une empreinte SHA-256 de sa table ordonnée et des contrats MessagePack accessibles : clés, types de champs, unions et énumérations, avec un calcul indépendant de la langue et de l’ordre d’énumération. Elle distingue Debug et Release, car le contenu chiffré de `HailPacket` diffère déjà entre ces configurations. L’empreinte est calculée à l’enregistrement des types, pas à chaque envoi.

La réception vérifie l’empreinte **avant** de choisir le type local et de décoder son contenu. Une table différente ou une ancienne enveloppe sans empreinte est refusée. Une demande de connexion incompatible reçoit le statut de version incompatible déjà géré par l’interface. Les clés inconnues et contenus malformés restent interceptés. Les options MessagePack utilisent aussi `UntrustedData`.

L’empreinte est un contrôle de compatibilité, pas une authentification. Les contrôles de connexion, les clés et les validations d’autorisation existants restent nécessaires.

**Conséquence de publication : serveur, client et Game Editor doivent être reconstruits depuis la même branche/version et la même configuration, normalement Release, puis mis à jour ensemble.** Mettre uniquement le serveur à jour refusera les anciens programmes. La PR est indépendante des PR navale et de sauvegarde ; intégrer d’abord les corrections souhaitées à la branche de publication approuvée. Aucun merge ni déploiement n’est effectué automatiquement.

## Diagnostic des prochains arrêts

Après le démarrage, le journal de crash reçoit un relevé immédiat puis chaque minute : mémoire résidente et privée du processus, mémoire gérée/GC, mémoire disponible pour le GC, temps CPU et compteurs de threads/file du pool.

Sous Linux, le relevé tente aussi de lire les limites et compteurs du conteneur : `memory.current`, `memory.max`, `memory.events` (dont `oom`/`oom_kill`), ou les fichiers de mémoire cgroup v1 lorsque disponibles. Ces lectures sont limitées en taille. Le signal SIGTERM est journalisé sans annuler son traitement par le système ou les gestionnaires existants.

Un SIGKILL, un arrêt du conteneur par manque de mémoire ou certaines défaillances natives peuvent empêcher tout gestionnaire .NET de s’exécuter. L’absence d’une ligne finale ne permet donc pas de conclure seule à un manque de RAM. Il faut comparer la dernière mesure au message/code de sortie de l’hébergement. Aucun relancement automatique, changement de limite mémoire ou modification des bases n’est ajouté.

Le refus de roulette (« Buy-in 1000, Available 36 ») est une règle de fonds insuffisants déjà interceptée ; ce n’est pas une trace fatale. Les fonds en séquestre et les tables `PokerMoney` ne doivent pas être supprimés pour contourner ce refus.

## Vérifications et essai sur serveur de test

Les tests serveur comprennent notamment : chaque préfixe tronqué d’un message AES-GCM/AES-CBC, longueurs énormes annoncées dans une petite trame, offsets invalides, 2 000 entrées chiffrées malformées déterministes, réutilisation du pool après échecs, déchiffrement valide, suppression de la ré-encryption spéculative, aller-retour d’une demande de connexion réellement chiffrée, refus des anciennes tables avant décodage, stabilité culturelle de l’empreinte et changement de contrat conservant le même nom de type. Les métriques de processus et des fichiers cgroup simulés sont également vérifiés. Un workflow les exécute sous Windows et Ubuntu, en Debug et Release.

Après approbation et mise à jour de l’environnement de **test** :

1. Utiliser un serveur, un client et un éditeur issus de la même publication Release ; vérifier connexion, navigation, chargement d’une map et édition ordinaire.
2. Vérifier que le journal `logs/crash-Intersect Server-*.log` reçoit un `Runtime heartbeat` au démarrage puis après une minute. Sur le conteneur, relever notamment `memory.max`, `memory.current` et `memory.events` si disponibles.
3. Essayer un ancien client de test : il doit être refusé ; le serveur doit continuer de fonctionner sans tenter de décoder le mauvais type de message.
4. Redémarrer normalement le conteneur de test et vérifier la trace SIGTERM lorsqu’elle est utilisée par l’hébergeur, ainsi que les traces d’arrêt existantes.
5. En cas de nouvelle fermeture, conserver le **journal complet du processus arrêté**, les dernières lignes de console de l’hébergement après l’arrêt, son code de sortie, l’heure exacte et la limite RAM. Le journal créé après le redémarrage ne remplace pas celui du processus précédent.

Les tests Windows locaux ne constituent pas un essai connecté sur le conteneur de production. Les relevés Linux et la réception effective des signaux restent à vérifier dans cet environnement de test ; aucun accès au conteneur de l’hébergeur n’est disponible dans cette tâche.
