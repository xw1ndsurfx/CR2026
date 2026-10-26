# Vérification du prototype PvE et du Lobby

Base : `4f204806a1f314127b1123eb6f3031ab7c921791`, 25 octobre 2026. Windows, .NET SDK 10.0.302, projets ciblant .NET 8.

- Réseau, client, serveur et Game Editor compilés sans erreur avec `-m:1 -p:RunAnalyzers=false` et les clés réseau de la copie de travail. Les avertissements existants du dépôt restent présents.
- 40 contrôles de règles partagées : horloge, entrées non finies, limitation de fréquence, rejeu, passes, résolution simultanée, points V2, casque/cheval exclus, bonus, IA, stabilité cumulative, introduction tenue et géométrie des deux couloirs.
- 22 tests serveur ciblés réussis : 8 scénarios de monde/protocole de joute, régressions de montures et routes cinématiques. Le scénario de trois passes utilise un vrai Player, l’acteur partagé, une MapInstance et les méthodes natives de déplacement, puis contrôle sa disparition et la libération de l’arène. Les transports de test ne sont pas des clients graphiques connectés.
- 3 tests client : validation des montures intégrées de l’adversaire, durées sans double multiplication et identifiants du Lobby.
- 27 tests de compilation cinématique : six nouveaux scénarios de joute et régressions du compilateur existant. Les cinq sources sont compilées par CinematicCompiler ; aucun moteur de caméra parallèle.
- Suite UI du Lobby passée en anglais/français/espagnol à 640x480 et 1920x1080 : catégorie, filtre et accès à l’entraînement sans recouvrement des contrôles. Tests de métriques et contrôles natifs, pas une capture du jeu connecté.
- Suite serveur/inventaire du Lobby passée, dont verrouillage lors d’une joute, refus de rejoindre/voyager et réouverture de l’annonce après la fin.
- Paquet graphique : 16 objets alignés sur 16 pixels et sans chevauchement de leurs emplacements, 16 cellules du chevalier rapproché, variantes et lance avec alpha dur ; comparaison à l’échelle native conservée.
- Textes ajoutés en en/fr/es ; paramètres de formatage contrôlés. La préférence de langue existante n’est pas modifiée.

Le rendu complet, les scènes royales jouées et l’expérience réseau avec deux clients n’ont pas été validés manuellement. PvP, gradins, récompenses et tournois restent à implémenter. Les critères correspondants du cahier V2 ne sont pas cochés par ces tests.
## Éditeur guidé et économie — 26 octobre 2026

La grille de propriétés interne est remplacée par des contrôles nommés : montures du serveur, scènes existantes, règles, zones de visée et offres de boutique. Le préréglage William peut être ajouté depuis l’éditeur. Les trois outils de placement se commandent avec des boutons et un clic gauche ; les problèmes de géométrie sont expliqués. Une scène liée se rouvre dans le Cinematic Director avec son contenu et son identifiant existants.

L’économie en médailles est active par défaut. Résultats et reçus utilisent la sauvegarde native du personnage. Les échanges sont enregistrés avec l’inventaire sous le verrou de sauvegarde du compte et annulés intégralement si cette sauvegarde échoue.

Contrôles ajoutés : résultats terminaux, rejeu après rechargement, plafond quotidien UTC, délai entre versements, abandon/absence de touche, modèle William, achat atomique, inventaire plein, solde insuffisant, reçu d’achat et retour arrière après échec/exception. Les tests serveur ciblés passent avec 28 cas. Le parcours réel du formulaire est exercé en en/fr/es : noms, choix de monture, édition du brouillon, placement par boutons, confirmation de sauvegarde et réouverture d’une scène personnalisée. Les aperçus utilisent une carte synthétique explicitement indiquée.

`scripts/Verify-Jousting.ps1` inclut maintenant ces contrôles. Le workflow `Royal Jousting` les relance sur GitHub avec le parcours de l’éditeur. L’expérience graphique sur un client connecté et les autres systèmes V2 restent des critères distincts.
