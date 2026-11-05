# Commandes d'artisanat — atlas graphique

`resources/gui/crafting-orders/royal-crafting-atlas.png` est un atlas RGBA indexé de
448 × 310 pixels fabriqué **à partir des deux planches envoyées par le créateur** :
`1195.png` (cadre, blason, coins et parchemin) et `1196.png`
(icônes d'artisanat). Le fichier PNG final est **commité dans le dépôt**
et automatiquement copié avec le client par `Shared/CraftingOrdersAssets.props`.

Les coordonnées ci-dessous sont celles de la texture fournie, déjà intégrée dans
`CraftingOrdersWindow.Art()` avec `ImagePanel.SetTextureRect()`.

| Élément | X | Y | Largeur | Hauteur |
|---|---:|---:|---:|---:|
| Blason Corps Royaux | 0 | 0 | 170 | 70 |
| Coin haut gauche | 171 | 0 | 48 | 48 |
| Coin haut droit | 222 | 0 | 48 | 48 |
| Coin bas gauche | 273 | 0 | 48 | 48 |
| Coin bas droit | 324 | 0 | 48 | 48 |
| Parchemin | 0 | 73 | 135 | 164 |
| Bandeau couronné | 138 | 73 | 190 | 52 |
| Panneau de commandes | 138 | 132 | 51 | 50 |
| Rouleau / commande | 192 | 132 | 43 | 52 |
| Enclume | 286 | 132 | 48 | 47 |
| Aureons | 338 | 132 | 49 | 48 |
| Caisse | 179 | 191 | 40 | 43 |
| Outils | 224 | 190 | 46 | 46 |

## Règles d'intégration

- La capture de référence `1197.png` n'est **pas** utilisée comme fond d'écran :
  seule sa composition guide l'interface. Le monde du joueur reste visible.
- Le fond, les listes, les boutons, le texte et les formulaires sont de vrais
  contrôles Gwen. Les trois langues du projet continuent de fonctionner.
- Les icônes des objets proviennent des fichiers `ItemDescriptor` du jeu,
  pas d'armes ou de récompenses fictives dessinées dans la maquette.
- Ne pas afficher de faux filtres métier/qualité si les données du serveur
  ne prennent pas encore ces critères en charge.
- Les actions restent celles du protocole existant. Aucun changement du
  fonctionnement des paiements, livraisons ou métiers dans cette PR.

## Tests avant la fusion

1. Confirmer la compilation client Windows et le chargement du PNG depuis
   `resources/gui/crafting-orders/royal-crafting-atlas.png`.
2. Ouvrir le panneau par `Open Crafting Orders` dans l'Event Editor.
3. Vérifier l'onglet **Commandes disponibles**, recherche, filtre 24 h,
   sélection d'une carte et détail de la récompense/délai.
4. Vérifier **Mes commandes** et **Mes fabrications**, ainsi que les actions
   accepter, livrer et annuler sur deux comptes différents.
5. Dans **Publier une commande**, choisir un objet, saisir les termes, puis
   publier; un simple clic sur une recette ne doit jamais créer une commande.
6. Vérifier les textes FR / EN / ES et les petites résolutions disponibles.
