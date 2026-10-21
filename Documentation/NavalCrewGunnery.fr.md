# Canonnier et postes de canon d’équipage

## Installer et activer

Installer ensemble le client, le serveur, le Game Editor et les ressources de cette version. Le nouveau paquet fait partie du registre natif : ne pas mélanger les anciens et nouveaux exécutables. Aucune migration SQL supplémentaire ; l’EXP de canonnier est enregistrée dans les données navales existantes du personnage.

Dans System Editor > Naval :

- Navigation et capitainerie : conserver Activer la navigation et les canons activés.
- Canons d’équipage et canonnier : activer les postes, choisir le nombre par flanc (1–4, défaut 3), la recharge (défaut 2500 ms), l’angle de visée maximal (défaut 45° de chaque côté de l’axe) et la progression. La capacité d’équipage du modèle reste séparée, capitaine compris (1–8).
- Munitions des canons : trois profils possibles. Le boulet standard reste gratuit, accessible au niveau 1. Les deux autres consomment des objets réels de l’inventaire du tireur. Choisir un objet empilable dans ItemId, la quantité consommée, le niveau requis et les coefficients de dégâts/portée/recharge. Aucun objet ni identifiant n’est inventé.
- Un profil spécial sans objet associé reste indisponible. Distribuer/vendre ces objets par les boutiques et événements déjà utilisés dans le jeu ; aucun stock gratuit de boulets spéciaux n’est ajouté.
- Sons, icônes et effets : Décor du poste de canon utilise resources/ocean/Ship_Crew_Cannon.png. La liste accepte les PNG accompagnés d’un fichier .crew.json valide et présente un aperçu. Les sons et effets de tir/impact existants restent utilisés.

Sauvegarder les sections modifiées. Les deux nouvelles sections partagent la révision de navigation, sans écraser la révision de boutique. Les anciennes configurations reçoivent les valeurs par défaut ; les anciennes EXP Sailing et flottes sont conservées.

## Jouer avec deux comptes

1. Former un groupe natif, ouvrir l’embarquement depuis Équipage, embarquer au Q puis rejoindre le navire du capitaine.
2. L’équipier ouvre la vue avec le troisième raccourci naval (3 par défaut).
3. Choisir Bâbord ou Tribord, puis Prendre le canon. Le serveur attribue le premier poste libre sur ce flanc et affiche son numéro. Un autre équipier ne peut pas prendre le même poste.
4. Déplacer la souris dans la vue : le viseur suit le pointeur dans l’angle autorisé. Cliquer avec le bouton gauche pour tirer un boulet. Les clics sur les autres boutons de la fenêtre ne tirent pas par eux-mêmes dans la vue.
5. Cliquer le sélecteur de munition pour parcourir standard, lourd et longue portée. Le niveau requis, la gratuité ou le nombre de tirs disponibles sont affichés ; l’infobulle indique les coefficients. Le bouton de tir et le clic de visée respectent le niveau, les objets possédés et le rechargement du serveur.
6. Changer de flanc demande un nouveau poste. Si ce flanc est plein, le poste précédent reste conservé ; revenir sur son flanc pour l’utiliser. Fermer la fenêtre libère le poste. En cas de message perdu, le poste expire après 3,5 secondes sans renouvellement. Quitter puis reprendre un poste ne réinitialise pas sa recharge.
7. Le capitaine continue de piloter, d’ancrer et d’utiliser ses bordées de trois boulets via 1/2. Les équipiers tirent avec leurs propres postes et délais ; ils ne prennent pas le contrôle du bateau.

## Progression et récompenses

Le canonnier gagne l’EXP des dégâts réellement retirés aux navires Herbonia par ses propres tirs : 1 EXP par PV retiré par défaut. Pas d’EXP sur l’eau, les obstacles, les bateaux de test ou les joueurs. Les dégâts excédant les PV restants ne comptent pas. Une collision ne récompense pas plusieurs fois le même boulet. La barre Canonnier affiche un niveau et une EXP distincts de Sailing ; le niveau débloque les munitions mais ne multiplie pas implicitement les dégâts.

La récompense de naufrage existante est conservée : le navire tireur garde son capitaine comme origine. Sailing revient au capitaine ; l’EXP personnage et le tirage de butin restent partagés à tous les membres présents. Le canonnier du tir fatal reçoit donc sa part normale d’équipage en plus de sa propre progression de canonnier.

L’EXP de canonnier est appliquée et sauvegardée sous le verrou du joueur concerné, en dehors du verrou du monde ; un échec restaure la valeur précédente et remet le gain en attente. Une récompense pas encore sauvegardée peut être perdue lors d’un arrêt brutal du serveur, comme l’attente Sailing existante.

## Visée et rendu

La caméra utilise le vrai point du canon sur le flanc et la longueur du bateau. Le rayon de la souris est calculé à partir de la même projection que l’image en perspective. L’axe horizontal choisit l’angle relatif au navire ; l’axe vertical choisit la distance sur le plan de l’eau, bornée par la portée de la munition. Au-dessus de l’horizon, la portée maximale est utilisée. Les boulets utilisent toujours le déplacement et les collisions navales balayées du serveur : cette version ne simule pas de trajectoire verticale parabolique.

Le décor fourni a été copié sans modification. La planche mesure 1448 × 1086 en RGBA ; trois rectangles explicites de premier plan sont déclarés dans Ship_Crew_Cannon.png.crew.json (centre : x=488, y=363, largeur=476, hauteur=358 ; gauche : x=0, y=363, largeur=484, hauteur=358 ; droite : x=968, y=363, largeur=480, hauteur=358). Le décor passe entre ces trois poses selon la visée, sans supposer une grille uniforme ni présenter les huit dessins comme une animation du tir. Le viseur se déplace réellement et le premier plan suit la visée. L’image entière reste disponible dans les ressources.

Les limites de terrain et de visibilité du premier aperçu restent décrites dans NavalCannonView.fr.md : cartes réellement chargées, relief non extrudé, entités reçues du serveur uniquement. Aucun pont d’instance où marcher n’est ajouté.

## Vérifications techniques et essais à terminer

Tests automatisés : postes exclusifs, faux jetons et angles refusés, niveau requis, cadence, expiration, reprise sans recharge réinitialisée, consommation/restauration d’inventaire, sauvegarde/reprise de progression, sérialisation, impact/naufrage réel, partage des récompenses, projection aller-retour et déplacement natif de souris dans la fenêtre. Éditeur natif : huit sections, réglages et munitions sauvegardés dans la bonne révision. Traductions en/fr/es.

À confirmer sur le serveur de test avec deux clients : visée sur un ennemi mobile pendant une rotation/traversée de map du capitaine, effets/sons, plusieurs postes simultanés, objets spéciaux réellement retirés une seule fois, fermeture/déconnexion/mort/naufrage et reprise de poste, progression et inventaire après reconnexion. Confirmer aussi le premier plan et les performances sur Intel HD 620. Les tests de contrôles et de calcul ne remplacent pas une vérification du rendu GPU connecté.


### Barre et progression selon le rôle

Lorsqu’un équipier occupe un poste de canon, les dix raccourcis temporaires sont réservés aux munitions : 1 = standard gratuit, 2 = lourd, 3 = longue portée. Les cases 4 à 0 restent désactivées tant qu’aucun autre type n’existe. La touche ou le clic sur la case sélectionne le boulet ; le clic gauche dans la vue canon tire. La quantité indique les tirs disponibles ; ∞ indique le standard gratuit. Les types indisponibles (niveau/stock/configuration) sont désactivés et la sélection est surlignée. Fermer la vue libère le poste et rétablit les actions d’équipage ; débarquer restaure les raccourcis ordinaires.

La barre de progression au-dessus des raccourcis affiche Canonnier pour l’équipage et Sailing pour le capitaine. Seul le capitaine gagne l’EXP Sailing des naufrages ; les équipiers conservent le même butin et l’EXP personnage. Leur EXP Canonnier reste liée aux dégâts réels de leurs propres tirs. Les récompenses mémorisent la part Sailing au naufrage pour que le débarquement ou un changement de rôle avant sauvegarde ne change pas l’attribution.
