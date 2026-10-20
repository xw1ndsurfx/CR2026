# PV, EXP personnage et cargaison navale

## Fonctionnement
La barre verte au-dessus de Sailing affiche les PV actuels/maximum de votre coque, la cargaison occupée/capacité et la vitesse. La touche navale **8** ouvre/ferme la cale; son icône est configurable dans Sons, icônes et effets (resources/spells). Les raccourcis du personnage reviennent au débarquement.

Un ennemi Herbonia coulé récompense une fois le capitaine du tir fatal encore à sa barre. La récompense EXP configurée sert de base au Sailing et à l’EXP personnage; cette dernière passe par GiveExperience, donc respecte les bonus, montées de niveau et plafond natifs. Atteindre le plafond Sailing ne supprime pas le gain personnage. Aucun gain pour la distance ou un simple impact. Les joueurs et bateaux publics de test coulés ne donnent pas de récompense.

La cargaison est attachée au bateau personnel, conservée au débarquement et à la reconnexion, et **entièrement perdue au naufrage**, selon le choix demandé. L’EXP déjà obtenue reste acquise. Une réparation ne recrée pas la cargaison perdue. Les bateaux publics de test ne possèdent pas de cale persistante : utiliser un bateau de Ma flotte.

## Configuration dans System Editor
1. Dans Boutique navale et réparations, choisir un modèle existant et sa **Capacité de cargaison** (0 désactive la collecte; 20 par défaut, maximum 200 lots). Enregistrer cette section.
2. Dans Patrouilles IA Herbonia, choisir un ennemi. Ajouter des lignes dans **Table de butin naval**. Sélectionner de vrais objets du jeu, y compris les monnaies déjà créées si souhaité. Aucun objet ni identifiant de monnaie n’est inventé; une table vide ne donne aucun butin.
3. Pour chaque ligne : probabilité 0–100 %, quantité minimum/maximum 1–1 000 000, qualité 1–5, type (caisse, coffre, coffre royal, matériaux, équipement). Chaque ligne est tirée indépendamment une fois au naufrage; les objets/quantités sont fixés à ce moment, pas relancés à l’ouverture de la cale.
4. Le type sert au classement et à l’affichage. Les coffres ne sont pas des objets à ouvrir : chaque lot contient directement l’objet et la quantité configurés. Un lot occupe une place quel que soit son nombre d’objets. Quand la cale est pleine, une qualité strictement supérieure remplace le lot le moins précieux; sinon le nouveau lot est écarté. Réduire la capacité ne détruit pas la cargaison existante.
5. Quitter toutes les barres avant d’enregistrer la navigation. Les sections de navigation et de boutique gardent leurs versions indépendantes.

## Déchargement au quai
Arrêter et débarquer à un quai Q, puis ouvrir **Ma flotte** et cliquer **Décharger la cargaison** sur le bateau concerné. Les objets sont transférés dans l’inventaire principal, selon leurs limites de pile natives. Si tout ne rentre pas, rien n’est retiré de la cale. Les sacs annexes ne sont pas utilisés. Le serveur vérifie le propriétaire, la proximité de Q et l’absence de navigation; la boutique PNJ reste distincte.

Le transfert inventaire/cargaison utilise une même sauvegarde native du compte et restaure les deux en cas d’échec. Les tâches de collecte sont actualisées après réussite. La récompense EXP personnage utilise une attente persistante avec la flotte avant son application; une sauvegarde échouée est réessayée sans répéter le gain déjà appliqué en mémoire. L’attente initiale entre naufrage et première sauvegarde reste en mémoire : un arrêt brutal à cet instant peut perdre la récompense non enregistrée.

## Procédure de test
Installer ensemble serveur, client et éditeur de cette version sur un environnement de test, avec une copie préalable des bases. Aucun déploiement cloud n’est effectué par la PR. Le stockage naval existant est enrichi sans migration SQL; conserver la copie des bases pour un retour à une ancienne version qui ignore la cargaison.

1. Configurer un modèle personnel avec capacité 2; une patrouille avec 275 EXP et un objet réel, probabilité 100 %, quantité 2, qualité 1. Acheter/utiliser ce modèle depuis Q.
2. Vérifier les PV sur la barre verte. Recevoir une bordée : les PV diminuent. Naviguer/toucher sans couler ne donne ni EXP ni butin.
3. Couler l’ennemi : Sailing +275 une seule fois; EXP personnage de base +275, avec ses bonus/plafonds habituels. Ouvrir 8 : voir un lot de quantité 2. Reconnecter, puis vérifier sa conservation.
4. Remplir la cale, puis configurer/tester un butin de qualité supérieure : remplacement du moins bon lot. Une qualité égale ne remplace pas un lot existant.
5. Débarquer au Q : retour au point d’embarquement. Décharger depuis Ma flotte : objets reçus une fois, cale vide. Réessayer : aucun doublon. Refaire avec inventaire principal plein : cargaison intacte.
6. Collecter à nouveau et laisser couler le bateau hors protection Q : cale vide après naufrage, même après reconnexion/réparation; EXP acquise conservée. Refaire avec deux capitaines : seul le coup fatal reçoit cette récompense, chaque cale reste privée.

## Limites et validation
Adaptation du principe de cargaison à revendre/décharger au port, inspiré du [document de conception Pirates Online préservé par TLOPO](https://tlopo-cdn.tlopo.com/preservation/9f1122fc-37cf-43c6-b748-349e50a41632.pdf). Pas de ramassage flottant ni de partage d’équipage à cette étape. Les tables de butin doivent être configurées avec les objets réels du projet. Supprimer ensuite un objet référencé rend son déchargement indisponible et conserve la cargaison; rétablir l’objet pour la récupérer.

Tests automatisés : gain unique au naufrage, sérialisation des lots, tirages, capacité/qualité, sauvegarde/reprise EXP, perte au naufrage, transfert atomique, piles et inventaire à cases clairsemées. Fenêtre native de l’éditeur vérifiée avec une table et un aperçu enregistrable; rendu HUD, combat et déchargement connectés restent à confirmer dans le jeu installé.

## Notifications de récompenses
Voir [Bandeau EXP, butin et cale pleine](NavalRewardNotifications.fr.md).
