# Scène interactive de validation — copie locale seulement

Le générateur `Utilities/CinematicEditorSmoke` accepte `--interactive-scenario <export-natif> <dossier-sortie>`. Il prépare une scène sur la terre à l'est de Polonya Port, des événements communs, trois variables personnelles et leurs textes en/fr/es. Il ne modifie aucune base.

Le générateur vérifie les cases de départ/trajet/interaction et compile les commandes natives. Il rafraîchit également le cache `PagesJson` des descripteurs après leur édition. Le son optionnel utilise uniquement le fichier existant `Barkeeper_Chat_1.wav`, lorsqu'il est présent dans les voix de l'installation de test.

## Installer et lancer le scénario

1. Arrêter le serveur local. Utiliser un dossier séparé contenant la copie de `gamedata.db`. Commencer avec une base joueur locale neuve ; si les inscriptions sont volontairement bloquées, une copie de comptes peut être utilisée uniquement après autorisation explicite du propriétaire. Sauvegarder la base locale et vérifier l'intégrité du snapshot avant le remplacement. Aucun import de comptes n'est effectué par l'installateur du scénario.
2. Le dossier serveur doit avoir le marqueur `.cinematic-test-sandbox` contenant exactement `Royal Cinematic Director local QA only`, ainsi que le nom `CR2026 Cinematic Local Test` dans `resources/config.json`. L'installateur refuse les dossiers non marqués.
3. Exécuter `python scripts/Install-CinematicQaSandbox.py <dossier-scenario> <dossier-serveur-local>`. Une sauvegarde de la base de contenu est conservée avant la première installation. Aucun accès au panneau hébergé n'est effectué.
4. Dans cette copie jetable seulement, l'installateur suspend les déclencheurs automatiques des événements communs d'origine et place le départ des classes sur le site de test. Les Events historiques et leur contenu sont conservés dans la sauvegarde. Les nouvelles variables sont personnelles ; il n'y a aucune récompense.
5. Démarrer les builds correspondants du serveur et du client, avec les ressources copiées et le client configuré sur `127.0.0.1:15463`. Aucun fichier de l'installation originale n'est remplacé.
6. Se connecter avec un compte du bac de test, puis saisir `/cinematicqa`. Cette commande remet les indicateurs de test à zéro et place le joueur au départ prévu. Conserver le blocage des inscriptions si l'équipe le demande ; les essais doivent rester limités à cette copie, sans synchronisation vers le serveur hébergé.

## Résultat attendu

- Le joueur avance d'une case pendant un mouvement de caméra avec zoom.
- La scène attend la confirmation du dialogue, puis le choix de la branche A ou B. La branche B enregistre la valeur personnelle vraie dans `qa_cinematic_choice`.
- Le dialogue explique l'action requise. Après confirmation, le contrôle revient au joueur. Avancer d'une case vers le bas pour entrer sur le repère d'interaction.
- Le serveur constate la collision avec l'Event personnel et modifie `qa_cinematic_interaction`. Le dialogue final doit rester inaccessible tant que cet indicateur est faux.
- La scène reprend, attend le dernier dialogue, puis marque `qa_cinematic_complete` et restitue les contrôles normaux.
- Un second joueur lance sa propre scène : le choix et l'action du premier ne doivent ni débloquer la scène du second ni modifier sa caméra.

Tester aussi un départ invalide, une route bloquée, changement de carte/instance, mort et déconnexion. Ne pas déduire ces résultats de la seule existence du scénario.

## État observé le 2026-10-24

Le scénario a été généré et installé dans la copie locale ; le serveur démarre avec son contenu. Les tests serveur cinématiques passent : 17 cas couvrent notamment une boucle d'attente répétée 100 fois sans croissance de pile, un saut vers une branche, les interruptions de cycle de vie, la conservation des contrôles/trajectoires d'autres actions et les variables de deux joueurs distincts.

Deux échecs de régression ont été reproduits avant la correction du saut natif vers une étiquette : accumulation de cadres de pile et ordre incorrect pour une cible imbriquée. La correction conserve le cadre appelant et reconstruit la pile dans l'ordre d'exécution.

Le lancement du client de test échoue avant la connexion avec `NoSuitableGraphicsDeviceException` / absence de `ARB_framebuffer_object` ou `EXT_framebuffer_object` dans le contexte OpenGL. L'éditeur a pu rendre la vraie carte avec son backend DirectX ; cela ne valide pas le backend OpenGL du client. Aucun résultat de parcours connecté, de persistance des indicateurs ou de multijoueur graphique n'est donc revendiqué. Aucune configuration graphique Windows ni aucun pilote n'a été modifié.

## Reprise avec les bibliothèques de compatibilité existantes

L'équipe a ensuite fourni son dossier de compatibilité Intel HD 3000. En copiant ses bibliothèques OpenGL existantes **à côté de l'exécutable de test**, le client dépasse l'erreur graphique et charge son interface. Il ne faut ni les installer dans Windows ni remplacer l'exécutable de la distribution existante.

`scripts/Start-CinematicQaClient.ps1` prépare ce lancement reproductible à partir du build, du dossier de compatibilité fourni et des ressources de la copie locale. Il exige un nouveau dossier de sortie, ou un dossier de test déjà marqué, et une configuration locale sur le port 15463 sans URL de mise à jour. `-PrepareOnly` prépare les fichiers sans lancer le jeu. Aucun téléchargement de bibliothèque n'est effectué et ces binaires ne sont pas distribués dans le dépôt.

La capture automatique de la fenêtre OpenGL a expiré à deux reprises. Le démarrage du processus et le chargement de l'interface sont constatés dans les journaux, mais ne prouvent pas encore le déroulement de la scène. La création/connexion d'un compte neuf et le parcours interactif restent à observer sur le client local.

Pour cette session, le propriétaire a ensuite autorisé une copie des comptes existants afin de garder les inscriptions désactivées. Cette copie a été vérifiée et installée après sauvegarde locale ; elle reste hors du dépôt et des artefacts distribués. L'API REST de la copie locale est désactivée. Aucun compte, mot de passe, personnage ou progrès du serveur hébergé n'est modifié par les essais.

## Premier essai connecté : recherche de scène corrigée

Le client a atteint Polonya Port et son départ prévu, mais le serveur refusait l'appel avant le démarrage. La commande consultait `MapDescriptor.LocalEvents`, une collection renseignée par l'éditeur et non par le chargement serveur. Elle utilise maintenant l'appartenance dans `EventIds` et le registre natif `EventDescriptor`, tout en vérifiant la carte du descripteur et du source, le caractère personnel de l'Event et l'unicité de sa page. Les conditions serveur de la page restent actives.

Huit nouveaux cas reproduisent la structure d'une carte chargée côté serveur, avec `LocalEvents` vide, et refusent les références non déclarées, les Events d'une autre carte, communs ou globaux, les pages multiples et les sources manquants/incohérents. Les 25 tests serveur cinématiques passent après cette correction ; les builds Server.Core et Server passent également. Le déroulement complet doit encore être réessayé dans le client connecté.

## Deuxième essai : caméra seule, trajet du joueur bloqué

Le propriétaire observe la caméra mais pas la suite. Le verrou natif `HoldingPlayer` bloquait aussi la Move Route du joueur exécutée par le serveur : trois tests reproduisent cet échec avant correction. Pendant l'appel de l'interpréteur natif seulement, le trajet peut maintenant ignorer le verrou de sa propre scène active. Cette permission exige la page propriétaire et la carte/instance capturées ; elle est limitée au thread qui exécute le trajet et retirée même en cas d'exception. Les entrées manuelles concurrentes, les verrous d'autres Events, les collisions, la mort et les règles historiques restent soumis aux contrôles existants. Un trajet dont la carte/instance ou la page a changé n'avance pas, même après restitution temporaire du contrôle.

Les 35 tests serveur cinématiques réussissent, dont dix cas de trajet (progression et fin natives, entrée concurrente, terrain, mort, autre verrou, route historique, exception et trois invalidations). La capture Windows du client Intel expire toujours ; la touche de capture intégrée n'a pas produit de nouvelle image lors de l'essai. Ces tests ne valident donc pas encore le parcours visuel complet, les dialogues/choix/action dans le client ni deux clients simultanés.
