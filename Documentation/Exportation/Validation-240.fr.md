# Validation de la PR 240 — 25 novembre 2026

La branche est synchronisée avec `main` (`e081b2801`). Les améliorations déjà présentes dans `main` sont conservées : alignement avec le panneau approuvé, mesure de la police Empire, contraste indépendant du monde et aide superposée.

## Correctifs et ressources

- Les 13 images et les 10 sons approuvés sont désormais versionnés. Leurs empreintes sont identiques au manifeste existant et aux fichiers de `CURRENT CLIENT & EDITOR`.
- Les contrôles SHA-256, dimensions PNG, format PCM mono 16 bits, durée, signal non silencieux et absence d'écrêtage passent. Les trois traductions et leurs paramètres de format sont contrôlés.
- Les lingots utilisent le son métallique; les provisions utilisent le bruit d'empaquetage en bois. Les effets suivent la réponse serveur, pas une prédiction au clic. Un état inchangé ne rejoue pas les effets.
- Pagination compatible avec la police bitmap (`<` et `>`), compteurs bornés, aucune fausse progression sur une caisse vide.
- Une image de marchandise absente utilise l'icône de l'objet; un fichier ajouté puis rechargé peut apparaître dans une fenêtre déjà ouverte.
- Les retours à la ligne tiennent compte de l'échelle réellement dessinée. L'ajustement des textes d'Exportation utilise le même calcul que le contrôle Gwen, pour éviter les noms coupés.
- L'aide bloque les demandes d'empaquetage, de retrait et de scellement derrière elle; fermer l'aide restaure les interactions.

## Contrôles reproductibles

Résultats locaux : **352 tests client réussis**, 6 tests GPU ignorés par leur condition d'environnement; **363 tests serveur Exportation/naval réussis**; **12 combinaisons langue/résolution**, 84 captures et les contrôles de secours/rechargement réussis. Les compilations client standard et éditeur réussissent, avec les 23 ressources vérifiées dans chaque sortie. Les avertissements préexistants du projet sont conservés.

Un premier passage de la suite client pendant les compilations a échoué sur la fraîcheur d'un instantané naval (expiration à 1,5 seconde). La suite navale isolée passe ses 20 tests et le second passage complet passe ses 352 tests. Aucun comportement naval n'a été modifié pour contourner cet échec.

Le passage complet sur GitHub a également révélé deux acteurs de joute construits sans initialisation dans les tests et laissés avec leur finaliseur actif. Leur nettoyage pouvait interrompre le processus de test. Le finaliseur est désormais désactivé pour ces deux objets factices, comme pour les autres personnages de test; le code du jeu n'est pas modifié.

```sh
python scripts/exportation/validate_assets.py
dotnet test Intersect.Tests.Client/Intersect.Tests.Client.csproj -c Release
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter "FullyQualifiedName~Exportation|FullyQualifiedName~Naval"
dotnet run --project Utilities/ExportationUiTests/ExportationUiTests.csproj -c Release
dotnet build Intersect.Client/Intersect.Client.csproj -c Release
dotnet build Intersect.Editor/Intersect.Editor.csproj -c Release
```

Le banc d'interface instancie la vraie fenêtre Gwen et les véritables PNG. Il utilise des réponses serveur simulées et un rendu logiciel, sans connexion au serveur du jeu.

| Couverture | Cas |
| --- | --- |
| Langues | Français, anglais, espagnol |
| Résolutions | 800×600, 1280×720, 1600×900, 1672×941 |
| Captures | Préparation sur fond clair/sombre, aide, cargaison scellée, arrivée, livraison, perte |
| Interactions | Ajouter/retirer, pagination des marchandises et commandes, aide, scellement, remise, réessai |
| Autorité serveur | Aucune quantité prédite; voyage confirmé requis; téléportation insuffisante; réessai au marchand |
| Ressources | Icône de secours, rechargement d'un fond absent, 23 fichiers copiés dans les sorties client/éditeur |

Le workflow `Exportation interface and assets` rejoue ces contrôles et conserve les captures dans son artefact `exportation-interface-validation`. Les captures locales se trouvent dans `validation-local/exportation-ui/`.

## Recette connectée restante

Les captures logicielles ne remplacent pas le rendu GPU et la police Empire dans une session réelle. Les déclenchements audio sont contrôlés avec leurs fichiers; l'écoute dans le mixage du jeu reste à faire. La traversée complète, la remise, l'inventaire plein, la mort/le naufrage, puis la reconnexion doivent encore être parcourus avec deux événements de marchand sur une copie isolée.

La PR reste en brouillon pour cette recette finale. Aucun serveur, exécutable installé ni configuration de production n'est remplacé. Le client Intel HD 3000 et sa branche restent séparés et inchangés.
