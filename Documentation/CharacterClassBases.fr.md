# Exporter les corps de base des classes

Dans Character Generator, utiliser **Exporter les bases (masculin + féminin)**. Le bouton d’ajout à la création de personnage redirige également vers cet export si un corps est sélectionné dans Base, Body ou dans les anciens dossiers Female. Les vêtements présents dans Female sont exclus de la liste des corps.

Choisir le corps masculin et le corps féminin, puis un nom d’export. Le générateur rapproche les noms des deux sexes quand il connaît une paire correspondante ; les deux listes permettent de corriger le choix. Les fichiers vont dans `resources/entities`, qui est la source des sprites de classes, et non dans les paperdolls de vêtements.

Pour `mage_base`, les deux séries sont :

- `mage_base_male.png`, puis `_attack`, `_cast`, `_idle`, `_shoot`, `_weapon` avant l’extension PNG ;
- `mage_base_female.png`, avec les mêmes variantes d’animation.

Les corps sont exportés seuls : les vêtements, cheveux, armes et accessoires de la composition ne sont pas incorporés. Chaque sexe garde ses propres pixels et dimensions. Les douze images sont préparées avant l’écriture pour qu’une source de corps manquante ne laisse pas un export uniquement masculin. Un export existant demande confirmation avant remplacement.

Après l’export, les textures d’entités et les listes de ressources sont actualisées. Choisir ensuite les deux sprites dans les apparences masculine et féminine de la classe. Les messages et les contrôles ajoutés existent en anglais, français et espagnol.

Ce correctif nécessite une nouvelle version de l’éditeur. Les images de base disponibles dépendent du contenu installé dans `charagen/Base` et `charagen/Body` ; il ne fabrique pas un corps féminin en recopiant silencieusement un corps masculin.
