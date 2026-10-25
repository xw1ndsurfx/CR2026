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
