# Personnage, maîtrises et accueil de l'éditeur

## Maîtrise d'arme et prérequis

Dans l'éditeur de maîtrise des armes, associer les objets d'armes à leur catégorie
(par exemple les épées à la catégorie d'escrime). Une élimination avec une arme
associée donne à cette catégorie la même récompense d'XP que le personnage, bonus
compris. Le gain respecte le plafond de maîtrise et affiche la barre d'activité
pendant cinq secondes, comme pour les métiers de récolte. La maîtrise continue
de progresser lorsque le personnage a atteint son niveau maximal.

Dans une branche conditionnelle d'événement ou les conditions d'utilisation d'un
objet, choisir **Niveau de maîtrise d'arme**, puis la catégorie, la comparaison
et le niveau. Pour exiger le niveau 10 en épée, sélectionner la catégorie des
épées, `>=` et `10`. Les conditions d'utilisation s'appliquent également à
l'équipement effectué directement par un événement. Une catégorie supprimée ne
valide jamais son ancien prérequis.

## Personnage et joute

Character Information conserve les dimensions de son fond d'origine. Les sept
actions sont regroupées en trois rangées lisibles : détails, succès, familiers ;
montures, joute, tir à l'arc ; puis maîtrises des armes.

La profession Joute reprend la carrière déjà sauvegardée du personnage et sa
progression native jusqu'au niveau 25. Elle ne crée pas une seconde réserve d'XP.

## Fenêtres de l'éditeur et feux de camp

Les espaces de travail historiques révisés suivent désormais la taille de leur
fenêtre, avec navigation et boutons de sauvegarde accessibles. Les dispositions
déjà spécifiques aux objets, PNJ et sorts restent gérées par leurs éditeurs.

Dans l'éditeur de tables de fabrication, **Utiliser cette table pour créer les
feux de camp** reste cochable même sans recette existante. Configurer ensuite
une recette de feu de camp valide dans l'éditeur de recettes : les tables
marquées l'incluent automatiquement. Les conditions de la recette restent
contrôlées par le serveur. Les anciennes tables restent lisibles.

## Hub

L'accueil affiche **Derniers modules ajoutés**, puis les éditeurs principaux.
Le filtre **Ajouts récents** donne accès au Smart Map Builder, aux maîtrises
d'armes, au tir à l'arc, aux joutes, aux professions, aux tables de fabrication,
au système naval et au Royal Cinematic Director. Les cartes utilisent les
commandes existantes de l'éditeur.

Cette mise à jour demande les versions correspondantes du serveur, du client et
de l'éditeur. Les tests automatisés ne remplacent pas une vérification en jeu
avec les données et les associations d'armes du projet.
