# Capitainerie et attribut Q — prototype

Cette tranche dépend des sprites et correctifs de navigation de la PR #154. Aucun déploiement automatique. Mettre à jour ensemble le client, le serveur, l’éditeur et les ressources Ocean : deux paquets réseau et un attribut de map ont été ajoutés.

## Quai dans l’éditeur

Dans **Map Attributes**, sélectionner **Q - Quai**, puis peindre les cases de rive où le joueur peut se tenir et débarquer. Le Q doré apparaît sur la couche des attributs de l’éditeur. Ce marqueur n’est pas un décor visible dans le jeu : dessiner également le quai avec les tilesets.

Enregistrer et envoyer la map au serveur. Le quai doit se trouver sur une map du dossier Ocean, sous-dossiers compris. Il est praticable à pied, mais bloque la coque. Une case ne porte qu’un attribut : Q remplace l’attribut précédemment peint. Conserver les attributs Bloqué sur la terre et les obstacles autour.

À pied, à moins de 1,5 case d’un Q, utiliser le contrôle habituel **Attaquer / Interagir**, ou `/naval`, pour ouvrir la capitainerie. Les interactions existantes avec un événement ou un PNJ devant le joueur restent prioritaires. La commande ouvre aussi la fenêtre sans dépendre de l’orientation du personnage.

En navigation : maintenir Espace jusqu’à l’arrêt, puis `/naval`. Le serveur cherche une case **Q libre**, à moins de cinq cases du centre, sur la map actuelle, hors coques, entités et événements bloquants. Cela fonctionne à un autre quai et après une transition Ocean. Il ne choisit pas une case de mer non marquée autour du Q. Sur une map sans Q, le comportement de débarquement historique au quai configuré reste disponible.

Les bateaux de test publics de `naval.json` sont conservés. Ils ne deviennent pas gratuitement la propriété du joueur. À proximité d’un Q, la capitainerie prend priorité sur l’embarquement dans un bateau public ; son ancien embarquement reste accessible près de la coque, à distance du Q.

## Catalogue et monnaies

Dans le répertoire de lancement du **serveur de test**, copier `resources/naval-shop.example.json` vers `resources/naval-shop.json`. L’exemple est désactivé par défaut. Définir les prix puis passer `Enabled` à `true`. La navigation doit aussi être activée dans `resources/naval.json`. Seul l’exemple est distribué : une mise à jour ne remplace pas le catalogue actif.

Les deux offres d’exemple utilisent volontairement le même modèle graphique royal 002 déjà vérifié. Elles montrent les conditions Aureons et Auragems/Premium ; elles ne prétendent pas représenter deux coques différentes. **1 000/100 Aureons et 50/5 Auragems sont des prix de démonstration**, à remplacer avant utilisation réelle.

Chaque offre définit :

- `Id` : identifiant stable du modèle ; ne pas le changer après des achats.
- `Name` : nom affiché, traduisible par le catalogue de contenu. Les deux noms standards sont fournis en anglais, français et espagnol.
- `Currency` : `Aureons` ou `Auragems` ; une seule monnaie par offre.
- `CurrencyItemId` : identifiant réel de l’objet monnaie. Si vide, le serveur exige un unique objet de type Currency portant exactement le nom indiqué. Un nom absent ou ambigu bloque les achats et réparations. Aucun identifiant réel n’est inventé.
- `Price` et `RepairPrice` : coûts entiers non négatifs. La réparation complète utilise la même monnaie que l’achat ; son prix fixe couvre aussi un naufrage.
- `PremiumRequired` : Premium actif nécessaire à l’achat et à chaque mise à l’eau. Le contrôle réutilise la variable d’expiration Premium existante de la boutique LogiCoins. Si elle n’est pas configurée, l’accès Premium est refusé. Une expiration ne supprime pas le bateau ; la réparation reste permise.
- `Ship` : caractéristiques existantes de la coque, mouvement et sprites. Position, identifiant de navire et coordonnées de quai sont déterminés par le serveur à la mise à l’eau, pas par le modèle.
- `SinkingGraphics` : PNG de naufrage, avec sa métadonnée Ocean. Respecter la casse, notamment `Ship_CorpsRoyaux_002_Sink.png` sous Linux.

Le catalogue est relu à chaque requête, avec une limite de taille et validation. Trente-deux modèles maximum. Un exemplaire de chaque modèle par personnage, trente-deux bateaux maximum par personnage. Les prix, le statut Premium et la propriété sont toujours revérifiés côté serveur ; les requêtes du client ne contiennent aucun prix ni PV.

## Flotte, mise à l’eau et réparation

La fenêtre paginée présente les bateaux du catalogue, leur propriété, état de coque, coût d’achat, coût de réparation, monnaie et solde d’inventaire. Actions : **Acheter le bateau**, **Prendre la mer**, **Réparer entièrement**. Échap et la fermeture des fenêtres du menu ferment également la capitainerie.

L’achat déduit la monnaie de l’inventaire et enregistre le bateau avec le personnage dans la même transaction native de sauvegarde du compte. Une sauvegarde refusée restaure l’inventaire et la flotte en mémoire. Un second achat du même modèle est refusé. Aucun prélèvement sur banque ou LogiCoins n’est ajouté.

À la mise à l’eau, recherche d’un emplacement pour la coque entière dans un rayon de cinq cases du Q, sur la map courante. Les Q, obstacles, événements, entités et autres bateaux restent bloquants. En absence de place, aucun bateau n’est activé. Un seul bateau piloté par joueur. Au débarquement, le bateau personnel rentre dans la flotte et disparaît du monde ; les bateaux publics restent en place.

La propriété et les PV de coque sont stockés dans la nouvelle colonne nullable **NavalFleet** de la table Players. Migrations SQLite et MySQL fournies ; les personnages existants commencent avec une flotte vide. La mise à l’eau sauvegarde aussi un marqueur de retour au quai avant le déplacement du joueur. Une reconnexion après interruption ramène le joueur au quai de départ, ou au point de réapparition natif si le quai est devenu inutilisable. Les navires personnels rentrent au port lors d’une déconnexion normale ; leur position en mer n’est pas persistée.

**Réparer entièrement** restaure les PV à 100, y compris depuis 0 (coulé), après paiement et sauvegarde réussis. Impossible de prendre la mer avec une coque à 0. Aucune revente, aucun transfert entre personnages, aucun remboursement automatique après expiration Premium.

## Naufrage et raccord des futurs combats

Le raccord serveur `NavalSystem.DamageOwnedShip(owner, shipId, damage)` valide le propriétaire, enregistre les PV et déclenche le sprite endommagé ou le naufrage à 0. Le client ne dispose d’aucune commande pour infliger ou annuler ces dégâts. Après l’animation, le bateau personnel rentre dans la flotte avec 0 PV et le capitaine revient au quai de départ, ou au point de réapparition natif si le quai n’est plus libre/présent. Il peut ensuite payer sa réparation complète.

Les canons, tirs ennemis, dégâts de collision et combats ne sont **pas encore raccordés** à ce point d’entrée. La navigation seule n’inflige aucun dégât. Le naufrage ne peut donc pas être provoqué par le gameplay actuel pour tester la boutique. Les tests automatisés couvrent l’état coulé et la réparation ; un essai connecté de dégâts attend le raccord des combats. L’horloge du sprite reste locale : un observateur arrivant en cours de naufrage peut voir l’animation depuis son début.

## Test sur copie

1. Sauvegarder les bases de test avant leur première ouverture par le nouveau serveur, qui applique la migration NavalFleet.
2. Installer les trois exécutables et les ressources Ocean correspondants sur le test uniquement.
3. Peindre et sauvegarder au moins deux quais Q, avec une rive et de l’espace pour une coque 2 × 4. Pour essayer une transition, placer un quai sur chaque map Ocean reliée.
4. Configurer le catalogue, les vrais objets Aureons/Auragems et les prix. Vérifier la variable Premium si une offre en dépend.
5. À pied près du Q : Interagir ou `/naval`. Vérifier le solde, acheter, fermer et rouvrir ; le bateau doit rester possédé. Tenter un achat sans fonds et un achat Premium sans abonnement.
6. Prendre la mer : vérifier les huit directions, inertie et collisions. Revenir à un Q différent, arrêter avec Espace, puis `/naval` ; l’avatar doit apparaître sur un Q libre, le bateau personnel rentrer dans la flotte.
7. Se déconnecter en pilotant, puis reconnecter : retour au port, propriété conservée. Tester aussi un redémarrage du serveur de test pendant le pilotage.
8. Vérifier le gameplay ordinaire, l’ouverture des événements/PNJ et les autres fenêtres. Ne pas remplacer le `naval.json` cloud par le catalogue de boutique : ce sont deux fichiers distincts.

Les déplacements de carte et les validations utilisent l’architecture Intersect existante. Les assets irréguliers conservent les limites d’alignement documentées dans `NavalOceanSprites.fr.md`. Aucun asset absent n’a été intégré.
