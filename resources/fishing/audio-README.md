# Corps Royaux — Fishing audio pack

32 effets et ambiances originaux obtenus par synthèse procédurale pour ce mandat. Aucun enregistrement, échantillon, morceau de musique ou son tiers n’a été utilisé. Il s’agit de sons stylisés de jeu, pas de prises de son réalistes.

## Contenu

| Famille | Fichiers |
| --- | --- |
| Interface | `ui_open`, `ui_close`, `ui_select`, `ui_confirm`, `ui_error` |
| Outils | `cast_rod`, `cast_net`, `cast_cage`, `crate_raise`, `net_retrieve` |
| Pêche | `splash`, `bite`, `reel_loop`, `tension_loop`, `line_break`, `escape`, `catch` |
| Compétences | `skill_stabilize`, `skill_pull`, `skill_repair`, `skill_sink` |
| Automatique | `auto_start`, `auto_stop`, `auto_pause` |
| Légendaires | `legendary_appear`, `legendary_challenge_cue`, `legendary_challenge_success`, `legendary_challenge_fail`, `legendary_catch` |
| Progression | `xp_gain`, `level_up` |
| Ambiance | `ambience_sea_loop` |

Les noms physiques suivent `wav/fishing_<identifiant>.wav`. `crate_raise` désigne la remontée de la cage de pêche ; l’événement `cage_retrieve` lui est associé dans le manifeste.

## Format et lecture

- WAV PCM signé 16 bits, 44 100 Hz.
- Effets en mono ; ambiance de mer en stéréo.
- Plafonds de crête : −6 dBFS pour les effets ponctuels, −12 dBFS pour les boucles de moulinet et de tension, −18 dBFS pour la mer.
- Les trois boucles comportent un raccord croisé incorporé et une frontière de lecture choisie à faible variation. Aucun nouveau raccord croisé n’est nécessaire à chaque tour.
- Le lecteur doit boucler de la frame 0 à `loop_end_frame_exclusive`, et appliquer les fondus d’entrée/sortie du manifeste aux transitions d’état.

## Configuration dans Fishing Editor

`manifest.json` constitue une proposition de données d’importation, **pas un format déjà reconnu par le moteur**. Adapter les chemins au résolveur de ressources audio existant.

Chaque son expose : activation, ressource, volume, bus, hauteur et variation de hauteur, boucle, fondus, délai minimal, nombre de voix simultanées, groupe de concurrence et spatialisation. Les libellés existent en `en`, `fr`, `es`.

Les événements supplémentaires (inventaire plein, appâts épuisés, outil absent, interruption, changement d’outil, consignes de défi) réutilisent les effets existants et restent remplaçables individuellement. Une référence sonore nulle signifie « silence ».

Ordre proposé des réglages : global → zone → outil équipé → profil de prise → événement précis. L’éditeur doit rendre explicite la valeur héritée et la surcharge locale. Les durées, niveaux et distances proposés ne sont pas un équilibrage final.

### Règles de déclenchement

- Démarrer une boucle une seule fois à l’entrée dans son état ; l’arrêter à sa sortie.
- Terminer les boucles lors de la fermeture, de l’interruption ou de la destruction de la session.
- Les lancers et récupérations peuvent être spatialisés à la position du joueur dans le monde. Les contrôles, alertes de ligne et défis sont locaux au joueur.
- Éviter de jouer deux fois le même son via l’animation locale et sa réplication réseau.
- Les sons de lancer comprennent déjà une petite éclaboussure : désactiver l’événement `splash` supplémentaire pour le même contact.
- Les prises comprennent déjà une ponctuation musicale. Regrouper les gains d’XP et limiter leurs sons pour éviter un empilement à chaque objet.
- Les consignes des défis doivent toujours avoir un signal visuel ; le son ne doit jamais être la seule information permettant de réussir.
- Une seule ambiance marine est fournie. Les zones de rivière, lac, tempête ou intérieur peuvent avoir des ressources supplémentaires configurées par l’éditeur.

## Régénération

Prérequis : Python 3, NumPy, SciPy. Depuis ce dossier :

```bash
python render_audio.py
```

Pour générer une copie ailleurs :

```bash
python render_audio.py --out /chemin/vers/un/autre/dossier
```

Le script utilise une graine fixe par identifiant. Il produit les WAV, le manifeste et `validation.json`. Les empreintes de chaque WAV sont incluses. La reproductibilité binaire a été vérifiée dans le même environnement ; elle n’est pas garantie entre toutes les versions de dépendances.

## Validation et limites

`validation.json` vérifie les formats, les canaux, les durées, les crêtes, l’absence d’écrêtage, les extrémités nulles des effets ponctuels et la continuité des boucles. La vérification numérique a réussi sur les 32 fichiers.

**Aucune écoute humaine ni audition dans le jeu n’a été réalisée dans cet environnement.** Avant livraison finale en jeu, écouter les sons et régler leur équilibre avec l’ambiance, la musique, les autres joueurs et les préférences de volume. Les effets graves et les boucles doivent notamment être vérifiés sur casque et haut-parleurs.

## Origine

Original procedural synthesis for this task. Les fichiers de sons sont générés uniquement à partir d’oscillateurs, de bruit synthétique filtré et d’enveloppes décrits dans le script fourni. Aucune licence de contenu tiers n’est nécessaire pour des échantillons externes, car aucun n’est inclus. Appliquer la politique habituelle du projet à la distribution de ces créations.
