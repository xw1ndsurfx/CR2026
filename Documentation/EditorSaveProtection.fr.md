# Sauvegardes de l’éditeur et corrections Foundry/Forge/PvP

Cette correction est indépendante de la phase navale. Mettre à jour **le serveur et le Game Editor ensemble** pour disposer des confirmations d’enregistrement des recettes et du PvP. Aucun changement n’est publié en production par cette PR.

## Réglages et fonctionnement

Dans **Fichier → Protection des sauvegardes…** :

- Le rappel après environ cinq minutes de modifications non sauvegardées est activé par défaut. Une bannière propose « Sauvegarder maintenant ». Dans une fenêtre d’événement ouverte, le rappel apparaît aussi dans son pied de fenêtre.
- La sauvegarde automatique vers le serveur est **facultative, désactivée par défaut**. Son intervalle est réglable de 1 à 30 minutes, avec 2 minutes par défaut. Elle concerne la map courante et ses événements terminés, et rend les changements actifs sur le serveur. Elle attend la fermeture des éditeurs d’événement/dialogues modaux, la fin d’un tracé/déplacement de sélection et la sortie de la saisie des propriétés.
- Les préférences suivent le serveur configuré, comme les autres options de l’éditeur.
- Indépendamment de cette option, une copie de récupération locale des maps modifiées et des modèles d’événement ouverts est prise toutes les **30 secondes** lorsque le dessin est arrêté. Une copie est aussi tentée avant sauvegarde manuelle, changement de map, fermeture et traitement d’une déconnexion.
- Chaque objet conserve trois générations locales, écrites dans un fichier temporaire puis déplacées après vidage sur disque. Une génération corrompue est ignorée au profit de la précédente. Les copies identiques ne remplacent pas les générations utiles.

Le chemin réel est affiché dans la fenêtre de réglages : `%LOCALAPPDATA%/IntersectEditor/Recovery/<empreinte du serveur>/`. Les copies restent sur le poste Windows de l’auteur. Elles ne sont pas envoyées au serveur ni synchronisées entre les postes.

**Les champs d’une commande encore ouverte et non validée ne sont pas inclus dans le modèle sauvegardé.** Valider sa commande régulièrement. Les copies d’événement avec identifiant vide ne sont pas prises en charge ; enregistrer les événements intégrés aux quêtes avec leur éditeur parent.

## Récupérer après un crash ou redémarrage serveur

1. Relancer l’éditeur et se reconnecter au **même serveur** depuis le même compte Windows.
2. Ouvrir la map concernée puis **Fichier → Récupérer une copie locale de la map…**. Confirmer après avoir vérifié le nom et la date proposés. Cela récupère propriétés, tuiles, attributs et événements locaux, y compris un nouvel événement présent dans la copie de map. Les liens avec les maps voisines restent ceux du serveur.
3. Pour un événement commun existant, l’ouvrir puis utiliser **Récupérer une copie locale de l’événement…** en bas de la fenêtre. Le bouton fonctionne aussi sur un événement de map existant.
4. Examiner les changements. La récupération est locale : utiliser ensuite **Sauvegarder l’événement et la map** pour un événement local, ou Sauvegarder pour un événement commun. Une map récupérée peut être annulée via l’historique de la map.

Une modification faite après l’envoi d’une map est conservée lorsque son retour serveur arrive : elle reste marquée comme non sauvegardée. La fermeture normale propose Sauvegarder / conserver uniquement une copie locale / Annuler. Si Sauvegarder est choisi, l’éditeur attend le retour serveur au lieu de couper immédiatement la connexion ; après 15 secondes sans retour il reste ouvert.

Une coupure brutale peut perdre les changements réalisés depuis la dernière copie réussie, et une commande non confirmée dans son sous-éditeur. Ces copies ne remplacent pas les sauvegardes régulières des bases du serveur. L’édition simultanée de la même map par plusieurs développeurs conserve les règles de concurrence existantes du moteur.

## Recettes et PvP

Le chargement JSON ajoutait les dix grades PvP initiaux aux dix grades reçus. Le tableau passait à vingt grades ; les validations de toute la configuration pouvaient alors refuser des recettes pourtant complètes. Le chargement remplace maintenant la liste initiale par la liste enregistrée, sans changer les seuils ni les noms personnalisés.

Les listes Foundry/Forge sont liées à leur contexte avant la sélection du type/objet, y compris à la réouverture d’une liste non vide. Avant l’envoi, la dernière cellule en cours de saisie est validée. Une cellule dont la conversion échoue est signalée et bloque l’envoi ; les règles de validation du serveur sont conservées.

Les messages de validation citent la recette, le matériau, le grade ou l’arène concerné, ainsi que les limites attendues. Les bases Capture the Chest doivent être dans la map et séparées d’au moins quatre cases (distance X + distance Y). Une arène activée nécessite une map de zone Arena ; une map classée doit autoriser le PvP. Ces dernières conditions sont toujours vérifiées par le serveur.

Foundry/Forge et PvP restent ouverts pendant l’enregistrement, avec les contrôles temporairement désactivés. Ils se ferment seulement après la réponse positive du serveur à leur requête. Refus ou délai de 15 secondes : les champs restent disponibles pour correction. Les anciens éditeurs sans identifiant de requête restent acceptés par le serveur.

Les nouveaux textes ont des traductions anglaises, françaises et espagnoles dans le catalogue embarqué `EditorSafetyText.json` ; les noms de maps, objets, recettes et fichiers ne sont pas traduits automatiquement par cette correction.

## Vérification sur serveur de test

1. Ouvrir les réglages, vérifier les valeurs par défaut puis activer la sauvegarde serveur à une minute. Modifier une map sans ouvrir d’événement : attendre l’envoi, rouvrir la map et vérifier la persistance.
2. Ouvrir un événement de map, ajouter et **valider** une commande ; attendre 30 secondes. Vérifier l’heure de copie locale. Redémarrer le serveur de test, se reconnecter puis récupérer la map. Vérifier les tuiles et la commande. Répéter avec un événement commun et son bouton de récupération.
3. Laisser une commande en cours de saisie : vérifier que l’éditeur ne la valide/annule jamais tout seul et qu’aucune map inachevée n’est publiée automatiquement.
4. Désactiver la sauvegarde serveur et laisser une modification pendant environ cinq minutes : vérifier le rappel. Fermer ensuite la map avec Annuler puis Sauvegarder ; vérifier la conservation des changements et l’attente de confirmation.
5. Créer une recette Foundry puis Forge avec un objet produit et des matériaux existants ; modifier la dernière quantité puis enregistrer directement. Rouvrir et vérifier valeurs et identifiants. Tester une quantité nulle pour vérifier le diagnostic ciblé.
6. Ouvrir PvP Rankings depuis une configuration enregistrée : vérifier exactement dix grades. Modifier le dernier seuil saisi puis enregistrer. Ajouter une arène Capture the Chest valide, enregistrer, rouvrir. Tester deux bases trop proches pour vérifier le message précis.
7. Refuser volontairement une map d’arène côté serveur ou couper la connexion pendant l’enregistrement : vérifier que la fenêtre de configuration ne disparaît pas avec les champs saisis.

Les tests automatisés couvrent les cycles JSON, les vraies fenêtres WinForms et leurs cellules non encore validées, création/réouverture de recettes, diagnostics, confirmations ciblées, générations de récupération, corruption de la dernière copie, modifications pendant l’attente du retour d’une map et traductions. Un essai connecté avec les bases et ressources réelles reste nécessaire pour les parcours complets de crash/redémarrage et de publication en jeu.
