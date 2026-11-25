# État des imports

Animations Import et Sound Import affichent l’état de la ressource sélectionnée dans l’aperçu. **Déjà importé** indique le nom de sa copie présente dans le jeu, même après un renommage. La vérification se fait en arrière-plan et s’annule quand la sélection change.

Les nouveaux imports enregistrent une empreinte du fichier source et de sa destination dans le cache de l’éditeur. Cela permet de reconnaître aussi les sons convertis, après fermeture puis réouverture de l’éditeur. Un fichier supprimé ou remplacé n’est plus considéré comme une copie valide. Les destinations sons et voix sont indépendantes.

Les copies identiques antérieures à cette fonctionnalité sont détectées par leur contenu. Pour les anciens sons qui ont subi une conversion modifiant leur contenu et dont l’historique n’existe pas, l’éditeur peut afficher **Aucune copie correspondante détectée dans le jeu**. Ce message ne garantit pas que le son n’a jamais été importé.

Le défilement naval utilise un panneau défilant autour d’une grille à hauteur naturelle. Les champs gardent leur hauteur, les aperçus d’icônes ne recouvrent plus les lignes suivantes et le dernier paramètre reste accessible dans une petite fenêtre.
