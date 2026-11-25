# Pêche — validation isolée du 25 novembre 2026

Base : `main` après la PR 284 (`383b5288c85e927dfa808b96dfd6c4928d895d1d`). Branche locale : `codex/fishing-validation`.

## Corrections

- Les noms des quatre compétences occupent désormais le bas de leur bouton, avec un retrait adapté à la résolution. L'ancien alignement combinait centrage vertical et bas ; le centrage gagnait et superposait le texte à l'icône.
- Un fond sombre protège le compteur de prises des coraux et poissons clairs.
- Le vérificateur de médias contrôle maintenant les fichiers réellement copiés dans les compilations client et éditeur (`--output`, répétable), en plus des sources et des listes de livraison.
- Les contrôles de persistance SQLite et les captures de l'interface sont intégrés à la validation dédiée à la pêche. Aucun changement d'équilibrage ou de format réseau.

## Vérifications réalisées

- Serveur, client normal et éditeur : compilations Release réussies, avec les avertissements préexistants.
- Règles partagées : **475 contrôles** ; variante native serveur : **835 contrôles**, incluant événements, refus, interruptions, mode automatique, légendaires et transport réel sur boucle locale.
- Client : **352 tests réussis**, **6 scénarios externes ignorés**. Ceux-ci ne sont pas comptés comme validés.
- Éditeur : contrôles natifs de pêche réussis en anglais, français et espagnol (brouillon, application, refus, sélection des cartes, accusés de sauvegarde et ouverture depuis les menus).
- Persistance : **7 nouveaux cas SQLite réussis**, sur base vierge puis sur sauvegardes jetables de la copie restaurée. Sauvegarde réelle `User.Save`, puis rechargement des objets depuis un nouveau contexte :
  - prises, bonus, limites de piles, EXP active et EXP AUTO ;
  - appât consommé et conservé après une tentative sans récompense ;
  - refus réel d'écriture SQLite, annulation de l'ensemble et nouvelle tentative unique ;
  - inventaire plein sans remise partielle ;
  - banque Premium sans copie en inventaire ni répétition de la récompense.
- Interface : **36 captures**, quatre phases × trois langues × trois résolutions (800×600, 1280×720, 1920×1080). Contrôles de position des boutons et inspection visuelle des captures.
- Médias : **20 PNG**, **32 WAV**, **58 associations sonores** ; **1 448 contrôles réussis**, dont **114 fichiers vérifiés par empreinte** dans les deux sorties client/éditeur (57 chacune). Contrôles des découpages, transparence, niveaux PCM et raccords de boucles. Aucune nouvelle illustration ou bande-son n'était nécessaire.
- Configuration restaurée : un outil, une zone couvrant 26 cartes, cinq objets référencés et métier associé présents. AUTO : 50 % d'échecs, 20 % d'EXP normale. La configuration active n'a pas été remplacée.
- Serveur isolé : deux démarrages/arrêts propres, écoute exclusivement sur `127.0.0.1:52145`, base des joueurs intègre après chaque cycle. Données obtenues par sauvegarde SQLite ; comptes sources non modifiés par les tests.

## Limites et recette manuelle restante

Le contrôle des fenêtres Windows n'a pas pu démarrer (`failed to write kernel assets`, erreur de chemin Windows 3). Aucun essai manuel connecté, écoute des sons ou contrôle du rendu GPU n'est donc revendiqué. Les captures utilisent les contrôles de production et les PNG livrés, mais un moteur de rendu GDI+ et des métriques de police de substitution. Les états d'interface sont des exemples explicites ; ils ne constituent pas une session réseau.

Restent à faire dans le client connecté à la copie isolée : pêche active/AUTO avec les objets réels, interruption et reconnexion, légendaire accepté/ignoré, écoute des sons et observation du filet par un deuxième joueur. Les tests de charge restent également ouverts.

Une saturation du disque a interrompu un premier essai ; des sorties de compilation temporaires de la validation précédente ont été supprimées, puis les contrôles concernés ont été relancés avec succès. Le premier port prévu était occupé ; un port local libre a été choisi sans arrêter son utilisateur.

Les installations normale et Intel HD 3000, la branche Intel et le serveur de production n'ont pas été modifiés. Cette validation reste locale ; aucune publication ni fusion n'est incluse.

## Reproduire

Voir `Utilities/FishingUiTests/README.md` pour les captures et l'option `FISHING_TEST_DB`. `scripts/Verify-Fishing.ps1` regroupe les contrôles dédiés. Les logs et captures de cette exécution sont conservés dans `validation-local/` ; les données restaurées et leurs relevés restent hors dépôt, dans le dossier privé `2026-11-25/fishing-validation`.
