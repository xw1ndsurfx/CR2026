# Expéditions navales coopératives

## Parcours joueur

Les expéditions utilisent les navires, équipages, patrouilles et monstres existants dans le monde partagé. Une expédition peut accepter un équipage sur un seul navire, une flotte d’au moins deux navires, ou les deux formats.

1. Former un groupe et embarquer sur les navires prévus. Chaque capitaine doit faire partie du groupe. Arrêter les navires près des quais Q de la même carte de port.
2. Dans **Équipage du navire → Expéditions navales**, le chef du groupe propose une expédition.
3. Chaque participant ouvre le suivi et confirme **Prêt pour le départ**. Le chef choisit **Partir ensemble**. La préparation expire après trois minutes.
4. Suivre l’objectif commun. Le suivi indique la carte, les coordonnées pour une destination, la cible pour un combat et l’avancement du groupe.
5. Une fois toutes les étapes terminées, chaque participant choisit **Récupérer ma récompense**. Une récompense en attente reste disponible après reconnexion; elle doit être récupérée avant une nouvelle expédition.

Fermer la fenêtre conserve l’expédition. Abandonner demande une seconde confirmation et termine l’expédition pour tout le groupe. Un résultat d’échec peut ensuite être fermé individuellement.

## Configuration dans l’éditeur

Ouvrir **Éditeur du système naval → Expéditions navales**. Les anciennes configurations restent compatibles et ne créent aucune expédition automatiquement.

- Ajouter une définition, renseigner son nom et sa description, puis choisir **Un navire ou une flotte**, **Équipage sur un navire** ou **Flotte**.
- Régler les limites : 2 à 32 participants, 1 à 8 navires, niveau Navigation requis, durée de 5 à 180 minutes et délai de retour de 30 à 600 secondes. La taille réelle du groupe reste limitée par les options du serveur et la capacité des navires.
- Définir une récompense individuelle d’EXP Navigation et, facultativement, un objet existant avec sa quantité. Aucun objet n’est créé par l’expédition. L’EXP respecte le plafond Navigation du serveur.
- Ajouter et ordonner les étapes. Les cartes doivent appartenir au dossier **Ocean**, y compris ses sous-dossiers. Les sélecteurs de patrouilles et de monstres suivent la carte choisie.
- Activer puis **Enregistrer cette section**. Les contrôles habituels de version de la configuration navale et l’interdiction de modifier la navigation pendant qu’un navire est occupé s’appliquent.

| Objectif | Conditions de validation |
| --- | --- |
| Rejoindre la zone | Tous les navires inscrits sont sur la carte, dans le rayon autour des coordonnées choisies. |
| Vaincre la patrouille | La patrouille configurée est réellement coulée ou son abordage est remporté par un navire inscrit. Le compteur peut demander plusieurs victoires après réapparition. |
| Vaincre le monstre marin | L’assaut du monstre configuré est réellement remporté. L’échec ou son écrasement fatal ne comptent pas. |
| Rentrer au port | Tous les navires sont dans la zone configurée, arrêtés et à portée d’un quai Q. Prévoir un rayon assez large pour la flotte et les grandes coques. |

Pour une victoire navale, tous les participants doivent être présents dans le groupe et tous les navires inscrits dans la carte cible à moins de 40 cases du combat. Les équipiers temporairement transférés sur le pont d’un abordage ou d’un assaut restent reconnus. Le combat du monstre conserve son fonctionnement existant : l’équipage du navire attaqué combat sur son pont; les autres navires restent dans l’océan.

Les participants et leurs navires sont fixés au départ. Une nouvelle recrue ne rejoint pas rétroactivement l’expédition. Changer de navire, quitter le groupe ou se déconnecter suspend la progression; revenir avec le même personnage, le même navire et le groupe avant le délai permet de poursuivre. Un navire coulé, un abandon, le dépassement du délai de retour ou de la durée totale provoque un échec. Les chronomètres continuent hors ligne.

## Persistance et déploiement

- Définitions : `resources/naval.json`, champ `Expeditions`.
- Journal serveur : `resources/naval-expedition-runs.json`. Les étapes, participants, victoires déjà comptées, délais et récompenses récupérées y sont enregistrés par remplacement atomique.
- Reçu de récompense : `NavalCareer.LastExpeditionRewardRunId`, dans la sauvegarde navale du personnage. L’objet, l’EXP et ce reçu sont enregistrés ensemble dans la transaction du compte. Un nouvel essai après interruption ne redonne pas la récompense.
- Inventaire plein ou sauvegarde refusée : les modifications du personnage sont annulées et la récompense reste à réclamer. Une erreur d’écriture du journal conserve l’état durable précédent; une victoire validée en mémoire est réessayée tant que le processus reste actif. Si le serveur s’arrête avant cette écriture, cette victoire non enregistrée doit être refaite.
- Après redémarrage, le journal est relu. La récupération navale existante peut ramener les navires au port; reformer le groupe et réembarquer sur les mêmes navires avant les délais. Il ne s’agit pas d’une restauration exacte de leur position en mer.
- Inclure le journal et la base des personnages dans **la même sauvegarde cohérente**. Ne pas restaurer l’un indépendamment de l’autre. Un journal invalide bloque les modifications d’expéditions au lieu d’être remplacé par un journal vide.

Client, éditeur et serveur doivent provenir de la même version : deux nouveaux types de paquets changent l’empreinte du protocole. Aucun binaire ni réglage privé Intel HD 3000 n’est inclus dans ce chantier; ce client conserve sa branche et son installation séparées.

## Validation

Validation locale du 21 novembre 2026 : **338 tests navals serveur réussis**, **9 scénarios d’interface réussis** et contrôles de l’éditeur réussis dans les trois langues. Le client normal et l’éditeur compilent en Release. Les avertissements existants du dépôt restent présents.

Les tests serveur couvrent les formats d’équipage/flotte, le consentement, les droits du chef, le regroupement de tous les navires, les absences, les échecs, les véritables victoires de patrouille et de monstre, les reprises du journal, les doublons réseau et les récompenses. La déconnexion simultanée de tout le groupe est enregistrée; revenir après le délai ne peut pas effacer l’échec. Les tests d’interface utilisent les véritables contrôles avec des mesures de police simulées en EN/FR/ES à 640×480, 1280×720 et 1920×1080, y compris la disparition du suivi pendant un dialogue modal. Les contrôles de l’éditeur vérifient les sélecteurs, le changement de carte/cible, l’ordre des étapes et l’enregistrement.

Commandes de reproduction :

```text
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter FullyQualifiedName~Naval
dotnet run --project Utilities/NavalExpeditionUiTests/NavalExpeditionUiTests.csproj -c Release
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Release -p:RuntimeIdentifiers=win-x64 -- --naval-expeditions-only
```

Recette multijoueur à effectuer sur la copie isolée avant d’activer un parcours public :

- Deux joueurs sur un navire : proposition, refus du départ incomplet, accord des deux, zone, patrouille, retour et deux récupérations individuelles.
- Deux navires du même groupe : attente du navire retardataire et partage de la victoire, sans crédit pour une victoire d’un groupe extérieur.
- Assaut d’un monstre et abordage : maintien du suivi pendant le transfert de pont et validation au véritable résultat.
- Déconnexion d’un équipier, reconnexion, puis redémarrage propre du serveur : conservation des étapes et reprise avec les mêmes navires.
- Inventaire plein, nouvelle tentative et reconnexion après récupération : récompense conservée puis remise une seule fois.
- Abandon, naufrage et délais expirés : aucun gain de récompense d’expédition.

Cette recette en jeu ne peut pas être remplacée par les tests automatisés.
