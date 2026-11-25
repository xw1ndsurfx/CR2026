# Configurations rapides, sorts d’armes et forge d’équipement

## Utilisation

Chaque personnage dispose de **cinq configurations nommées**. Ouvrir **Configurations d’équipement** dans le menu du jeu, choisir un numéro, saisir un nom et cliquer sur **Enregistrer**. La configuration mémorise toutes les pièces actuellement équipées, y compris les emplacements laissés vides. Un second clic confirme le remplacement d’une configuration existante.

**Équiper** applique la configuration choisie. Les cinq actions **Configuration d’équipement 1 à 5** sont disponibles dans **Paramètres > Commandes**. Elles ne reçoivent aucune touche par défaut pour préserver les raccourcis existants. Les touches sont enregistrées dans les préférences de ce client ; les configurations elles-mêmes sont enregistrées par personnage sur le serveur. La touche choisie apparaît dans la fenêtre. Un appui maintenu n’envoie pas une succession de changements ; la saisie dans un champ texte ou le chat ne déclenche pas les raccourcis.

Un astérisque indique une configuration enregistrée. Son nom est visible dans le champ et au survol de son numéro. **Supprimer** demande un second clic et efface seulement la configuration, jamais les objets. **Actualiser** recharge les disponibilités et l’état de combat, notamment après un changement d’inventaire ou la fin d’un combat.

## Règles de changement

- Utilisation hors combat uniquement, vérifiée par le serveur, y compris pour l’équipage d’un navire engagé en combat ou en abordage.
- Pas de changement pendant une mort, une incantation, une immobilisation ou une activité exclusive (échange, banque, artisanat, etc.).
- Les pièces doivent être présentes dans l’inventaire et remplir leurs conditions d’utilisation. Aucun objet n’est récupéré automatiquement dans un coffre ou créé par la configuration.
- Le tri de l’inventaire ne casse pas les configurations : elles mémorisent le type d’objet, ses bonus tirés et ses améliorations, pas son ancienne case. Deux exemplaires avec les mêmes propriétés sont interchangeables. Après une amélioration à la forge, enregistrer à nouveau la configuration.
- Une pièce manquante, un emplacement devenu invalide, une condition non remplie, un délai de récupération actif ou une combinaison arme à deux mains/bouclier invalide refuse **tout** le changement.
- Les cases vides mémorisées déséquipent les emplacements correspondants. Les objets restent dans l’inventaire.
- Un changement n’ajoute ni soins ni ressources. Les valeurs actuelles sont seulement limitées au nouveau maximum si celui-ci baisse.
- Un échec de sauvegarde restaure l’équipement et les points de vie/mana précédents. Une réponse réseau absente impose une actualisation avant une nouvelle modification dans la fenêtre.

## Intégration et persistance

Le serveur capture l’équipement réel lors de l’enregistrement ; le client transmet seulement l’action, le numéro, le nom et la révision. Les configurations utilisent le profil JSON existant du personnage (`PetsJson.EquipmentLoadouts`) et ne nécessitent pas de migration SQL. Les anciens profils démarrent avec cinq emplacements libres. La sauvegarde des configurations et celle des changements d’équipement passent par la sauvegarde persistante du compte, sous les verrous existants. Les événements d’équipement et les mises à jour aux joueurs proches sont exécutés après un changement sauvegardé.

Les messages, boutons et intitulés de raccourcis sont disponibles en français, anglais et espagnol. Les noms d’objets passent par le catalogue de traduction du contenu ; les noms choisis par le joueur restent inchangés.

Le client, le serveur et l’éditeur doivent être mis à jour ensemble pour prendre en charge les nouveaux paquets et les nouvelles propriétés. **Le profil, la branche privée et le client installé Intel HD 3000 restent séparés et ne sont pas modifiés par ce travail.**

## Sort d’arme dans Items Editor

Dans l’onglet **Sort de l’arme**, ouvrir **Configurer le sort de l’arme**. Choisir un sort existant et son activation :

- **Manuel** : bouton avec l’épée enchantée à gauche de la barre rapide, et raccourci **Sort de l’arme** dans **Paramètres > Commandes**. La cible sélectionnée est utilisée ; les sorts personnels ciblent le porteur. Le temps d’incantation du sort s’applique. Retirer ou modifier l’arme annule l’incantation.
- **Automatique à l’impact** : probabilité de 1 à 100 %, évaluée sur les impacts d’armes réussis. Le déclenchement est immédiat, même si le sort possède une incantation manuelle. Les projectiles conservent les propriétés de l’arme au départ du tir ; changer d’arme ensuite ne change pas leur effet.

Les coûts en vie/mana, conditions, portée, munitions et temps de recharge habituels sont vérifiés par le serveur. Les sorts ne sont pas ajoutés au livre du personnage. Leurs animations et sons restent ceux du **Spell Editor** ; l’interface ajoute son propre son court d’activation. Une arme ne reçoit qu’un sort lié à la fois. Le sort défini par une amélioration remplace celui de la définition générale pour cet exemplaire seulement.

## Amélioration dans Crafts Editor

1. Dans l’onglet de recette habituel, choisir comme résultat le **type d’équipement existant à améliorer**.
2. Saisir les **matériaux supplémentaires** dans les ingrédients, le temps de fabrication et les chances d’échec/perte de matériaux.
3. Ouvrir **Forge d’équipement > Configurer l’amélioration d’équipement**, puis activer l’amélioration d’un exemplaire existant.
4. Choisir une rareté cible, un sort pour une arme et/ou un événement commun. **Aucun / conserver l’existant** laisse la propriété actuelle intacte. Une recette doit apporter au moins une amélioration. Le résultat est fixé à un exemplaire ; ce mode exclut le résultat « feu de camp ».
5. Pour l’événement lié, choisir **utilisation**, **équipement**, **retrait**, **impact** ou **dégâts subis**. L’événement commun peut contenir les commandes voulues. Il est distinct de l’événement de recette, qui s’exécute après la fabrication réussie.

À l’atelier, le joueur choisit précisément une case d’inventaire dans la fenêtre **Forge d’équipement**, puis confirme. La fenêtre montre la rareté, les bonus et les effets au survol. Le serveur compare les propriétés montrées à celles de la case avant de démarrer, puis les vérifie à nouveau à la fin : un déplacement, une modification ou une recette changée refuse l’opération. Les améliorations sont réservées au hors combat.

Les bonus aléatoires existants restent identiques. La rareté change l’information de rareté, sa couleur et son éclat ; elle ne relance pas automatiquement les statistiques. La recette ne peut pas diminuer une rareté déjà supérieure. Les autres exemplaires et la définition générale de l’objet ne changent pas. Les matériaux n’incluent jamais l’exemplaire choisi ni un objet équipé. Les lignes d’ingrédients répétées sont additionnées. Un échec de fabrication conserve toujours l’objet choisi et applique seulement la règle de perte de matériaux. Un échec de sauvegarde restaure tout l’inventaire.

Les améliorations suivent l’objet dans les propriétés JSON déjà persistées des exemplaires et dans les paquets d’inventaire/banque/échange. Les anciennes propriétés sans amélioration et les anciennes recettes à ingrédients seuls restent lisibles. Aucune migration de schéma SQL n’est ajoutée.

## GFX et sons livrés

- Deux fonds originaux : `resources/gui/equipment_loadouts_v1.png` et `equipment_forge_v1.png` (1254 × 1254).
- Atlas transparent `equipment_icons_v1.png` (2 × 2) : équipement, forge, sort d’arme, événement. Les icônes des objets restent celles configurées dans le jeu.
- Neuf effets originaux courts, mono PCM16 WAV à 44,1 kHz : ouverture, sélection, enregistrement, équipement, suppression, erreur, début de forge, amélioration réussie et activation d’un sort. Noms `resources/sounds/equipment_<action>_v1.wav`.

Les sources sonores reproductibles sont dans `Utilities/EquipmentAssets/generate_audio.py`. Le manifeste `Documentation/Equipment-Assets.json` indique dimensions, durée, niveau et empreinte de chaque fichier. Les images ont été créées avec le générateur d’images intégré ; les prompts exacts sont conservés dans `Documentation/Equipment-ArtPrompts.json`. `Shared/EquipmentAssets.props` copie les ressources lors des builds/publications client et éditeur. Ces fichiers ne sont pas installés automatiquement dans les clients de Nicolas.

## Validation

La suite `EquipmentLoadoutTests` vérifie la persistance JSON par personnage, les profils antérieurs, le déplacement des objets dans l’inventaire, les bonus, les refus de combat terrestre/naval, les conditions d’équipement, les délais de récupération, l’absence de soins, le remplacement et la suppression, les échecs de sauvegarde, les traductions et la sérialisation des paquets.

`Utilities/EquipmentLoadoutUiTests` exerce la vraie fenêtre Gwen : enregistrement, confirmations, application, réponse manquante, défilement, fermeture et traductions aux résolutions 640×480, 1280×720 et 1920×1080. Il vérifie aussi les touches personnalisées, leur rechargement depuis les préférences et l’absence de répétition sur appui maintenu. Ses captures utilisent un moteur de rendu de test, pas une session connectée au jeu.

`EquipmentUpgradeTests` couvre les exemplaires distincts, matériaux, échecs/rollback, modifications pendant la fabrication, conservation des bonus, nouveaux sorts/événements, sauvegardes antérieures et transport réseau. `EquipmentPowerTests` couvre le lancement manuel, l’annulation, les coûts/recharges, les propriétés de l’exemplaire et l’absence de déclenchement récursif. Les tests d’éditeur `--equipment-only` vérifient le choix des effets sans modifier les données avant validation, dans les trois langues. La forge dispose aussi d’un test du choix d’exemplaire et de la copie de ses propriétés au clic.

Avant déploiement, compléter la recette sur une copie isolée : enregistrer deux configurations avec un personnage, changer par touche, se reconnecter après redémarrage du serveur, vérifier les statistiques et l’apparence depuis un second client, puis confirmer que l’autre personnage du même compte possède ses propres configurations. Cette recette réelle ne doit pas être confondue avec les tests automatisés.
