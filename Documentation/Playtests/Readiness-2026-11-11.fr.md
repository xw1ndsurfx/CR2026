# Préparation du playtest — 11 novembre 2026

## Périmètre et référence

L’utilisateur a choisi de poursuivre la roadmap par la stabilité et la préparation du playtest. Référence source : main, commit 69670d7778575829fa0e508d7d0483355709a2b4 (fusion de la PR #265). La branche de ce contrôle part directement de main ; le profil Intel privé est exclu.

Le calendrier des 14–15 novembre reste un objectif conditionnel. Aucune annonce, ouverture Steam, réinitialisation de compte, migration ou modification du serveur déployé n’est effectuée par cette revue.

## Validations déjà disponibles

- L’utilisateur confirme le fonctionnement de l’agriculture et indique avoir testé le client le 11 novembre. Le retour ne précise pas la durée de session ni les scénarios hors connexion/redémarrage.
- Les quatre workflows de la PR #265 sont réussis : Pull Requests, Editor save protection, Blackjack production validation et Poker production validation.
- Les PR #240 et #247–249 sont toujours ouvertes. Exportation et Royal Guide ne doivent pas être présentés comme intégrés sur la seule base de ces branches.
- Le crash audio du laptop a été identifié dans OpenAL Soft 1.18.1. Le remplacement privé embarque OpenAL Soft 1.25.2 ; l’empreinte de la bibliothèque embarquée et le profil Intel ont été vérifiés. Le test audio isolé a passé 8 285 cycles avec streaming simultané pendant trois minutes. Ce résultat ne certifie pas une session de jeu prolongée.

## Contrôles automatisés de cette revue

Contrôles effectués sur la référence indiquée ; les séries encore en cours restent signalées :

| Contrôle | Résultat |
| --- | --- |
| Régressions serveur : crashes, sécurité de configuration et donjons | **21/21 exécutés réussis** |
| Diagnostics : arrêt normal, exception non gérée, arrêt forcé et collecte locale | **10/10 réussis** |
| Sauvegarde périodique : premier passage, délai, expiration et délai nul | **5 assertions réussies** |
| Transactions réelles d’inventaire sur base temporaire : débit, remboursement, reprise et rollback | **14/14 groupes réussis** |
| Client : heartbeat, suivi des phases lentes, ping et ajout de ressources | **9/9 réussis** |
| Lecture de la configuration locale réelle, sur une copie | **1/1 réussi**, 17 127 octets, deux règles d’animation ; aucune réécriture |
| Écoute réseau isolée : sockets IPv4/IPv6 locales et comportement normal conservé | **2/2 réussis** |
| Démarrage : lecture des arguments avec sortie redirigée | **1/1 réussi** |

Les copies des configurations installées des deux distributions étaient identiques ; une seule lecture explicite a donc été nécessaire. Le cas de configuration réelle, volontairement explicite dans NUnit, a été lancé séparément des 21 régressions ordinaires.

Les fixtures sont isolées des comptes, inventaires et bases du serveur de jeu. Les journaux détaillés restent sur le poste de test et ne sont pas publiés dans le dépôt.

## Ressources et incidents à suivre

Le contrôle PNG du 11 novembre reconfirme les trois images invalides déjà signalées par l’[audit du 10 novembre](../../docs/playtest-stability-2026-11-10.md) : resources/gui/table/button_cards.png (CRC IDAT), button_exit.png (longueur de chunk invalide), button_portraits.png (CRC IDAT). Les copies vérifiées dans les deux distributions locales ont les mêmes défauts. Aucun original sain n’a été trouvé dans ces emplacements ; aucune image n’est recréée arbitrairement. Le chargeur possède un traitement de secours, mais ces boutons restent à contrôler visuellement.

L’erreur SQLite « database is locked » signalée dans l’audit précédent nécessite toujours une vérification des accès à la base et de la restauration d’une sauvegarde. Les archives historiques ne permettent pas de conclure à une récidive sur le serveur actuel.

## Copie de recette locale

Une archive existante du 11 novembre à 06:38:57 UTC a été téléchargée sans arrêter le serveur en ligne. Les sept bases SQLite restaurées passent integrity_check. Deux cycles de démarrage, arrêt par exit et redémarrage ont terminé avec un code de sortie nul ; les sockets réelles sont exclusivement 127.0.0.1 et ::1 sur un port distinct. API, UPnP, courriels et intégration Logiklik de production sont neutralisés dans cette copie privée. Les premières tentatives ont révélé deux accès à un tampon de console absent sous Windows, corrigés pour les sorties redirigées.

La méthode de création de l’archive et sa cohérence temporelle entre fichiers restent inconnues. Aucun compte joueur n’a encore été utilisé pour vérifier une action persistée après reconnexion. Les archives, clés, journaux et bases demeurent hors du dépôt. Voir le [guide de reproduction locale](../../docs/local-playtest-server.fr.md) et le [questionnaire EN/FR/ES](Questionnaire.en-fr-es.md).

## Portes de validation encore ouvertes

- [ ] Relever les révisions effectives du serveur, du client normal et de l’éditeur installés, ainsi que les versions de leurs configurations et migrations. Une PR fusionnée ne prouve pas que ses binaires sont déployés.
- [ ] Vérifier une sauvegarde des bases/configurations/ressources et une restauration dans un environnement isolé. Les journaux de crash et Git ne remplacent pas une sauvegarde de données.
- [ ] Avec des comptes propres : connexion Steam puis Logiklik, création/sélection du personnage, carte, déplacement, inventaire, combat de base et quête.
- [ ] Reconnexion après coupure, conservation de la progression, des objets et de la position ; sortie et abandon d’un donjon partagé sans bloquer les autres participants.
- [ ] Redémarrage contrôlé du serveur de test : présence des derniers changements sauvegardés et reprise sans duplication de récompense.
- [ ] Recette de la ferme : embauche, graines/salaires, culture/récolte, EXP Agriculteur, absence, redémarrage, capacité et refus d’accès aux finances d’un autre propriétaire.
- [ ] Client Intel : session prolongée sur le laptop après le correctif audio. Client normal : session séparée pour exclure une régression de sa distribution.
- [ ] Préparer la collecte des incidents et le questionnaire EN/FR/ES, puis consigner une décision go/no-go avant d’annoncer l’ouverture.

**Décision actuelle : préparation en cours.** Les portes non cochées restent nécessaires ; ce document ne valide pas encore l’ouverture du playtest.
