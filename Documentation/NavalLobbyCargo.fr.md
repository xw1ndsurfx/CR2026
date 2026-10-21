# Naval : équipage, cale et réparations

## Réparation

Le prix de réparation configuré dans la boutique représente une perte totale. Un bateau encore à flot paie seulement la fraction de coque manquante, arrondie à la monnaie supérieure et plafonnée sous le prix d’un naufrage. Exemple : prix total 1 000 Aureons, coque 70/100 : 300 Aureons ; coque 0/100 : 1 000 Aureons. Coque intacte : aucune réparation. Un tarif nul reste gratuit. Le devis affiché et le paiement utilisent la même règle serveur et la monnaie de l’offre.

## Cargaison individuelle

Ouvrir la cale en navigation. Chaque carte possède Décharger et Supprimer. Le déchargement porte sur le lot complet sélectionné, à l’arrêt près d’un quai Q ; son contenu rejoint le sac, les autres lots restent en cale. Si le sac est plein ou si la sauvegarde échoue, rien n’est transféré. Supprimer fonctionne aussi en mer, demande confirmation, détruit seulement le lot choisi et ne donne rien. Les membres gèrent leur propre part. Le déchargement global depuis la capitainerie reste disponible après débarquement. La cargaison restante est toujours perdue au naufrage.

## Lobby naval et canon automatique

Le capitaine lance son bateau personnel et l’arrête près d’un Q. Dans Lobby, sélectionner Équipage naval et créer l’annonce. Rejoindre ajoute réellement le joueur au groupe, l’embarque et ouvre la vue canonnier. Le groupe, la capacité du navire, les blocages entre joueurs, le combat et les activités privées sont contrôlés côté serveur. Le capitaine doit encore attendre au quai ; aucune entrée dans un bateau en mouvement, coulé ou remplacé n’est acceptée. Une sauvegarde d’embarquement refusée annule l’ajout au groupe. Fermer l’annonce conserve l’équipage déjà présent.

L’arrivée ordinaire comme équipier au Q ouvre aussi automatiquement le canon. Le premier flanc complet bascule une fois vers l’autre flanc. Un équipier peut toujours fermer la vue, qui reste fermée aux snapshots suivants. À la sortie du bateau, le point de retour est celui conservé à l’embarquement ; pour un embarquement via lobby, c’est la position du joueur avant le transfert. Le HUD compte capitaine et membres à bord et affiche la capacité du bateau.

## Animations d’impact

System Editor → Naval → Munitions : chaque type possède Animation d’impact, choisie parmi les vraies animations du jeu. Le serveur fixe l’animation à la création du boulet et la diffuse à l’impact sur l’eau, un obstacle ou une coque. Les deux couches visuelles, les frames, la cadence, les répétitions, la taille, les décalages et la transparence sont affichés en top-down et en vue canonnier. Une animation absente/non sélectionnée conserve les effets océaniques habituels. Le son propre à l’animation, s’il est défini, remplace le son d’impact habituel. Les effets sont limités à vingt secondes ; la lumière volumétrique native n’est pas reproduite en perspective.

## Décors et Herbonia

Le rendu 2,5D reconstruit les objets cohérents à travers les calques et partage leurs pieds entre maps voisines chargées. Les planches de quai répétées restent à plat ; les poteaux et les pieds des barils sont séparés du sol. Cela reste du décor en billboards, sans vrai modèle 3D des maisons. L’art peint entièrement sur Ground reste au sol.

Les planches Herbonia 001 et 002 utilisent des bandes de découpage suivant les espaces entre poses, au lieu d’un rectangle qui prenait la proue du voisin. Les pixels PNG d’origine sont conservés ; distribuer les fichiers Ship_Herbonia_001.png.naval.json et Ship_Herbonia_002.png.naval.json avec le nouveau client, ainsi que l’image 002 maintenant incluse. Les deux vues utilisent ces mêmes bandes.

Installer ensemble client, serveur et éditeur. Ces changements étendent les paquets sans déplacer les anciennes clés. Vérifier dans une session avec deux joueurs : annonce navale, entrée automatique, compteur, retour au point initial, réparation endommagée/coulée, déchargement d’un lot, annulation/suppression, puis effets choisis et passage de limites de maps à Polonya. Les tests automatisés ne remplacent pas une vérification graphique et réseau dans le jeu installé.
