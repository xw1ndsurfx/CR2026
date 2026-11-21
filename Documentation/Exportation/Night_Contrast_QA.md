# Exportation — contraste des GFX, jour et nuit

## Problème corrigé

L’interface Exportation affichait du bois et des parchemins presque noirs
sur les cartes nocturnes. Le voile de fond (180/255) était trop fort et
les PNG de la maquette ont des portions transparentes : sans fonds opaques
propres à l’interface, le monde noirci se voyait à travers les caisses.
Certains textes étaient également brun foncé sur les zones sombres.

## Comportement attendu

- Le monde derrière la fenêtre reste discernable, légèrement obscurci.
- Les panneaux Bon de commande, Préparer les caisses et Livraison ont
  un fond opaque, indépendant de l’heure, des intempéries et du brouillard.
- Les GFX existants sont dessinés au-dessus de ces fonds, sans changer
  les fichiers PNG et sans teinte violette.
- Des lavis chauds discrets relèvent les zones sombres, mais préservent
  le bois, le parchemin et les ornements.
- Les objets et leurs quantités restent lisibles même si les sprites
  PNG sont transparents; l’action principale et Fermer ont un bouton
  avec bordure suffisamment contrastée.
- Les niveaux et la barre d’EXP d’Exportation, les interactions réseau,
  les récompenses et les règles navales restent inchangés.

## Validation manuelle après compilation

1. Lancer une commande Exportation de jour et de nuit, dans le même port;
   vérifier que la fenêtre conserve sa luminosité aux deux moments.
2. Ouvrir l’interface avec une commande de 1, puis de plusieurs objets.
   Vérifier les icônes, caisses, quantités, barres, titres et boutons.
3. Vérifier l’ouverture et la fermeture à 1600×900 et à 1280×720;
   aucun contrôle ne doit devenir illisible à cause du voile de fond.
4. Faire une préparation complète et sceller; vérifier que les couleurs
   et les textes changent correctement selon les actions possibles.
5. Tester avec les GFX facultatifs manquants : le cadre de secours doit
   rester lisible et le client ne doit pas planter.

Le test NUnit de contraste garde l’opacité du voile sous 100/255 et
protège la présence des couches de fond. Il ne remplace pas une
validation visuelle avec les vrais PNG ni un test in-game de nuit.

## Artwork alignment and Empire 7 at global size 10

The approved 1672x941 background (manifest SHA-256 unchanged) is the layout reference. The help control is on its original upper-right red plaque. Cargo counts, shelf cells, progress bar, recipient panel and bottom actions are anchored to their matching artwork. Single-page pagination is hidden. Item icons keep their aspect ratio.

Texture drawing now honors the explicit tint argument, rather than inheriting the preceding backing fill. This prevents brown/low-alpha fills from darkening the parchment and inventory icons. Tests cover explicit white and translucent tints, including horizontal mirroring and clipping.

Empire 7 stores small visible glyphs inside 16px cells. Exportation now fits text to measured visible capital-letter ink and compensates vertical centering, while preserving the selected font and global font size. Fitting results are cached until text, layout or font settings change. Validate preparing, sealed, lost and delivered states at 1280x720 and 800x600, in en/fr/es. The help question mark is an intentional help control, not a missing glyph.
