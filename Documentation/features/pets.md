# Familiers - Corps Royaux

Premiere version serveur + NPC Editor (pas encore de fenetre client dediee).

## Game Editor
Dans NPC Editor : cocher Summonable companion, choisir un item d'invocation et definir la portee du loot, le niveau maximal, la croissance des stats/HP et la frequence de deblocage des sorts. Le sprite, les sorts, le comportement de combat de base et les autres stats sont ceux du NPC.

Donner l'item d'invocation avec une quete, une boutique ou un evenement. Utiliser un item de type None comme jeton de familier. L'utilisation ne consomme pas le jeton; elle debloque le familier de facon permanente sur ce personnage.

## Joueur
/pet list : familiers debloques
/pet summon NOM : invoquer/ranger le familier
/pet dismiss : ranger
/pet status : niveau et EXP
/pet loot on / /pet loot off : collecte automatique

Un familier actif par personnage. EXP du familier = 25% de l'EXP de l'ennemi vaincu. Les sorts definis dans le NPC Editor sont debloques dans l'ordre. La collecte respecte l'attribution des objets et la capacite de l'inventaire. Le familier ne doit jamais etre considere comme un NPC de donjon/invasion.

## Verification necessaire
Tester sauvegarde des niveaux, combat et credit EXP/quetes/achievements, sorts, ramassage en loot prive/partage, inventaire plein, mort, reconnexion, changement de carte et instances dungeon. Compiler et valider les migrations sur une copie des bases avant fusion.
