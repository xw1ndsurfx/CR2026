# Commandes d'artisanat - pack GFX

Les sprites sont prévus pour le panneau indépendant `CraftingOrdersWindow`, ouvert
par `Open Crafting Orders` dans l'Event Editor.

## Déploiement des images

Le pack `crafting-orders-gfx-pack.zip` généré séparément contient les PNG
extraits des deux planches conceptuelles. Extraire l'archive **à la racine du dépôt**
afin d'obtenir les fichiers sous :

```text
resources/gui/crafting-orders/
  icon-board.png
  icon-scroll.png
  icon-checklist.png
  icon-tools.png
  icon-anvil.png
  icon-crate.png
  icon-aureons.png
  icon-hourglass.png
  icon-sword.png
  icon-shield.png
  icon-success.png
  icon-cancel.png
  icon-package.png
```

Puis ajouter les PNG au commit de cette branche avant le merge. Le client charge
ces textures depuis `TextureType.Gui` et `Shared/CraftingOrdersAssets.props`
les copie dans les sorties Windows/publish.

Les libellés et actions restent des composants Gwen natifs et localisés en
français, anglais et espagnol. Les images ne servent qu'à la décoration; les
texte des boutons ne doivent pas être incrustés dans les sprites.

## Validation

1. Vérifier les chemins et la casse des noms de PNG dans la sortie du client.
2. Compiler `Intersect.Client/Intersect.Client.csproj` et la solution.
3. Ouvrir `Open Crafting Orders` depuis un événement NPC/tableau.
4. Tester les onglets et les trois langues, y compris une résolution basse.
5. Tester le comportement lorsqu'un sprite est absent (fallback Gwen).
