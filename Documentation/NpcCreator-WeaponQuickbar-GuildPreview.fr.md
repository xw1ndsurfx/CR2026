# PNJ, barre des compétences et guilde à la connexion

## Auto NPC Creator

Le créateur contient l’onglet de génération et les véritables onglets de NPC Editor : général, statistiques, combat, comportement, butin et familiers. Les sons, les animations, les déplacements tactiques, les avertissements d’attaque, les combos de boss, les événements et les intervalles individuels des sorts restent donc accessibles avec leurs sélecteurs habituels.

Utiliser **Générer et vérifier** pour appliquer le profil simulé, puis ajuster les paramètres dans les autres onglets. **Conserver les modifications du PNJ** valide le brouillon ; **Annuler** restaure l’état précédant l’ouverture du créateur, y compris les modifications non encore enregistrées auparavant. L’enregistrement vers le serveur reste celui de NPC Editor. La génération d’un profil de combat reste désactivée pour les familiers.

## Compétences d’arme

La barre utilise des cases d’icônes et suit la position réelle de la barre rapide principale. Le nom et les caractéristiques sont dans l’infobulle ; les secondes restantes apparaissent sur l’icône pendant le temps de recharge. L’icône de l’arme sert de repli quand aucune icône de compétence n’est disponible.

Dans **Weapon Mastery Editor → Mastery talent tree**, sélectionner une compétence active et choisir son image dans **Icône de compétence (resources/spells)**. Le sélecteur lit les PNG du dossier `resources/spells` de l’éditeur et conserve une référence existante même si le fichier manque. Distribuer également le PNG aux clients.

Le bouton **Barre rapide des compétences d’arme** règle l’activation de la barre, la taille des cases, leur espacement, le nombre maximal de colonnes et l’espace au-dessus de la barre principale. Ces réglages et les icônes sont enregistrés dans la configuration des maîtrises sur le serveur. Une reconnexion ou une actualisation des maîtrises transmet la configuration au client.

La barre reste masquée en bateau, dans les mini-jeux et lorsqu’une fenêtre de jeu est ouverte, notamment Daily Rewards.

Le joueur dispose de dix cases. Dans **Weapon Masteries**, il choisit une case puis **Ajouter à la case** auprès d’une compétence active apprise, ou **Vider la case sélectionnée**. Le serveur conserve cette disposition par personnage et catégorie d’arme ; changer d’arme ou se reconnecter retrouve la disposition correspondante. Une compétence déplacée libère son ancienne case et une case vidée reste vide.

Dans **Settings → Controls**, les dix entrées de compétences d’arme permettent de personnaliser deux raccourcis par case, selon le fonctionnement habituel des réglages. Les valeurs initiales sont `Alt+1` à `Alt+0`. Ces combinaisons n’activent pas simultanément la barre principale et ne déclenchent pas une compétence pendant la saisie de texte. La touche configurée apparaît dans la case.

## Guilde à la sélection du personnage

Le serveur résout les noms depuis les identifiants de guilde enregistrés, avant l’entrée en jeu. Cette lecture ne dépend plus du chargement de l’objet de guilde du personnage et ne modifie pas son appartenance. Un personnage sans guilde, ou dont la guilde n’existe plus, conserve l’affichage vide habituel.

Ces changements nécessitent les versions mises à jour du client, de l’éditeur et du serveur. Ils n’ajoutent aucun profil spécifique Intel HD 3000 au client normal.
