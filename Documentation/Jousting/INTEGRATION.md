# Joutes Royales — installation et éditeur guidé

Cette version comprend l’éditeur guidé, les médailles royales et leur boutique, ainsi que les duels entre deux comptes. Elle conserve la charge PvE, les collisions, les montures et le Cinematic Director natifs. Le projet V2 complet reste en développement.

## Préparer les ressources

Copier les ressources du paquet local dans les dossiers `resources/images/jousting` et `resources/tilesets` du client et de l’éditeur utilisés pour les essais. Les sprites William doivent se trouver dans `resources/images/jousting/William`. Les fichiers graphiques restent locaux ; ils ne sont pas publiés dans le dépôt public.

Les éléments de décor sont découpés sur la grille 16 × 16. Les cellules d’animation William sont de 128 × 128 et contiennent une marge transparente ; elles ne représentent pas des tuiles de terrain.

## Créer une arène sans saisir d’identifiant

1. Ouvrir une carte de test, puis **Mini-Games → Éditeur de joutes**.
2. Cliquer **Nouvelle arène sur cette carte**. Son nom apparaît dans la liste de gauche ; le modifier dans **1. Préparer l’arène**.
3. Choisir **William** dans **Monture du chevalier**. S’il est absent, cliquer **Ajouter le préréglage William**, attendre l’actualisation puis le sélectionner. Ce bouton ajoute la définition avec les bonnes dimensions et animations sans supprimer les autres montures. Il ne copie pas les images : les fichiers fournis doivent être installés comme indiqué ci-dessus.
4. Cliquer **Placer le départ du joueur**, puis sur une case de la carte. Cliquer **Placer l’arrivée**, puis sur sa colonne. L’arrivée reste automatiquement sur la ligne du joueur. La distance doit être paire, de 4 à 64 cases.
5. Cliquer **Placer le couloir adverse**, puis sur une autre ligne. Les départs et couloirs doré/bleu sont indiqués sur la carte ; les positions et les problèmes apparaissent à droite. Ces trois boutons fonctionnent avec un clic gauche ordinaire. Les anciens raccourcis restent disponibles.
6. Cocher **Activer cette arène**, puis **Enregistrer**. Attendre la confirmation du serveur. Si elle échoue, les modifications restent dans la fenêtre ; les limites de carte et les ressources sont contrôlées de nouveau côté serveur.

**2. Règles du match** présente les durées en secondes, les points et le nombre de passes. **5. Zones de visée** permet de modifier les rectangles en pourcentage du sprite rapproché. La zone précise doit rester dans le plastron ; le casque et le cheval ne sont pas des cibles valides par défaut.

## Raccorder les cinématiques

Dans **3. Cinématiques**, chaque moment possède une liste de scènes nommées et un bouton **Créer ou modifier cette cinématique**. Choisir **Aucune (facultatif)** pour créer une base, ou choisir une scène existante pour la réutiliser.

Le bouton ouvre le Cinematic Director natif. Une scène déjà liée se rouvre avec son contenu et son identité, au lieu de recréer une nouvelle base. Sa sauvegarde lie le résultat à l’arène ; enregistrer ensuite l’arène. Un événement commun classique se modifie dans l’éditeur d’événements.

Les sources de charge et d’impact laissent le mouvement au serveur. Aucun réglage interne `AllowPlayerControl` ni identifiant n’est à saisir dans l’éditeur de joutes. Les scènes ordinaires conservent leur comportement existant.

## Économie active

Les récompenses en **médailles royales** sont activées par défaut. Une victoire rapporte de 5 à 25 médailles selon la difficulté de l’IA ; une défaite terminée rapporte 1 médaille. Une touche rapportant des points est nécessaire. Un abandon ne rapporte rien. Le plafond par défaut est de 100 médailles par jour UTC, avec 60 secondes entre deux versements.

L’onglet **4. Récompenses et économie** permet de désactiver les versements, régler le plafond et le délai, et ajouter des offres : choisir un objet empilable par son nom, sa quantité et son prix, puis **Ajouter une offre** et **Enregistrer**. Aucune monnaie ni aucun objet existant n’est choisi arbitrairement pour une boutique vide.

Le solde, l’expérience de joute, les matchs et les victoires sont conservés dans la sauvegarde native du personnage. Chaque résultat serveur possède un reçu pour empêcher les doubles versements. Les achats enregistrent ensemble les médailles, les objets et le reçu ; un inventaire plein ou un échec de sauvegarde ne consomme pas les médailles. Les médailles n’améliorent pas la précision ou les scores.

## Essai en jeu

Recompiler et utiliser le client, le serveur et l’éditeur de la même version. Se placer sur la case de départ dorée de l’arène active. Dans **Lobby → Joutes Royales → S’entraîner contre l’IA**, choisir une difficulté puis lancer l’entraînement. La boutique et le bilan de carrière apparaissent dans la même fenêtre hors duel.

Le serveur réserve la lice, calcule les scores et nettoie l’adversaire partagé en fin de partie ou en cas d’interruption. Les voyages du Lobby restent bloqués pendant la charge.

## État des autres demandes

Les sièges réservés et animations assises, la caméra spectateur, le décor final complet, les cérémonies royales finalisées en jeu, les classements, les quêtes/achievements dédiés et les tournois ne sont pas implémentés dans cette version. La recette visuelle avec un client connecté reste à effectuer. Les tests et aperçus de l’éditeur ne remplacent pas cette recette.

Le cahier complet reste `Conception-v2.txt`. Aucun de ses critères restants n’est marqué comme validé par cette correction de l’éditeur.

## Duels PvP

Les duels entre deux comptes sont maintenant disponibles. Dans l’onglet **1. Arène**, activer les duels entre joueurs, placer les deux personnages aux départs doré et bleu, puis ouvrir **Défier un chevalier** depuis la fenêtre des Joutes du Lobby. Le détail des invitations, des résultats et des interruptions se trouve dans `PVP.md`. Le PvP doit encore être essayé visuellement sur deux clients connectés ; les tests de monde et d’interface sont réussis.
