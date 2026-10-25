# Scène interactive de validation — copie locale seulement

Le générateur `Utilities/CinematicEditorSmoke` accepte `--interactive-scenario <export-natif> <dossier-sortie>`. Il prépare une scène sur la terre à l'est de Polonya Port, des événements communs, trois variables personnelles et leurs textes en/fr/es. Il ne modifie aucune base.

Le générateur vérifie les cases de départ/trajet/interaction et compile les commandes natives. Il rafraîchit également le cache `PagesJson` des descripteurs après leur édition. Le son optionnel utilise uniquement le fichier existant `Barkeeper_Chat_1.wav`, lorsqu'il est présent dans les voix de l'installation de test.

## Installer et lancer le scénario

1. Arrêter le serveur local. Utiliser un dossier séparé contenant la copie de `gamedata.db` et une base joueur locale neuve. Ne pas copier les comptes du serveur hébergé.
2. Le dossier serveur doit avoir le marqueur `.cinematic-test-sandbox` contenant exactement `Royal Cinematic Director local QA only`, ainsi que le nom `CR2026 Cinematic Local Test` dans `resources/config.json`. L'installateur refuse les dossiers non marqués.
3. Exécuter `python scripts/Install-CinematicQaSandbox.py <dossier-scenario> <dossier-serveur-local>`. Une sauvegarde de la base de contenu est conservée avant la première installation. Aucun accès au panneau hébergé n'est effectué.
4. Dans cette copie jetable seulement, l'installateur suspend les déclencheurs automatiques des événements communs d'origine et place le départ des classes sur le site de test. Les Events historiques et leur contenu sont conservés dans la sauvegarde. Les nouvelles variables sont personnelles ; il n'y a aucune récompense.
5. Démarrer les builds correspondants du serveur et du client, avec les ressources copiées et le client configuré sur `127.0.0.1:15463`. Aucun fichier de l'installation originale n'est remplacé.
6. Créer/connecter un compte local de test, puis saisir `/cinematicqa`. Cette commande remet les indicateurs de test à zéro et place le joueur au départ prévu.

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
