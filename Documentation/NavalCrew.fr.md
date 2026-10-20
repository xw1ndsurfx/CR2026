# Équipage naval et récompenses personnelles

## Règles de cette version
Un bateau personnel peut embarquer un capitaine et des équipiers. La capacité par modèle se règle dans le System Editor : 4 personnes par défaut, capitaine compris, entre 1 (solo) et 8. La taille maximale du groupe natif reste également applicable. Les bateaux publics de test et Herbonia ne se rejoignent pas comme équipage.

Les invitations utilisent le groupe existant du jeu. Le capitaine doit autoriser l’embarquement; l’équipier choisit ensuite son bateau depuis un quai Q. Les deux sont sur la même map Ocean, près du même bateau arrêté. Le serveur contrôle groupe, autorisation, distance, état et places disponibles. L’équipier n’a pas besoin d’acheter son propre bateau.

Le capitaine conserve la navigation, l’ancre et les bordées. Les équipiers suivent la coque et ses transitions Ocean; leur personnage à pied est masqué et ses déplacements/attaques ordinaires sont désactivés uniquement pendant l’embarquement. Ils disposent de l’équipage (7), de leur cale personnelle (8) et du débarquement (0). Les contrôles ordinaires reviennent à terre. Cette V1 ne propose pas encore de pont où marcher ni de canon individuel.

Au naufrage d’Herbonia, le bateau du tir fatal récompense son capitaine et ses équipiers présents. Chacun reçoit la récompense de base complète en Sailing et EXP personnage, avec les plafonds et bonus natifs propres au personnage. Le butin est tiré une fois puis copié pour chaque membre : mêmes objets, quantités, catégories et qualités, avec identifiants de lots indépendants. Les autres membres du groupe à terre ou dans un autre bateau ne sont pas récompensés.

Chaque cale est privée. Le même tirage peut être conservé différemment si les membres ont des places ou qualités déjà occupées différentes. Les remplacements et refus suivent les règles existantes de cale pleine. Chaque personnage décharge sa propre part au Q; le capitaine ne récupère pas la part des autres.

La cargaison encore embarquée est perdue si le navire coule, pour le capitaine et les équipiers. L’EXP déjà obtenue reste acquise. Au débarquement, votre part reste disponible au quai jusqu’au déchargement. Déchargez-la avant d’embarquer comme équipier sur un autre navire; cela évite d’écraser la cargaison d’une ancienne traversée.

## Configuration et utilisation
1. Installer ensemble serveur, client et éditeur de cette version sur le serveur de test, avec une sauvegarde préalable des bases. Aucun déploiement cloud n’est effectué par cette modification.
2. Dans System Editor → Boutique navale et réparations → modèle du bateau, régler **Capacité équipage (capitaine inclus)** et la capacité de cargaison. Enregistrer la section. L’icône du raccourci Équipage se choisit dans Sons, icônes et effets, depuis resources/spells, avec aperçu; un dessin natif est utilisé si elle est vide.
3. Inviter vos amis dans un groupe avec le système de groupe existant. À Q, le capitaine met son bateau personnel à l’eau, s’arrête près du quai et ouvre **7 → Autoriser l’embarquement**.
4. L’équipier resté à Q ouvre **/naval → Ma flotte → Équipage du navire**, ou saisit **/naval crew**. Le navire ouvert du groupe apparaît; cliquer **Embarquer**. Les noms du capitaine et de ses équipiers apparaissent dans la liste d’équipage.
5. Naviguer et combattre. Chaque équipier peut consulter sa propre cale avec 8. La caméra, la sélection et les barres suivent le bateau unique; les équipiers ne créent pas de coque supplémentaire.
6. Au retour à un Q, s’arrêter puis débarquer avec 0 ou le bouton d’équipage. Chaque personnage revient à sa propre case d’embarquement, avec les replis sûrs existants si elle est occupée/supprimée. Le capitaine décharge depuis Ma flotte; l’équipier utilise **Équipage du navire → Décharger ma part d’équipage**. Un inventaire principal plein conserve la cargaison.

## Déconnexion, arrêt et naufrage
Le point de retour et la cale personnelle de l’équipier sont sauvegardés avant son déplacement sur le bateau. Quitter, mourir ou être déplacé par le serveur libère son rôle; il ne peut pas continuer à contrôler la coque. Si le capitaine quitte la barre ou se déconnecte, les équipiers sont ramenés à leur point d’embarquement par leurs propres mises à jour, sans prendre le verrou d’un autre joueur dans la boucle navale.

Après un redémarrage, une traversée d’équipier encore marquée en mer est récupérée au point d’origine. Un compteur de naufrages persistant sur le bateau du capitaine permet de retirer le butin perdu même si le capitaine a déjà réparé son bateau avant la reconnexion de l’équipier. Les métadonnées sont dans le stockage naval JSON existant; aucune migration SQL n’est nécessaire. Une copie des bases reste nécessaire pour revenir à un ancien serveur qui ignore les nouveaux champs.

## Tests à effectuer avec deux clients
- Groupe formé, capacité 2 : embarquer un équipier; un troisième joueur ne doit pas pouvoir rejoindre. Refaire sans groupe et avec embarquement fermé : refus.
- Naviguer dans les huit directions et franchir une frontière Ocean réciproque. Les deux caméras suivent le même navire, sans avatar flottant ni deuxième bateau. L’équipier ne peut ni piloter, ni tirer une bordée, ni utiliser ses objets/sorts à pied.
- Configurer Herbonia à 275 EXP et un objet réel à 100 %, quantité 4. Le couler : chaque personnage présent reçoit la base +275 en Sailing/personnage et un lot de quantité 4 dans sa propre cale, une fois seulement. Un membre du groupe resté au quai ne reçoit rien.
- Remplir uniquement la cale d’un personnage : vérifier que son refus/remplacement n’affecte pas l’autre cale. Décharger les deux parts au Q avec deux inventaires distincts, puis réessayer : aucun doublon.
- Débarquer depuis un autre Q : chacun revient à sa propre case d’embarquement. Déconnecter un équipier puis le capitaine en mer et se reconnecter : aucun joueur bloqué dans l’eau, récompenses sauvegardées conservées.
- Laisser couler le bateau avec l’équipier à bord : les deux cargaisons sont perdues, EXP conservée, retours sûrs. Tester une réparation du capitaine avant la reconnexion de l’équipier.

## Validation et limites
Tests automatisés : règles d’embarquement, groupe/consentement/arrêt/distance/capacité, tirage commun et récompense unique des membres présents, sauvegarde/retour arrière de la traversée et des gains, réconciliation après naufrage/réparation, compatibilité réseau, suivi de sélection/caméra des passagers, contrôles de fenêtre et localisation en/fr/es. Les contrôles UI utilisent les vrais widgets avec des mesures synthétiques; le rendu connecté et le parcours à deux clients ci-dessus restent à confirmer dans l’installation.

La présence de l’équipier doit être encore à jour dans la boucle serveur au moment du naufrage. Les files de récompenses avant leur première sauvegarde restent en mémoire comme auparavant. Les postes de canon, la marche sur un pont et les rôles spécialisés constituent une étape suivante, avec des points de raccord dans l’appartenance d’équipage et les contrôles serveur.

La vue d’observation au canon est décrite dans [NavalCannonView.fr.md](NavalCannonView.fr.md). Elle conserve les bordées au capitaine ; elle ne crée pas encore de poste de canon individuel.
