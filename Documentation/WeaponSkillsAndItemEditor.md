# Compétences d'armes et éditeur d'objets

## Maîtrises

Les épées, dagues, haches/marteaux, bâtons/magie et fouets disposent chacun de six talents : trois passifs et trois compétences actives. Les rangs s'achètent avec les Mastery Points de leur catégorie, avec des niveaux et des prérequis. L'approche reprend le principe de progression par arme et de techniques de combat présenté dans le [guide officiel de TLOPO](https://tlopo.com/help/combat/).

Les nouvelles compétences sont indépendantes des spells : elles ne créent aucun spell, n'occupent aucune case du livre et n'exigent pas de place libre dans ce dernier. Les anciens talents personnalisés associés à des spells restent compatibles.

- Les compétences apprises apparaissent dans une barre dédiée lorsque l'arme correspondante est équipée. Sélectionner un ennemi puis cliquer sur la compétence pour l'utiliser.
- Le serveur vérifie le rang appris, les prérequis, l'arme, la cible, la portée, les protections de combat et le délai de recharge. Changer d'arme conserve les recharges.
- Chaque compétence possède des dégâts et une progression par rang, une portée et une recharge configurables. Le combat passe par les règles habituelles de dégâts, de critique et de progression d'arme.
- Dans Weapon Mastery Editor > arbre de talents, sélectionner une compétence active pour choisir son animation et son animation d'impact. Sans animation spécifique, l'animation d'attaque de l'arme est utilisée lorsqu'elle existe.

La version 2 des maîtrises remplit une seule fois les arbres intégrés vides. Elle conserve les arbres personnalisés, les armes associées, l'expérience, les points dépensés et les animations de montée de niveau. Les arbres personnalisés déjà remplis ne sont pas remplacés ; les modèles restent disponibles dans l'éditeur après avoir vidé volontairement un arbre.

## Item Editor

General / Trading contient désormais les sélecteurs de catégorie et de niveau de maîtrise. Ils modifient les véritables conditions d'utilisation. Le prérequis est appliqué à chaque branche alternative pour éviter de pouvoir le contourner ; les conditions sans rapport avec la maîtrise sont conservées.

Les légendes et boutons des groupes sont dimensionnés selon leur texte et leur police. Les colonnes s'écartent pour conserver les champs accessibles, et les onglets permettent de défiler si la fenêtre est petite. Les listes déroulantes peuvent afficher leurs options longues.

Les objets issus des parcours de génération ont **Can sell in marketplace** activé par défaut. Le choix explicite de le décocher dans l'Auto Creator reste respecté. Les objets existants ne sont pas modifiés par l'ouverture de l'éditeur.

## Arbres coupés

Le client conserve l'image de l'arbre dès la transition de santé vers l'état épuisé. La chute reste visible même si le paquet de destruction arrive ensuite ou si l'état coupé n'a pas de visuel. La zone de rendu inclut la chute et la ressource est libérée à la fin de l'animation. L'option Falling Tree doit rester activée pour la ressource concernée.

Reconstruire et distribuer le client, le serveur et l'éditeur ensemble pour utiliser les nouvelles compétences.
