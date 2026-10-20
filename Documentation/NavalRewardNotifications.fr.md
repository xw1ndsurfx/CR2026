# Notifications de récompenses navales

Après une récompense de naufrage sauvegardée, un bandeau bleu sombre et or apparaît au-dessus des barres de coque et Sailing pendant six secondes :
- gain Sailing réellement ajouté (le plafond est respecté);
- objets réellement conservés dans la cale, regroupés par objet et quantité;
- nombre de lots de qualité inférieure remplacés;
- alerte de cale pleine et nombre de lots écartés, avec rappel du déchargement au Q.

Les trois premiers types d’objets apparaissent; si nécessaire, une ligne indique les autres types disponibles dans la cale. La touche de cale reste celle de votre raccourci naval (8 par défaut). Le détail complet reste accessible dans la cale. Les noms d’objets utilisent le catalogue de traduction du contenu, et les textes du bandeau existent en français, anglais et espagnol.

L’EXP du personnage conserve son affichage natif au-dessus du bateau, avec le détail des bonus; elle n’est pas ajoutée une seconde fois par le bandeau Sailing. Si Sailing est au maximum, le bandeau indique simplement l’ennemi coulé et le butin éventuel, tandis que l’EXP personnage suit son propre plafond.

Les notifications n’accordent aucune récompense : elles décrivent la sauvegarde effectuée côté serveur. Les données sont privées au personnage; les snapshots répétés ne répètent pas les messages. À la connexion, l’historique présent sert de référence et n’est pas rejoué. Une déconnexion ou une file de messages trop ancienne peut supprimer une notification visuelle, sans retirer l’EXP ou les objets déjà enregistrés. La file est bornée pour éviter un défilement prolongé après un combat chargé. Aucun fichier visuel ni sonore supplémentaire n’est nécessaire.

## Test sur Ocean
1. Installer ensemble le serveur et le client de cette version sur votre environnement de test; compiler/livrer l’éditeur correspondant avec le reste du lot. Aucun déploiement cloud n’est effectué ici.
2. Dans System Editor, donner à une patrouille 275 EXP et un objet réel à 100 %, quantité 2. Choisir un bateau personnel avec une capacité de cale de 2.
3. Couler l’ennemi : vérifier le bandeau Sailing +275 (hors plafond), le butin ×2 et le gain personnage natif. Attendre plusieurs mises à jour : aucun doublon; le bandeau disparaît après six secondes.
4. Remplir la cale. Couler un autre ennemi avec butin de même qualité : alerte cale pleine et lot écarté. Essayer une qualité supérieure : message de remplacement, contenu de cale effectivement modifié et rappel de cale pleine.
5. Avec plusieurs lignes de butin du même objet, vérifier la quantité regroupée. Avec plus de trois objets distincts, ouvrir la cale pour le détail complet.
6. Reconnecter immédiatement après une victoire : aucun ancien message rejoué, récompenses conservées. Décharger au Q : fonctionnement identique à la livraison précédente.
7. Vérifier le bandeau dans la résolution habituelle, sans chevauchement des barres, ainsi que son masquage pendant une interface qui masque les HUD de statut. Les positions sont calculées depuis la barre de raccourcis.

## Validation et limites
Tests automatisés : absence de notification sur sauvegarde échouée, contenu réellement conservé après remplacements, historique privé/borné, sérialisation réseau, répétitions de snapshots, référence à la connexion, expiration, fermeture de session et format français/anglais/espagnol.
Le rendu connecté du nouveau bandeau reste à confirmer dans votre installation. Les notifications regroupent les récompenses traitées lors d’une même mise à jour; elles ne sont pas un journal permanent de chaque ennemi coulé. Les tables de butin et les règles de récompense de la livraison précédente restent applicables.
