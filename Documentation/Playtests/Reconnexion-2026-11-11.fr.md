# Reconnexion et sauvegarde après redémarrage — 11 novembre 2026

## Environnement et méthode

Recette sur la copie privée restaurée pour la PR #266, basée sur le code de main 53349e3fe (binaire local construit depuis le même contenu applicatif f88c57efd). Le client utilisé est la copie Intel séparée. Jeu sur 127.0.0.1/::1, port distinct ; API, SMTP, UPnP et intégration Logiklik de production désactivés. Aucun changement du serveur en ligne ou des clients installés.

L’utilisateur a effectué les connexions et confirmé visuellement son personnage. La capture graphique du client a échoué ; les observations techniques proviennent des journaux et de transactions SQLite en lecture seule. Les captures de données et les identifiants de comptes restent privés.

## Scénarios exécutés

| Scénario | Résultat et preuve |
| --- | --- |
| Sauvegarde périodique en jeu | L’utilisateur change de carte et déplace un objet vers une case vide. Les nouvelles coordonnées et les deux lignes d’inventaire modifiées sont présentes avant la coupure ; les quantités totales sont identiques. |
| Coupure du client | Arrêt du seul processus client local, serveur laissé actif. L’état sauvegardé reste identique. |
| Reconnexion | L’utilisateur confirme la position et l’objet déplacé. Les 18 comparaisons du relevé enregistré passent. |
| Arrêt avec joueur connecté | Commande exit au serveur local, sortie 0 ; la base passe integrity_check et les 18 comparaisons restent identiques. |
| Redémarrage et nouvelle connexion | Serveur relancé depuis les mêmes données. L’utilisateur confirme en jeu ; le relevé initial des données est identique. Voir toutefois la réserve ci-dessous sur les changements ultérieurs pendant cette session. |
| Dernière progression puis démarrage sans client | Après l’arrêt final, un nouveau démarrage/arrêt sans client conserve exactement le dernier état : 18/18 comparaisons réussies, sans nouvelle récompense constatée. |
| Sauvegardes répétées du graphe de compte | Sur une seconde copie SQLite indépendante, appel du vrai User.Save(context, force:true) deux fois après modification de l’EXP et d’une quantité d’objet. Un processus neuf, avec un contexte EF neuf, retrouve exactement +30 EXP et +10 objets, sans écrasement de la dernière sauvegarde. Les clés étrangères restent activées. Ce contrôle ne simule pas une connexion réseau. |

Les 18 comparaisons couvrent carte, X/Y/Z, direction, niveau, EXP, points et allocations de caractéristiques, statistiques de base, équipement, apparence et six ensembles de lignes : inventaire (35), banque (200), quêtes (5), variables (95), sorts (40), raccourcis (10). Les totaux ne remplacent pas ces comparaisons : les identifiants, emplacements, propriétés et quantités des lignes sont comparés.

## Réserve : changements non expliqués pendant la session

Après le relevé initial suivant le redémarrage, la sauvegarde finale présente un changement de coordonnées, +20 EXP, +1 Rat Tail, +1 Raw Meat et +2 dans le compteur MonsterKilled. L’utilisateur déclare ne pas avoir effectué d’action après sa dernière connexion. Les données des événements font référence à ce compteur dans les événements de monstres ; les gains sont compatibles avec du combat, mais cette corrélation ne prouve ni la cause des récompenses ni celle du déplacement.

Ce changement ne constitue pas une perte de sauvegarde démontrée. Son origine reste à reproduire en zone sans combat, avec suivi des mouvements reçus et des événements/récompenses. Il ne doit pas être présenté comme un comportement normal validé. Le dernier démarrage sans client ne modifie ni ces gains ni les coordonnées finales.

## Limites et suite

La persistance des états effectivement enregistrés est validée dans les scénarios décrits. Le relevé effectué après connexion lit la base, et non directement la mémoire du personnage : un état encore non sauvegardé peut donc lui échapper. Les confirmations utilisateur et les relevés après arrêt complètent cette observation.

Restent à tester : abandon/reconnexion dans un donjon partagé, deux comptes simultanés, banque et transactions pendant coupure, arrêt forcé du serveur distinct de l’arrêt propre, ferme après absence/redémarrage et authentification Steam/Logiklik dans un environnement dédié. La méthode de création de l’archive et sa cohérence temporelle entre fichiers restent inconnues.

La session est arrêtée proprement. Le résultat ne donne pas encore un accord général d’ouverture du playtest, notamment tant que les changements inattendus ci-dessus ne sont pas expliqués.

## Enquête complémentaire

Le [suivi instrumenté au repos](Repos-2026-11-11.fr.md) documente deux nouvelles sessions : aucun gain initial reproduit, mais une réapparition après mort identifiée dans les traces. L’anomalie initiale n’est pas déclarée corrigée.
