# Contrôles CI déjà en échec avant l’ajout de la pêche

La base de cette intégration est `a925c1193916b4453b04b0dd071aa792f7589abd`, issue de la [PR 217](https://github.com/xw1ndsurfx/CR2026/pull/217).
Son contenu est identique au commit `1be670e3b70fc2cb2048d27815766b3d4fbe4420` testé par cette PR : les deux arbres Git valent `129b8b5341c1443af0ed72cce078decf7123a7bd`, et [la comparaison](https://github.com/xw1ndsurfx/CR2026/compare/1be670e3b70fc2cb2048d27815766b3d4fbe4420...a925c1193916b4453b04b0dd071aa792f7589abd) ne contient aucun fichier modifié.

Les logs de cette base établissent deux échecs antérieurs à Fishing :

| Contrôle | Échec observé | Preuve sur la base, avant Fishing |
| --- | --- | --- |
| Cinematic creators — creators | L’assertion de disposition des panneaux échoue à 800 × 550, dans `CinematicChapterEditorTests.ResponsiveChapterLayout`, ligne 257. | [Job 125667536593](https://github.com/xw1ndsurfx/CR2026/actions/runs/41559838789/job/125667536593), le 3 novembre 2026 à 01:30:47 UTC. |
| Editor save protection | `Sequence contains no elements` lors de la recherche du `TabControl` directement sous le formulaire PvP, dans `SaveProtectionTests.RoundTripForms`, ligne 83. | [Job 125667537388](https://github.com/xw1ndsurfx/CR2026/actions/runs/41559838955/job/125667537388), le 3 novembre 2026 à 01:36:50 UTC. |

Les mêmes erreurs sont présentes sur le deuxième commit Fishing `7f4bbc9c9d6cbeb8e05429154defa6eac47f65b9` : [Cinematic](https://github.com/xw1ndsurfx/CR2026/actions/runs/41574340271/job/125714256842) et [Editor save protection](https://github.com/xw1ndsurfx/CR2026/actions/runs/41574340229/job/125714257185). Les messages et emplacements correspondent à ceux de la base.

Ces deux contrôles n’ont pas été corrigés dans le lot Pêche. Leur présence explique qu’une validation Fishing réussie ne signifie pas que tous les workflows du dépôt sont verts. Consulter la [PR 218](https://github.com/xw1ndsurfx/CR2026/pull/218) pour le statut de la dernière révision.

Le cas `EndFishing` révélé par la suite Poker était, lui, une régression de cette intégration : la [validation Poker sur la base](https://github.com/xw1ndsurfx/CR2026/actions/runs/41559838833) réussissait. Son diagnostic et les tentatives Fishing sont conservés dans [le relevé Windows](windows-ci-attempts.json). Il ne doit pas être classé parmi les échecs préexistants.
