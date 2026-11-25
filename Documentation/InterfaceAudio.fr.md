# Sons des interfaces

Cette extension fournit 27 sons originaux courts pour les interactions auparavant silencieuses. Le menu principal, la connexion, le choix du personnage et les fenêtres du jeu utilisent le même mécanisme. Les sons sont composés à partir de textures synthétisées de bois, cuir, papier, métal et accents musicaux doux ; aucun échantillon externe n’est utilisé.

## Couverture

| Interface ou action | Retour sonore |
| --- | --- |
| Menu principal, connexion, inscription, choix/création du personnage | Ouverture royale discrète |
| Boutons et touches d’activation | Survol léger, clic, retour/fermeture |
| Onglets, listes, images cliquables, cases à cocher | Page, sélection, activation/désactivation |
| Curseurs de réglage | Crans courts au clavier, à la prise et au relâchement |
| Validation d’un champ texte | Validation courte, sans son à chaque caractère |
| Inventaire et sacs | Cuir et attaches |
| Banque, coffres et cargaison | Bois et serrure |
| Boutiques et échanges | Pièces |
| Journal, quêtes, contrats, donjons, métiers, saisons, actualités | Livre et parchemin |
| Personnage, maîtrises, équipements | Cuir et métal |
| Sorts et alchimie | Accent magique |
| Guilde, QG, amis, groupe, lobby, profils, montures et compagnons | Accent social |
| Carte, port et gestion navale | Carte dépliée |
| Artisanat, cuisine, atelier, maison | Outils |
| Confirmations, erreurs, informations, récompenses | Signaux distincts |
| Autres fenêtres, y compris futures fenêtres Gwen | Ouverture/fermeture générique |

Les boutons des mini-jeux sont couverts. Les sons de leurs actions de jeu, ambiances, musiques et résultats déjà configurés sont conservés. Cette extension n’ajoute pas de musique de fond et ne remplace pas les effets de combat ou de monde.

Les panneaux permanents du HUD, descriptions au survol, images décoratives et outils de débogage ne déclenchent pas de son d’ouverture. Une mise à jour de données ou une nouvelle image rendue ne rejoue pas le son de fenêtre. Les fenêtres créées cachées restent silencieuses jusqu’à leur présentation.

## Priorités et réglages

- Les fichiers configurés dans les interfaces et les sons propres aux activités ont priorité. Un fichier manquant laisse le retour générique prendre le relais ; un son déjà chargé empêche sa superposition avec le retour générique.
- Un même clic qui ouvre une fenêtre produit un seul retour générique. Erreurs et notifications passent avant les sons de navigation.
- Survols et répétitions sont espacés, les requêtes anciennes sont abandonnées et le nombre de sons simultanés est limité.
- Le réglage **Effets sonores** dans les paramètres contrôle également ces sons. Un volume nul évite de créer de nouvelles instances audio.
- Les champs de sons des fichiers d’interface restent utilisables. `DefaultInterfaceSounds: false` permet de désactiver les retours génériques d’un contrôle et de ses descendants. Les boutons dont les sons ont été explicitement désactivés par le code restent silencieux.
- Les fichiers de la palette sont dans `resources/sounds/interface/`. Ils peuvent être remplacés en conservant leur nom. La table des familles est dans `InterfaceAudio.ResolveWindow`.

## Livraison

`Shared/InterfaceAudioAssets.props` embarque les 27 WAV dans les sorties client et éditeur, y compris lors de la publication. Le manifeste `Interface-Audio-Assets.json` donne leur durée, niveau et empreinte. Le générateur reproductible est `Utilities/InterfaceAudioAssets/generate_audio.py` ; `--check` valide les fichiers sans les réécrire.

Les nouveaux fichiers audio seuls ne suffisent pas pour activer le mécanisme de remplacement : le client doit aussi être compilé avec ce changement. Aucun nouveau paquet réseau ni changement serveur n’est nécessaire. Aucun changement de profil Intel HD 3000 n’est inclus.

## Vérification

`Utilities/InterfaceAudioTests` utilise de vrais contrôles Gwen et un périphérique audio simulé pour vérifier souris, clavier, transitions de fenêtre, absence de doublons, silence des contrôles désactivés/décoratifs, volume nul, limites de répétition et libération des instances. Il contrôle aussi les familles du menu principal et des fenêtres du jeu. Le manifeste vérifie les WAV mono PCM16 44,1 kHz, leurs empreintes, la marge avant saturation et les débuts/fins silencieux.

La recette d’écoute en jeu consiste à ouvrir le menu principal puis l’inventaire, la banque, le journal, les paramètres et un mini-jeu possédant ses propres sons ; vérifier la souris, les raccourcis, les fermetures et le volume à zéro. Les tests automatisés ne remplacent pas cette écoute sur le matériel de jeu.

Validation locale du 23 novembre 2026 : 76 contrôles audio réussis, client et éditeur compilés, interfaces équipement vérifiées en EN/FR/ES à 640×480, 1280×720 et 1920×1080. Les 27 fichiers sont présents et identiques au manifeste dans les sorties client/éditeur et dans `CURRENT CLIENT & EDITOR/resources/sounds/interface`. Les exécutables installés n’ont pas été remplacés. Le générateur avec `--check --preview` produit un aperçu d’écoute dans `validation-local/interface-audio-preview.wav`.
