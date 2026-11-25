# Recherche de groupe pour les donjons et expéditions

## Accès joueur

Dans **Lobby**, utiliser **Trouver une aventure**. Choisir un donjon ou une expédition disponible, puis **Trouver un groupe**. Le chef inscrit son groupe complet ; un joueur seul peut aussi s'inscrire. Les groupes existants ne sont pas séparés.

Quand une équipe compatible est trouvée, une fenêtre demande à **chaque joueur** de confirmer. Aucun départ n'est déclenché par la simple inscription ou par l'accord du chef. Le délai est affiché ; le serveur reste responsable de son expiration. Fermer la fenêtre maintient la recherche. **Quitter la recherche** retire le groupe inscrit avec le joueur.

Un refus retire le groupe du participant concerné ; les autres groupes retournent dans la file. À l'expiration, seuls les groupes dont tous les membres ont confirmé conservent leur place et leur ancienneté. Une nouvelle proposition exige de nouvelles confirmations. Les demandes périmées ou provenant d'un autre participant ne peuvent pas valider un départ.

## Donjons et équipages

Le serveur vérifie le groupe, les niveaux, le Premium et la quête éventuellement exigés, le calendrier du donjon, les restrictions de téléportation, le combat et les autres activités en cours. Le groupe entre dans une nouvelle instance partagée, ou personnelle pour un départ solo. Si un membre ne peut pas entrer, le départ est annulé avant l'enregistrement de la tentative et les joueurs sont ramenés à leur origine.

Les expéditions acceptent un équipage sur un navire ou une flotte selon leurs règles existantes. Les capitaines doivent attendre à bord de navires arrêtés au même port, près des quais Q. Chaque équipage déjà embarqué doit être inclus dans son groupe inscrit. Des joueurs à terre peuvent compléter les places disponibles ; la proposition affiche leur navire attribué. L'embarquement n'a lieu qu'après l'accord de tous. Le niveau de Navigation, les places, les capitaines, l'absence de combat/abordage et les récompenses encore à réclamer sont revérifiés au départ.

Les changements de groupe, de carte, de navire, de conditions d'accès ou de définition invalident la recherche concernée. Une reconnexion crée une nouvelle session et exige une nouvelle inscription. Les files et confirmations sont temporaires ; elles ne survivent pas à un redémarrage serveur. Les expéditions démarrées utilisent leur journal persistant existant, sans nouveau mécanisme de récompense.

## Réglages dans l'éditeur

Ouvrir **Lobby Editor → Recherche de groupe**.

- **Paramètres** : activation globale, délai de confirmation (15–180 s), durée maximale de recherche (60–3 600 s), limite de joueurs en file, image de fond, planche de six icônes et sept sons. Les sélecteurs de médias conservent l'aperçu/recherche de l'éditeur.
- **Activités** : ajouter une entrée, choisir le type puis une définition existante, définir le nombre de joueurs visé et l'écart de niveaux autorisé (`0` sans restriction), choisir une image facultative et activer l'entrée.
- **Donjons** : choisir une carte appartenant au donjon, les coordonnées d'une case praticable et la direction d'arrivée. Le nombre de joueurs visé doit respecter les limites du donjon. Les groupes complets peuvent dépasser la cible sans dépasser le maximum de l'activité.
- **Expéditions** : les limites de joueurs/navires et le mode équipage/flotte proviennent de l'éditeur naval ; la recherche utilise les capitaines disponibles au port.

Enregistrer depuis le bouton **Save** du Lobby Editor. Les réglages sont persistés côté serveur dans `resources/lobbies.json`. Les anciennes configurations restent lisibles. Aucune activité n'est activée implicitement : la liste de recherche est vide tant que des entrées valides ne sont pas configurées. Le serveur refuse les destinations absentes, les cases non praticables et les tailles incompatibles.

## Ressources

Le [manifeste](Matchmaking-Assets.json) contient deux PNG et sept WAV originaux :

- `resources/gui/matchmaking_background_v1.png` : panneau royal brun, or et bleu.
- `resources/gui/matchmaking_icons_v1.png` : atlas transparent 3 colonnes × 2 rangées, recherche/prêt/refus puis donjon/navire/sablier.
- `resources/sounds/matchmaking/{open,queued,found,ready,cancelled,depart,error}_v1.wav` : PCM16 mono, 44 100 Hz, signaux courts avec attaque/fin adoucies et marge avant saturation.

Les sons sont synthétisés sans échantillons externes par `Utilities/MatchmakingAssets/generate_audio.py`. Les images proviennent de la génération d'images intégrée OpenAI, avec création originale et correction de transparence. Les builds normaux du client et de l'éditeur embarquent ces ressources via `Shared/MatchmakingAssets.props`. Les textes joueur et les nouveaux réglages sont traduits en EN/FR/ES.

Les neuf fichiers ont été ajoutés le 25 novembre 2026 dans **CURRENT CLIENT & EDITOR**, avec comparaison de leurs empreintes. Aucun exécutable, réglage serveur ou client Intel HD 3000 n'a été remplacé. L'utilisation de la nouvelle fonctionnalité exige un client, un serveur et un éditeur compatibles.

## Validation

La suite dédiée couvre les confirmations individuelles, tokens périmés/étrangers, refus, délais, groupes indivisibles, niveaux, capacité navale, destinations, départ équipage/flotte, persistance des expéditions et échec d'écriture. Les contrôles d'interface utilisent le vrai arbre Gwen en EN/FR/ES, à 640×480, 1280×720 et 1920×1080 ; ils restent distincts d'un essai connecté sur GPU.

Validation locale du 25 novembre 2026 : 20 tests dédiés réussis ; 362 tests de régression donjons/naval/lobbies réussis (les deux sélections se recouvrent partiellement). Les neuf combinaisons langue/résolution de l'interface et les trois langues de l'éditeur passent. Les builds Release du client normal et de l'éditeur réussissent, ainsi que la compilation serveur par les tests. Les neuf ressources installées sont identiques au manifeste. Les captures finales en français ont été inspectées à 640×480 et 1280×720.

Commandes de contrôle :

```text
python Utilities/MatchmakingAssets/generate_audio.py --check
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter "FullyQualifiedName~Matchmaking|FullyQualifiedName~MatchedNaval"
dotnet run --project Utilities/MatchmakingUiTests/MatchmakingUiTests.csproj -c Release
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Release -p:RuntimeIdentifiers=win-x64 -- --matchmaking-only
```

La recette connectée reste à effectuer avec plusieurs clients compatibles : donjon, équipage complété depuis la terre, deux capitaines au même port, refus/délai, changement de carte et reconnexion. Il faut configurer les activités voulues sur le serveur de test avant cette recette. La branche Intel HD 3000 conserve ses adaptations privées séparées.

## Briefs des images

Les demandes exactes de génération et de correction sont conservées dans [Matchmaking-Art-Prompts.json](Matchmaking-Art-Prompts.json).

Le fond a été demandé comme panneau médiéval en pixel art, bois/cuir brun sombre et or, accents bleus à fleurs de lys, petite arche de donjon et navire dans le bandeau supérieur, avec une grande zone centrale calme pour les vrais textes et boutons du jeu. Aucun texte n'est peint dans l'image.

L'atlas a été demandé en six cellules carrées régulières : loupe sur boucliers, sceau vert à coche, sceau rouge à croix, arche de donjon, trois-mâts bleu et sablier doré. Une passe d'édition a retiré le fond blanc et les lignes de grille, en conservant les silhouettes, les voiles claires et les symboles. Le contrôle de transparence et de visibilité est inclus dans la recette d'interface.
