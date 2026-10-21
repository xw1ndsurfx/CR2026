# Naval : boutiques, cale et équipage

## Fenêtres en jeu

- **Inventaire > Ammo** ouvre une fenêtre indépendante avec une grille de munitions. Sélectionnez un type, puis **Utiliser** ou **Retirer** (jusqu’à 10 unités par clic). Aucun bouton Déposer. Utiliser récupère automatiquement les munitions de ce type déjà présentes dans l’inventaire normal, jusqu’à 10 000 à la fois. Les achats vont directement dans le sac Ammo. Le boulet standard est gratuit.
- Au canon, **1 à 0** sélectionne les boulets; la souris vise et le clic gauche tire. Le niveau requis est celui de Canonnier; les bordées du pilote utilisent Sailing. Le sac Ammo reste accessible en mer.
- Le bouton **Réparer le navire** se trouve avec les commandes du bas. Il ouvre une fenêtre séparée de mini-jeux. Seuls les équipiers peuvent réparer en mer, à distance du quai; le propriétaire et le joueur à la barre ne peuvent pas réparer. Martelage, calfatage et pompage donnent de l’EXP de Charpentier naval après une réparation enregistrée. Aucun mini-jeu ne répare un bateau coulé.
- **Contrôle de la barre** est une fenêtre distincte, disponible à bord. Le propriétaire peut offrir la barre pendant 30 secondes à un équipier embarqué, puis la reprendre. Le pilote remplaçant gagne Sailing; les joueurs au canon gagnent Canonnier.
- À pied, l’approche d’un attribut **Q** ouvre automatiquement la flotte personnelle. En bateau, la proposition d’amarrage apparaît à proximité du quai. Le joueur débarque au point d’embarquement.
- Les fenêtres utilisent les boutons habituels avec leurs états normal, survolé et appuyé, ainsi que des listes défilantes. Il n’y a plus de panneau mélangeant toutes les activités navales.

## Cargaison et récompenses

- Chaque navire a une capacité de cale configurée dans son modèle. **MAX** apparaît lorsque la cale est pleine. Un lot de meilleure qualité remplace alors un lot de qualité inférieure; un lot égal ou inférieur n’est pas ajouté.
- **En mer, les lots sont scellés.** Le client reçoit seulement leur identité de lot, leur catégorie et leur qualité; les objets et quantités sont masqués aussi dans les données réseau et les notifications.
- Après amarrage et retour à terre au quai, la cale s’ouvre automatiquement avec le contenu révélé. Les lots peuvent être déchargés individuellement ou supprimés après confirmation. Le déchargement à bord est refusé par le serveur, même près du quai.
- Un naufrage produit **une seule réserve** d’EXP personnage et de butin. Elle est répartie entre les participants, sans copies supplémentaires. Les dégâts réellement infligés et les réparations enregistrées pendant l’engagement contribuent au partage. Le pilote actuel reçoit un poids de direction égal au double de la meilleure contribution individuelle. Un équipier inactif ne reçoit pas de part.
- Les quantités entières et l’EXP personnage de base sont conservées dans le partage; les arrondis sont répartis par les plus grands restes. Les bonus habituels du personnage restent appliqués par le système d’EXP du jeu. Sailing revient uniquement au pilote. Canonnier reste lié aux dégâts réels de ses tirs.
- Les notifications montrent l’EXP Sailing et personnage attribuée, les lots scellés récupérés, les améliorations de qualité et la cale pleine.
- Quitter le groupe ou en être exclu retire du bateau et détruit la cargaison personnelle encore embarquée. Le naufrage détruit également cette cargaison. Les objets déjà déchargés sont conservés.

## Shop Editor

Chaque boutique propose maintenant un type :

1. **Configuration boutique normale** : comportement existant conservé.
2. **Configuration boutique de munitions navales** : utilisez les listes habituelles d’objets vendus et rachetés pour sélectionner les objets de munitions existants et leurs prix/monnaies. La liste blanche ou noire de rachat et la réduction de rachat restent applicables. Les types de boulets doivent être reliés à ces objets dans System Editor > Naval.
3. **Configuration boutique de bateaux et compétences navales** : cliquez **Configurer les bateaux et compétences**. Les deux onglets permettent de régler les offres, prix, monnaie et coût de réparation de cette boutique. Les identifiants de modèles sont conservés. Les modèles et compétences proviennent de la configuration navale existante; aucun objet ni monnaie n’est inventé.

Les deux boutiques sont des fenêtres indépendantes, avec les colonnes achat et vente de l’interface de boutique normale. Les bateaux peuvent être revendus pour la moitié de leur prix, pondérée par leur état de coque; une cale non vide interdit la vente. Les compétences achetées sont permanentes. La réparation d’un navire encore à flot est proportionnelle aux dégâts; un navire coulé paie le prix complet de réparation.

Dans un événement NPC, utilisez **Ouvrir la boutique de munitions navales** ou **Ouvrir la boutique navale et réparations**, puis choisissez la boutique du bon type. Le catalogue par défaut conserve la compatibilité des événements déjà créés. La commande normale Ouvrir une boutique reconnaît également les nouveaux types.

Les boutiques ouvrent uniquement à terre. Chaque transaction est revalidée à portée du NPC et sauvegardée avec le sac Ammo, la flotte et l’inventaire. En cas d’échec de sauvegarde, le paiement et les objets sont restaurés.

## System Editor > Naval

Les modèles de bateaux, leur capacité, l’IA Herbonia, les courbes Sailing/Canonnier/Charpentier, les mini-jeux, les compétences, les sons, les icônes et les animations d’impact restent configurables ici. Arrêtez les bateaux avant de sauvegarder la configuration de navigation.

Les huit types de munitions sont standard, lourd, longue portée, foudre, chaîné, explosif, glace et maudit. Pour un type spécial, choisissez un **objet existant et empilable**, ses niveaux Sailing/Canonnier, unités par tir, dégâts, portée, recharge, ralentissement, icône dans resources/spells et animations. L’animation native d’impact sélectionnée a priorité sur l’image d’impact.

Les compétences sont vitesse d’éperonnage, toutes voiles dehors, à couvert, feu à volonté et bonus de réparation. Les achats se font à la boutique de bateaux; les compétences déjà possédées s’activent depuis les commandes du capitaine. À la barre, les touches **4** ouvrent les compétences possédées, **5** Ammo et **6** le contrôle de la barre. La barre terrestre est restaurée au débarquement.

## Quêtes et achievements

Les neuf objectifs navals sont disponibles dans les deux éditeurs : navires coulés, coups de canon réussis, dégâts de coque, réparations, accostages, lots déchargés, niveau Sailing, niveau Canonnier et niveau Charpentier naval.

Les niveaux sont des seuils absolus; les autres objectifs utilisent les compteurs enregistrés du personnage. Un nouveau succès peut donc reconnaître une progression navale déjà enregistrée. Les anciens identifiants d’objectifs d’achievements sont conservés.

## Vérification multijoueur sur le cloud

Après mise à jour du client, de l’éditeur et du serveur, vérifier avec un capitaine et deux équipiers : ouverture automatique près de Q, achats/ventes des deux boutiques, prise/reprise de barre, réparation à distance du quai, canon et munitions, partage d’un naufrage, cale MAX avec amélioration, retour à terre et déchargement individuel. Vérifier aussi le refus des achats en mer, du déchargement à bord et des réparations du capitaine, puis la perte du butin lors d’un départ du groupe.
