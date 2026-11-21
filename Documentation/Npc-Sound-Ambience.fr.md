# Ambiance sonore des PNJ

Dans NPC Editor, onglet **Behavior**, le groupe **Ambiance sonore** propose un son à la mort, un son à proximité du joueur et une distance de déclenchement de 1 à 100 cases. Les choix proviennent de `resources/sounds/*.wav`. Un choix vide désactive le son correspondant.

Le son de proximité est local à chaque joueur : il joue une fois quand le PNJ entre dans le rayon, y compris si le joueur s’approche. Il se réarme après une sortie de plus d’une case au-delà du rayon, avec au moins cinq secondes entre deux déclenchements. La distance tient compte des cartes voisines de la grille chargée. Le son de mort suit le paquet de mort du serveur, une seule fois par instance du PNJ, avec l’atténuation spatiale habituelle.

Les paramètres sont inclus dans les données de PNJ et les migrations SQLite/MySQL. Les sons restent vides pour les PNJ existants.

Vérification manuelle : choisir deux sons distincts, sauvegarder et rouvrir le PNJ ; entrer dans son rayon, y rester, sortir puis revenir après cinq secondes ; tuer le PNJ ; vérifier aussi depuis une carte voisine. Les sons doivent respecter le volume d’effets du client.
