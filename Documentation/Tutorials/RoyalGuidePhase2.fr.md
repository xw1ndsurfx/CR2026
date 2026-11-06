# Royal Guide — Phase 2 : guidage dans le monde et éditeur visuel

## Dépendance

Cette branche commence à la tête de `feat/royal-guide-interactive-tutorial`
(PR #247). **Fusionner et tester la phase 1 avant la phase 2.**
Aucun changement de schéma SQL ou de protocole réseau n'est requis :
les nouveaux champs sont ajoutés au JSON de configuration existant.

## Nouveaux repères en jeu

- **Player** : un cercle animé et des flèches directionnelles encadrent le
  personnage. Les tutoriels de mouvement existants en bénéficient sans
  réinitialiser la progression.
- **WorldTile** : sélection d'une carte et d'une case X/Y ; marqueur au sol
  avec flèche lorsque la destination est visible.
- **WorldNpc** : sélection d'une carte et d'un NPC descriptor ; l'entité
  correspondante la plus proche est ciblée **par identifiant** et non par
  correspondance approximative du nom.
- Si le repère est hors écran, la direction est indiquée sur la bordure.
  Si la carte n'est pas chargée ou si le PNJ n'existe plus, le repère est
  masqué (pas de faux objectif).
- Les marqueurs se recalent sur `Graphics.CurrentView` et `Graphics.WorldScale`
  : zoom, déplacement de caméra et cartes adjacentes sont pris en compte.
- Ces repères n'interceptent ni souris, ni clavier, ni déplacements. Les
  cinématiques et exercices de combat les désactivent.

## Nouvel objectif autonome : ReachTile

Le serveur valide automatiquement un objectif `ReachTile` lorsqu'un joueur
arrive sur la bonne carte et à moins de `TargetRadius` cases de X/Y
(distance en cases sur chaque axe). La position du client n'est **jamais**
utilisée comme preuve : seules les coordonnées côté serveur comptent.

Paramètres : `MapId`, `TargetX`, `TargetY`, `TargetRadius` (0–6).
`WorldTile` est une aide visuelle ; `ReachTile` est la condition de
progression. Les deux peuvent être utilisés sur la même étape.

Pour un tutoriel PNJ, choisissez `WorldNpc` et conservez un objectif existant
(`Variable` si l'événement du PNJ met à jour une variable, ou `Read` avec
bouton « Suivant »). Le guidage visuel **ne prétend pas** détecter toutes
les interactions NPC automatiquement.

## Tutorial Editor

L'onglet **Guide visuel** permet :

- choisir la cible HUD, joueur, case ou PNJ ;
- choisir une bordure ou un cercle ;
- choisir une vraie carte dans la liste ;
- choisir un vrai PNJ par son identifiant ;
- saisir les coordonnées X/Y et le rayon de détection ;
- examiner l'aperçu **illustratif animé**, qui n'est pas une capture de la
  carte actuelle. Le PropertyGrid d'origine reste disponible.

La configuration JSON est validée avant enregistrement. Les anciennes étapes
ne reçoivent pas artificiellement de nouveaux IDs, et les champs manquants
désérialisent avec leurs valeurs par défaut.

## Scénarios de test sur un client/serveur de développement

1. **Ancien personnage** : reprendre un tutoriel de phase 1 sans perte de
   progression après installation de la phase 2.
2. **Déplacement** : cercle et flèches sur le joueur, puis progression
   automatique après un déplacement réellement enregistré par le serveur.
3. **Case précise** : configurer une vraie carte, X=8, Y=8, rayon=1 ;
   rester à distance sans avancer, puis s'approcher et vérifier la réussite.
4. **Carte erronée** : même X/Y sur une autre carte ne termine pas l'étape.
5. **PNJ** : sélectionner un NPC existant, vérifier le halo/flèche puis
   déplacer le PNJ ; vérifier que le repère suit l'entité.
6. **PNJ absent / carte non chargée** : pas de marqueur trompeur ni crash.
7. **Zoom / caméra / fenêtre** : vérifier les repères au bord de l'écran et
   l'ajustement des marqueurs sans décalage.
8. **Combat / cinématique** : repères masqués sans bloquer les commandes.
9. **Éditeur** : changement de cible, carte, PNJ, coordonnées, sauvegarde,
   fermeture, réouverture et persistance des valeurs.
10. **Validation** : rejeter une carte absente, un NPC inexistant, des
    coordonnées hors carte et un rayon supérieur à 6.
11. **Langues** : vérifier les intitulés français, anglais et espagnols.
12. **Régression** : quêtes et invocations d'interface de la phase 1
    restent fonctionnelles.

## État de validation

Des tests NUnit de configuration ont été ajoutés dans
`Intersect.Tests/TutorialTests.cs`. L'environnement GitHub utilisé
pour créer les commits **n'a pas accès à un SDK .NET, à l'éditeur Windows
ni à un serveur de jeu** : aucune compilation ou validation en jeu n'a été
effectuée ici. Ne pas fusionner comme « testé » sans faire cette vérification.
