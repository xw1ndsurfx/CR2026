# Postes de canon panoramiques

28 atlas couvrent les quatre formats de combat (trois-mâts, sloop, radeau et Ship of the Line) des sept familles : Corps Royaux, Aventurier, Herbonia, Majestya, Squelette, Fantôme et Pirate. Les sept bateaux de pêche conservent leurs emplacements de pêche.

Le premier plan occupe toute la largeur de la fenêtre. Son rapport d’aspect est conservé ; sur une fenêtre très large et basse, seule la partie supérieure du cadre est recadrée. L’océan et l’atmosphère restent rendus en direct derrière la transparence des images.

Chaque atlas contient trois poses de visée (gauche, centre, droite). Les Ships of the Line possèdent trois poses supplémentaires pour la batterie inférieure. Le décor reste fixe pendant la visée : le changement de pose concerne le canon.

## Choix dans l’éditeur

L’option **Adapter le poste de canon intégré au navire** sélectionne automatiquement la variante adaptée lorsque le champ graphique utilise `Ship_Crew_Cannon.png`. Un fichier personnalisé explicitement choisi conserve la priorité. Si la nouvelle ressource manque, le client tente le graphique configuré d’origine. Le fichier d’origine n’est pas remplacé.

Le nom du navire est reconnu dans ses états normal, idle et endommagé. Le style du poste reste donc cohérent entre ces états.

## Fichiers et découpe

Les images sont dans `resources/ocean/Ship_Crew_*_Wide_v1.png`. Chaque fichier possède un voisin `.png.crew.json` de version 2 qui décrit les rectangles exacts des poses. Une marge supérieure écarte les pixels du plancher de la pose précédente, responsables de la ligne parasite. Les métadonnées de version 1 restent compatibles avec les anciens graphiques personnalisés.

Les images ont été générées avec l’outil ImageGen intégré, sur fond transparent, puis copiées sans modification de leurs pixels. [Assets.json](Assets.json) conserve les consignes de génération, les dimensions, les noms de fichiers et leurs empreintes SHA-256. Le brouillon Squelette dont le fond était incorrect a été remplacé par une extraction corrigée avec ImageGen.

## Vérification

Les tests `CannonStationLayoutTests` et `NavalPerspectiveTests` vérifient la géométrie, la sélection des variantes et des batteries et la compatibilité des métadonnées. L’utilitaire `Utilities/NavalCannonArtValidation` contrôle les 28 atlas, leurs empreintes, les limites des poses, la transparence, les bords du pont et les lignes parasites. Il produit une planche de contrôle sur un fond bleu de substitution ; cette planche n’est pas une capture en jeu.

Cette modification ne déploie aucun exécutable. Le client privé Intel HD 3000 reste séparé.
