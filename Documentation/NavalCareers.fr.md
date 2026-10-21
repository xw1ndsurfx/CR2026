# Navigation : équipage, métiers et munitions

## Utilisation en jeu

- **Inventaire > Ammo** : sac de munitions distinct. Déposez ou retirez jusqu’à 10 objets par clic. Le boulet standard reste gratuit. Les munitions spéciales sont débitées du sac Ammo avant la création des projectiles ; une transaction refusée ne produit aucun tir.
- Au canon, **1 à 0** choisit le type de boulet. Le tir reste à la souris. Le niveau requis est celui de **Canonnier** ; les bordées à la barre utilisent le niveau de **Sailing**. Une bordée consomme une munition par canon effectivement tiré (trois au maximum).
- Dans la vue canonnier, **Réparer le navire** ouvre trois activités : martelage avec une zone de rythme verte, calfatage des fuites indiquées et pompage alterné. Vous libérez le poste de canon pendant la partie. La réussite répare un bateau encore à flot et donne de l’EXP de **Charpentier naval**, uniquement lorsque la réparation a été acceptée et enregistrée. Un bateau coulé se répare toujours à la boutique.
- Les trois métiers **Navigation / Canonnier / Charpentier naval** apparaissent dans les professions du personnage. Les deux premières progressions existantes sont conservées ; leur EXP n’est pas copiée dans des variables de métier indépendantes.
- **Boutique de bateau > Compétences de navigation** : achat permanent en Auragems, sous condition de niveau. Après achat, activation depuis le panneau naval pendant que vous êtes à la barre. Les touches navales **4** ouvrent les compétences, **5** le sac Ammo et **6** les activités. Les raccourcis terrestres sont restaurés à la sortie du bateau.
- Compétences : vitesse d’éperonnage (vitesse et contact contre les navires IA, une fois par cible et activation), toutes voiles dehors (vitesse), à couvert (réduction des dégâts reçus), feu à volonté (dégâts des canons), bonus de réparation.
- **Équipage > Offrir la barre** : un membre du groupe déjà embarqué reçoit une proposition de 30 secondes. Il doit l’accepter. Le propriétaire passe au canon ; il peut reprendre la barre dans les activités navales. Le bateau reste sa propriété. Le propriétaire récupère automatiquement la barre si le remplaçant quitte le groupe ou le bateau.
- Près d’un quai **Q**, la proposition d’amarrage apparaît. Un clic lance l’ancrage puis l’accostage après l’arrêt. Le joueur revient au lieu où il a embarqué. Fermer la proposition la masque jusqu’à la prochaine approche.
- **Quitter le groupe ou en être exclu retire le joueur du bateau et détruit sa cargaison navale personnelle, y compris si le propriétaire choisit de quitter le groupe. Le départ du dernier équipier ne force pas un capitaine resté à bord à abandonner son navire.** Cette perte n’attribue aucun objet à l’inventaire. Les objets déjà déchargés restent acquis.
- La cale conserve les actions par lot : décharger au quai, ou supprimer après confirmation. Les boutons utilisent des légendes explicites et les nombres formatés utilisent des espaces compatibles avec la police.

## Configuration dans System Editor > Naval

Arrêtez les bateaux avant de sauvegarder la configuration de navigation.

### Munitions

L’onglet **Munitions de canon** contient huit types : standard, lourd, longue portée, foudre, boulets chaînés, explosif, glace et maudit.

Pour chaque type spécial, choisissez un **objet existant** et empilable de la base du jeu. Aucun identifiant d’objet ou de monnaie n’est créé automatiquement. Sélectionnez les niveaux Navigation et Canonnier, les unités par tir, les multiplicateurs de dégâts / portée / recharge, le ralentissement et sa durée, l’icône dans `resources/spells`, les images de projectile et d’impact dans `resources/ocean`, ainsi que l’animation native d’impact si désirée. L’animation native sélectionnée a priorité sur l’image d’impact.

Pour la vente, activez **For sale**, choisissez le prix unitaire et la monnaie existante. Sans identifiant explicite, le serveur recherche une monnaie unique nommée Aureons ; une monnaie absente ou ambiguë interdit la transaction.

Dans l’événement d’un NPC, section boutiques, ajoutez **Ouvrir la boutique de munitions navales**. La session est liée au NPC, à sa position et à une durée limitée ; ouvrir le sac Ammo depuis l’inventaire ne donne pas accès à l’achat.

Les cinq nouvelles planches fournies sont livrées sous `Cannon_Lightning.png`, `Cannon_Chain.png`, `Cannon_Explosive.png`, `Cannon_Ice.png` et `Cannon_Cursed.png`, avec leurs fichiers `.effects.json`. Les images d’origine sont conservées ; les bandes de découpage séparent les animations voisines.

### Réparation et compétences

- **Mini-jeux de réparation** : activation, points de coque, EXP par réussite, durée, nombre d’actions, durée minimale, recharge et courbe du métier.
- **Compétences de navigation** : les cinq types sont configurables par niveau, prix, monnaie, durée, recharge, puissance et icône. La monnaie par défaut est une monnaie unique nommée Auragems. L’achat reste lié à la session de la boutique de bateaux.
- **Progression Sailing / Canonnier** : les courbes existantes restent la source de vérité. Dans l’éditeur générique des professions, les trois métiers navals sont indiqués ; leurs paramètres se règlent dans l’éditeur naval.
- **IdleGraphics** permet de choisir la planche à l’arrêt. Les nouveaux idles Royal et Herbonia sont utilisés automatiquement pour les modèles d’origine correspondants, à pleine coque et arrêtés. Les modèles personnalisés gardent leur image jusqu’à une sélection explicite. Les images de dégâts et de naufrage gardent priorité.

### Quêtes

Neuf tâches natives sont ajoutées : couler des navires IA, réussir des impacts, infliger des dégâts de coque, terminer des réparations, amarrer le bateau, décharger des lots, atteindre un niveau Navigation, Canonnier ou Charpentier naval. Pour chacune, configurez la quantité ou le niveau dans l’éditeur de quête. Les tâches de niveau utilisent un objectif absolu ; les autres comptent les actions réussies pendant la tâche active. Les membres présents reçoivent la tâche de naufrage ; l’EXP Sailing va au joueur qui tient la barre, l’EXP Canonnier aux dégâts effectivement infligés au canon.

## Mise à jour

Cette évolution modifie les messages réseau : client, éditeur et serveur doivent être mis à jour ensemble. Les profils et flottes existants conservent leurs données et progressions ; les nouveaux champs sont enregistrés dans le stockage naval existant. Les configurations antérieures reçoivent les cinq types supplémentaires sans modifier les types déjà configurés, et sans rendre leurs objets automatiquement achetables.

La configuration des objets, monnaies et prix s’effectue avec votre base de contenu réelle. Les essais automatisés ne remplacent pas un essai multijoueur sur le serveur cloud, notamment pour l’acceptation du prêt de barre et les réparations simultanées.
