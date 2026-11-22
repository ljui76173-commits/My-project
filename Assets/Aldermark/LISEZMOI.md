# Aldermark — socle Unity

Prototype de la vallée de Varenne (arc 1), rendu peint en pixel art.

## Lancer

1. Ouvrir le projet dans Unity 6 (6000.3).
2. Menu **Aldermark > Générer la vallée de Varenne** (1 à 2 minutes).
3. La scène `Assets/Scenes/Varenne.unity` s'ouvre : appuyer sur **Play**, cliquer dans la vue Game pour capturer la souris.

Relancer le menu régénère la scène et écrase `Assets/Aldermark/Genere`.
Les retouches faites à la main dans la scène sont alors perdues.

## Commandes

| Action | Clavier | Manette |
| --- | --- | --- |
| Marcher | ZQSD ou flèches | stick gauche |
| Courir | Maj gauche | clic stick gauche |
| Regarder | souris | stick droit |
| Sceau du Grand Registre | E | bouton Nord (Y / Triangle) |
| Rendu brut / peint | R | Select |
| Libérer la souris | Échap | — |

## Fichiers

| Fichier | Rôle |
| --- | --- |
| `Scripts/Monde.cs` | Relief, rivière, routes, champs : la géographie partagée |
| `Scripts/RenduPeint.cs` + `Shaders/PeintPixel.shader` | Image basse résolution, postérisation, tramage, rayons, superposition du Sceau |
| `Shaders/CielAldermark.shader` | Ciel, deux soleils, nuages |
| `Scripts/SoleilNuages.cs` | Nuages devant le soleil (la lumière baisse), ombres de nuages au sol |
| `Scripts/JoueurAldric.cs` | Déplacement, regard, corps, bâton, marque d'argent, mana |
| `Scripts/HUD.cs` | Boussole, mana, fenêtre Système, étiquettes du Sceau |
| `Scripts/Habitant.cs`, `Scripts/Oiseaux.cs` | Villageois, Lise, charretiers, oiseaux |
| `Editor/GenerateurVallee.cs` | Construit toute la scène |

## Réglages utiles

- `RenduPeint` (sur la caméra d'Aldric) : `resolutionLongue` = taille des pixels (plus petit = pixels plus gros).
- Matériau `Genere/Materiaux/PeintPixel` : `_Niveaux` (nombre de teintes), `_Tramage`.
- Le terrain se sculpte et se peint avec les outils Terrain d'Unity (herbe, arbres, couleurs du sol).

## Modèles importés (Meshy, packs…)

Dépose tes modèles (FBX de préférence) dans `Assets/Aldermark/Models`.
Le générateur les reconnaît à leur nom de fichier et remplace les formes de base :

| Objet | Mots reconnus dans le nom du fichier |
| --- | --- |
| Four à chaux | `four`, `kiln`, `chaux` |
| Puits | `puits`, `well` |
| Maisons du village (autant de modèles que tu veux, utilisés à tour de rôle) | `maison`, `house` |

Le modèle est mis automatiquement à la bonne taille, posé au sol et reçoit des collisions.
La console indique quel fichier a été utilisé. Relance le générateur après chaque ajout.
