# Royal Guide — Phase 2 : guidage dans le monde et éditeur visuel

## Dépendance

La PR #247 est fusionnée. Cette branche intègre `main` après cette fusion
(`f369442bf67b49d24d3c170fe78e4d46162a3785`) et préserve ses correctifs de
disposition, modalités, reprise et nettoyage des contrôles.
Aucun changement de schéma SQL ou de protocole réseau n'est requis :
les nouveaux champs sont ajoutés au JSON de configuration existant.

## Nouveaux repères en jeu

- **Player** : un cercle animé et des flèches directionnelles encadrent le
  personnage. Les nouveaux guides par défaut l'activent pour le mouvement.
  Une étape existante réglée sur `None` reste sans repère ; ses identifiants
  et sa progression sont préservés.
- **WorldTile** : sélection d'une carte et d'une case X/Y ; marqueur au sol
  avec flèche lorsque la destination est visible.
- **WorldNpc** : sélection d'une carte et d'un NPC descriptor ; l'entité
  correspondante la plus proche est ciblée **par identifiant** et non par
  correspondance approximative du nom.
- Si le repère est hors écran, la direction est indiquée sur la bordure.
  Si la carte n'est pas chargée ou si le PNJ n'existe plus, est masqué ou
  furtif, le repère est
  masqué (pas de faux objectif).
- Les marqueurs se recalent sur `Graphics.CurrentView` et `Graphics.WorldScale`
  : zoom, échelle d'interface, déplacement de caméra et cartes adjacentes
  sont pris en compte. Le sol suit les pieds, le médaillon suit la tête.
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
- choisir une bordure ou un cercle pour une cible d'interface ;
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

## Images et sons

Les PNG transparents se trouvent dans `resources/gui/royal-guide/` :
`guide_npc_v1.png` (médaillon royal) et `guide_ground_v1.png` (anneau au sol).
Les images originales sont conservées sans retouche ; les prompts de génération
intégrée et empreintes sont dans `RoyalGuideAssets.json`. Le rendu garde un repère
géométrique de secours lorsque l'image n'est pas installée.

Les trois sons originaux sont dans `resources/sounds/` :

- `royal_guide_objective_v1.wav` : nouvel objectif, 0,65 seconde ;
- `royal_guide_step_v1.wav` : progression validée, 0,95 seconde ;
- `royal_guide_complete_v1.wav` : fin du guide, 1,50 seconde.

Ils sont synthétisés par `tools/generate_royal_guide_sounds.py`, sans échantillons
externes, en PCM16 mono à 44,1 kHz, avec marge avant saturation. Ils utilisent le
volume des effets du jeu. Les paquets répétés et pause/reprise ne rejouent pas le
son ; les cinématiques et fenêtres modales diffèrent le signal. Les exercices
de combat conservent leur présentation existante. Aucun moteur audio spécifique
au client Intel n'est ajouté à cette branche.

## Validation locale

Les contrôles automatisés et les commandes reproductibles sont consignés dans
`RoyalGuidePhase2-validation.fr.md`. L'essai réel avec un personnage connecté
reste à effectuer : l'aperçu de l'éditeur et les tests sans GPU ne le remplacent pas.
Les cartes et PNJ choisis dans le contenu du serveur doivent être configurés
avant cette recette. Le guidage ne calcule pas d'itinéraire entre cartes non chargées.
