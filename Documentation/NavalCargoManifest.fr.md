# Cale du navire : liste défilante et icônes

La fenêtre de cale accessible avec le raccourci naval 8 reprend les cadres brun et or du marketplace. Une carte indépendante représente chaque lot : vraie icône de l’objet, nom traduit, quantité, catégorie et qualité de 1 à 5 avec repères colorés. Le comptage reste par lots : deux lots du même objet occupent toujours deux places de cale.

Une scrollbar verticale remplace Précédent/Suivant. Tous les lots sont accessibles, triés par qualité décroissante. Les longues appellations sont ajustées à la ligne, avec le nom complet sur l’infobulle de la carte. Les rappels de déchargement au Q et de perte au naufrage sont séparés en deux labels; aucun retour à la ligne ni symbole de multiplication n’est utilisé pour les lignes de cargaison.

Les icônes utilisent l’icône et la couleur de l’objet existant via le même chargement resources/items que le marketplace. Une icône absente laisse son cadre vide; aucun autre objet n’est substitué. Aucun nouveau PNG ni fichier UI à copier. Le défilement est conservé lorsque le serveur répète ses snapshots et lorsque la cargaison change; un changement de bateau repart en haut. Les lignes sont reconstruites seulement sur changement de contenu, de langue ou de disposition, pas à chaque image.

La fenêtre adapte sa taille à la résolution, avec une taille maximale de 720 × 610. Textes en français, anglais et espagnol. Chaque lot propose Décharger et Supprimer. Décharger transfère ce lot entier au sac si le bateau est arrêté près d’un Q ; les autres lots restent dans la cale. Supprimer demande confirmation et libère la place sans récompense. Les deux opérations ciblent votre propre cargaison et sont annulées si la sauvegarde échoue. Le bouton historique de capitainerie permet toujours de tout décharger à terre.

## Test dans votre client
1. Installer le nouveau client avec les fichiers correspondants de cette version. Ouvrir la cale en navigation avec 8.
2. Vérifier chaque icône, le nom de l’objet, la quantité, la catégorie et les cinq repères de qualité. Les objets doivent posséder une icône existante dans votre contenu; un cadre vide indique une ressource absente/non définie.
3. Avec au moins dix lots, descendre à la molette puis tirer la scrollbar jusqu’au dernier lot. Attendre plusieurs secondes : la liste ne doit pas revenir en haut. Couler un ennemi : vérifier le maintien du défilement pendant l’ajout du butin.
4. Fermer/rouvrir la fenêtre; débarquer puis changer de bateau; tester une cale vide. Vérifier aussi la résolution habituelle et une fenêtre plus petite.

## Validation
45 tests client réussis, dont contrôles Gwen réels avec rendu de mesure synthétique : 20 cartes à 1920 × 1080, 800 × 600 et 640 × 480, demande des icônes d’objets, quantité, débordements de lignes, scrollbar, stabilité des widgets/défilement, changement de bateau, naufrage, objets inconnus et cale vide. Ces contrôles ne sont pas une capture GPU du jeu installé : le rendu final avec vos vraies icônes reste à confirmer en jeu.
