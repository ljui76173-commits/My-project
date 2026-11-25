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
| Sauter | Espace | bouton Sud (A / Croix) |
| Plan du Registre | P (1 : choisir le pont en bois) | gâchette haute gauche |
| Carte du chantier | Tab (glisser les portraits à la souris) | Start, croix directionnelle |
| Vue 1re / 3e personne | V | croix directionnelle haut |
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

## Musique

Dépose tes musiques (MP3, OGG ou WAV) dans `Assets/Aldermark/Audio`, puis relance
**Aldermark > Ajouter le gameplay du pont** : elles sont jouées à la suite, avec
20 secondes de silence entre deux morceaux. Les bruitages (vent, rivière, pas, Sceau,
voix des dialogues) sont fabriqués par le code : rien à installer.

## Personnages

Aldric : mets un fichier dont le nom contient `aldric` dans `Models` (un sous-dossier
avec ses textures convient), puis lance **Aldermark > Brancher le modèle d'Aldric**
(fait aussi par « Ajouter le gameplay du pont »). Le menu le met à 1,80 m, crée un
matériau mat avec les textures voisines (couleur + normale) et un Animator si le
fichier contient des animations (idle / walk / run). Touche V : troisième personne.

Lise, Odon, Bram : `lise.fbx`, `odon.fbx`, `bram.fbx`, branchés dans une prochaine mise à jour.

## Chantier (Boucle du pont, étapes 3 et 4)

Après avoir accepté de rebâtir le pont, P projette le **Plan du Registre** : un pont fantôme
bleu sur la rivière et la liste de ce qui manque (2 mana par minute). Il propose d'abord le
pont de pierre (6 jours, trop long), puis le pont provisoire en bois. Tab ouvre la carte du chantier : la vallée vue d'en haut, chaque poste avec sa recette, les flèches
de transport, les villageois en direct et un conseil d'Odon. On glisse un portrait sur un poste
(il y travaille) ou sur une flèche (il devient porteur).
Les villageois travaillent avec des gestes : hache à la forêt, pioche à la carrière et au talus,
marteau au chantier et au four, et portent leur charge à deux bras.
- **Postes** : Rivière (blocs, 31 au total, débloquée par l'indice des blocs), Carrière (blocs,
  loin), Forêt (poutres), Talus (calcaire, débloqué par l'indice du calcaire), Four, Chantier du pont.
- **Porteurs** : 3 m/s, 5 s de chargement, 3 blocs / 3 sacs / 4 poutres. Rien ne voyage sans eux.
- **Four** : 4 blocs + 1,5 jour-personne de réparation, puis 2 calcaire + 1 poutre → 2 sacs de chaux
  (8 sacs par personne et par jour). Au départ : 6 calcaire déjà extrait au talus, 4 poutres de bois mort au four.
- **Pont provisoire** : piles (8 blocs, 4 chaux), poutres maîtresses (16 poutres), tablier (20 poutres).
- Odon ne porte pas mais bâtit pour deux ; Bram arrive avec son indice ; Agathe, Colin et Firmin
  quand le four est relancé.
- Vue du Sceau (E) : une ligne ambrée par liaison active, une boule rouge sur un poste bloqué.

Raccourcis de test (dans l'éditeur seulement) : **F9** saute l'enquête et ouvre le chantier,
**F10** accélère le temps ×4, **F11** avance l'heure.

## Fin de la boucle du pont

- **Don de mana (G)** : vise un poste (ou choisis-le sur la carte) et donne 10 mana : l'équipe
  travaille deux fois plus vite pendant une demi-journée. Pendant le chantier, le mana remonte
  lentement : dormir le remplit.
- **Jour et nuit** : 3 minutes de jour, 1 minute de nuit. Les villageois rentrent le soir.
  La paillasse d'Odon (F) fait passer la nuit d'un coup.
- **Nuit des sonnailles** : une fois le four relancé, le soir, Odon donne 6 sonnailles.
  T pour les poser sur le chemin des saboteurs (le Sceau le montre, de chez Corvin au four).
  Réussi : trois hommes pris, dont Tobin, à interroger (choix moral). Raté : 10 poutres volées.
- **Village vivant** : marché, auberge, forge, chapelle, lavoir, moulin, moutons, poules ;
  les villageois vont aux champs, au marché, à l'auberge ; le village revit à chaque étape.
- **Fin** : le pont ouvert, le convoi du Comte traverse jusqu'au marché, titre « le Bâtisseur ».

## Équilibrage

Réglé avec une simulation de l'économie du chantier (mêmes règles que le jeu) :
bien organisé avec le don de mana, le pont tient en 4 jours environ après l'accord d'Odon,
5 à 6 jours sans mana, et le chantier se bloque si la chaîne de la chaux est oubliée.

## Prologue

Au lancement, le jeu commence par le prologue (rendu terne, sans pouvoirs) :
l'entrepôt à 6 h 22 (scanner les trois cartons avec F, puis sortir), la rue sous la pluie et le
camion, l'Entre-Deux avec Ysolde (orbes, Sceau, Genèse, pacte), puis le réveil dans la vallée,
quand le rendu peint s'allume. F12 passe le prologue ; F9 (éditeur) le saute aussi.
Les décors sont construits 600 m sous la vallée par « Ajouter le gameplay du pont ».

## Ilse et le nœud (scènes 10 et 11)

- Au matin qui suit la nuit des sonnailles, Lise annonce une femme en gris à l'auberge, et le
  **Voile du Registre (C)** se débloque : il fait tomber la Présence détectable (affichée sous le
  mana), coûte 1 mana par heure de jeu et ralentit la recharge.
- **Ilse** : son pendentif trouve le porteur si la Présence est haute, il hésite si le Voile tient.
  Selon le cas, le dialogue change. Elle révèle que Vesk arrive et qu'il puise dans le nœud.
- **Le nœud** sous les piles du vieux pont (visible au Sceau) : F maintenu pour l'absorber.
  Lâcher entre 1,6 et 2,2 fois le mana maximum : +10 de mana maximum pour toujours ; trop tard,
  rupture : −20 % jusqu'à l'aube. Le nœud s'affaiblit et le **Siphon (R maintenu)** s'éveille.
- Le rendu peint / brut passe sur F8 (R sert désormais au Siphon).

## La veillée d'armes et la bataille du Gué (fin de l'arc 1)

- Quelques secondes après l'arrivée du convoi, Ilse prévient : Vesk descend la rivière sur une barge
  noire avec douze Cendrés, il sera au Gué **à l'aube**. Garr, ancien sergent, lève une milice.
- **Les pièges** : au débarcadère (au nord des ponts, rive du village), le Sceau (E) montre la route
  de la barge (bleu), le chemin de ses hommes (or) et celui d'un fuyard (pâle). Le Plan propose
  14 emplacements pour 8 pièges : 1 chaîne, 3 filets lestés, 2 fosses à boue, 1 lot de
  chausse-trapes, 1 fil à cloches. **T** pour tracer ou effacer (3 mana).
  Les cloches : en amont, elles donnent l'alerte (moins de blessés) ; sur le sentier du bois, elles
  rattrapent le douzième Cendré. Il faut choisir.
- **La veillée** : dès 17 h, parle à Odon pour le discours au puits (le moral réduit les blessés).
  Ilse t'attend ensuite au bord du pont neuf (facultatif). Puis dors.
- **La bataille** : la barge bute sur la chaîne (sans chaîne, elle accoste et tout le monde débarque
  sur le quai). **Siphon (R maintenu) sur Vesk** : un bras de fer, il force par vagues. Une fois la
  vanne fermée, garde-la fermée : si tu lâches, sa main noire se rallume et brûle un filet ou la
  milice. Tu marches lentement tant que tu tiens. Ilse s'occupe de Vesk quand il saute à l'eau.
- **Le verdict** : l'épargner (et sceller son lien au mana) ou laisser Ilse le tuer. Bilan, titre
  « Vainqueur du Gué », fin de l'arc 1.
- Raccourci de test (éditeur) : **F7** saute à la veille de la bataille, F7 encore lance l'aube.
- Équilibrage : `Outils/simulation_bataille.py` (pièges parfaits : 1 Cendré passe ; sans chaîne : 5 ;
  sans rien : 11, la ligne plie).

## Le monde ouvert (mode RPG) — étape 1a

Le jeu repart en monde ouvert, sans scénario : un héros niveau 1 qui grandit sans plafond.
Le document de conception complet décrit toutes les étapes.

**Générer** : menu **Aldermark > Nouveau monde (RPG)**. La scène est enregistrée dans
`Assets/Scenes/Aldermark.unity` (l'ancienne vallée scénarisée reste dans `Varenne.unity`).

**Ce que contient l'étape 1a**

- Tu te réveilles dans ta maison, au fond d'un petit hameau à l'ouest de la rivière.
  Le lit (F) : dormir jusqu'au matin, se reposer, sauvegarder.
- **Horloge du monde** : 1 jour = 48 minutes, 4 saisons de 7 jours ; le soleil la suit.
- **Stats sans plafond** : FOR, AGI, END, INT, ESP, PER = (base + points + entraînement) × 1,09 par niveau.
  Elles montent en s'en servant (courir, sauter, explorer, dépenser du mana) et avec 3 points par niveau.
- **Vie, mana, endurance** en bas à gauche ; courir et sauter coûtent de l'endurance.
- **Statut (C)** : toutes les stats, d'où elles viennent, et la répartition des points (clic sur + ou 1 à 6).
- **Sauvegarde** du monde entier : automatique toutes les 5 minutes, en dormant et en quittant ;
  F5 pour sauvegarder, F9 pour recharger. La partie reprend toute seule au lancement.
- **Console de debug** (touche ² ou F1) : `aide`, `xp 500`, `niveau 50`, `points 10`, `stat FOR 10`,
  `heure 21`, `vitesse 20`, `tp varenne`, `tp hameau`, `soigner`, `sauver`, `charger`, `nouvelle`, `infos`.
- Les habitants du hameau et de Varenne ont un nom et répondent quand on leur parle.

## Le combat (mode RPG) — étape 1b

Pour tester sans monstres : la **quintaine** dans la cour à l'ouest du hameau (console : `tp quintaine`).
Frappe-la : son bras tourne et le sac de sable revient te cogner si tu restes dans sa course.

| Geste | Clavier / souris | Manette |
|---|---|---|
| Enchaînement de 3 coups (taille, revers, estoc) | clic gauche | RB |
| Coup lourd (repousse, sonne) | clic gauche maintenu | RB maintenu |
| Fente (avale 3 m) | clic gauche en courant | RB en courant |
| Coup plongeant (onde de choc) | clic gauche en l'air, d'assez haut | RB en l'air |
| Garde (réduit les dégâts de 75 %) | clic droit maintenu | LB |
| Parade parfaite (0 dégât, l'ennemi est sonné) | clic droit juste avant l'impact | LB juste avant |
| Riposte (critique ×3, au ralenti) | clic gauche juste après une parade parfaite | RB |
| Roulade (invulnérable un instant) | Alt | B |
| Se relever d'une roulade quand on est projeté | Alt juste avant de toucher le sol | B |
| Verrouiller la cible | clic molette | stick droit enfoncé |

- **Le recul** : un coup fort te repousse ; ton épée se plante dans le sol et tu glisses en arrière
  (étincelles, terre, sillon dans l'herbe). Plus l'ennemi est fort par rapport à ta défense, plus tu recules.
  Paré, un coup énorme te fait encore glisser. Sans garde, un coup très fort te **projette**.
- **Ressenti** : figeage d'image à chaque impact, tremblement de caméra, traînée de lame, chiffres de dégâts
  (blancs, dorés en critique, rouges sur toi), éclair blanc à la parade, bords rouges quand tu es touché.
- **Endurance** : chaque coup, roulade ou garde qui encaisse en coûte ; à zéro tu es essoufflé.
- **Entraînement** : frapper fait monter la FOR, esquiver et parer l'AGI, encaisser l'END.
- **Tomber à zéro** : tu te réveilles dans ton lit quatre heures plus tard, soigné.
- Les sons de combat sont fabriqués en code pour l'instant (de vrais enregistrements à l'étape 1e).

## Monstres et alliés (mode RPG) — étape 1c

Quatre repaires autour du hameau, de plus en plus dangereux en s'éloignant. Ils se repeuplent avec le temps,
jamais sous tes yeux.

| Repaire | Où | Niveau | Comportement |
|---|---|---|---|
| Gelées | au nord-est, vers la route (`tp gelees`) | 1–2 | avancent par bonds et se jettent sur toi ; une grosse se divise en deux |
| Tanière des loups | au nord (`tp loups`) | 3–4 | le premier hurle et appelle la meute ; un seul mord à la fois, les autres tournent autour ; bond de loin |
| Camp gobelin | au nord-est, après la route (`tp gobelins`) | 4–6 | gourdin, coup à deux mains ; s'enfuient blessés |
| Antre du troll | au sud, contre la montagne (`tp troll`) | 12 | massue qui projette, balayage large, piétinement en onde de choc — trop fort au début |

- **Attaques annoncées** : une lueur au-dessus de la tête grossit pendant la préparation. Jaune : coup normal.
  Rouge : coup lourd (il te fait glisser ou te projette). C'est le moment de parer ou de rouler.
- **Choc des lames** : si un gobelin ou le troll frappe en même temps que toi, face à face, les armes se bloquent.
  Martèle le clic gauche ; plus tu es fort par rapport à lui, plus chaque clic pèse. Gagné : il est sonné.
- **Coup de grâce** : un ennemi sonné pour de bon (parade parfaite, choc gagné, projeté contre un mur)
  montre des étoiles ; approche-toi, l'invite apparaît, un clic l'achève (dégâts ×7).
- **Contre le mur** : un ennemi repoussé contre un mur, un rocher ou une paroi prend un choc et reste sonné.
- **Un coup lourd ou critique** interrompt une attaque annoncée (sauf celles du troll).
- **Ralenti final** quand le dernier ennemi du combat tombe.
- **Le garde du hameau** patrouille et se bat contre les créatures qui approchent des maisons.
  Il ne meurt pas : mis à terre, il se relève.
- **Expérience** : seul le coup qui achève compte. Une gelée ≈ 12 XP, un loup ≈ 23, un gobelin ≈ 37, le troll ≈ 300.
- **Console** : `creer gelee|loup|gobelin|troll [niveau]` fait apparaître une créature devant toi ;
  `tuer` abat les créatures hostiles à moins de 40 m.

## Inventaire, butin et mains nues (mode RPG) — étape 1d

Tu te réveilles les mains vides. **Le coffre au pied du lit** (E) contient l'épée courte, deux pains et 5 pièces.

| Touche | Action |
|---|---|
| E | ramasser un objet au sol, ouvrir un coffre (les pièces se ramassent seules) |
| I | inventaire : survole un objet ; E ou clic droit équipe ou mange ; X jette (Maj+X : toute la pile) |
| H | ranger ou dégainer l'arme équipée |
| T | dans un coffre : tout prendre ; un clic fait passer une pile de l'autre côté |

- **Charge** : 20 kg + 2 par point de FOR. Surchargé, tu avances à 55 %. Arme rangée, tu cours un peu plus vite.
- **Mains nues** (arme rangée ou aucune) : direct du gauche, direct du droit, crochet ; maintenu, **coup de pied**
  qui repousse fort (parfait pour envoyer contre un mur) ; en courant, **charge à l'épaule** ; après une parade parfaite,
  **uppercut**. **Garde bras en croix** : encaisse la moitié des dégâts (75 % avec une lame) et coûte plus d'endurance ;
  la parade parfaite marche toujours. Pas de choc des lames sans lame.
- **Armes** : épée courte (rapide, précise), gourdin de gobelin (plus de dégâts, repousse plus loin, moins de critiques).
- **Butin** : gelée et parfois un noyau ; peau, crocs et viande de loup ; pièces, oreilles, pain et parfois un gourdin
  chez les gobelins ; sang et croc de troll avec une bourse bien garnie. Le butin brille d'un fin rayon pour le retrouver
  dans l'herbe. Chaque objet a un poids et une valeur estimée (la vente viendra avec les boutiques).
- **Manger** : le pain rend 20 % de la vie, la viande crue 8 %.
- **Sauvegarde** : le sac, la bourse, l'arme et le contenu du coffre sont sauvegardés. Les objets laissés au sol, non.
- **Console** : `objets` liste les objets, `donner gourdin_gobelin` en ajoute un.

## La carte (mode RPG)

**M** ouvre la grande carte, **N** affiche ou cache la mini-carte (ronde, sous l'horloge, elle tourne avec toi).

- **Peinte à partir du vrai terrain** au lancement (quelques secondes) : relief ombré, courbes de niveau tous les 5 m,
  rivière, routes, champs, forêts, bâtiments. Un **brouillard** couvre ce que tu n'as pas encore vu ; il se lève
  à 40 m autour de toi.
- **Légende à onglets** : Tout · Lieux · Commerces · Services · Dangers · Mes repères. Chaque type a son icône
  (chope : auberge, enclume : forge, bourse : marché, roue : moulin, croix : chapelle, goutte : lavoir, seau : puits,
  épée : quintaine, gelée, patte, tente, crâne pour les repaires). Clic sur une ligne : la carte s'y centre ;
  clic droit : itinéraire vers ce lieu.
- **Commandes** : molette pour zoomer (autour du curseur), glisser pour se déplacer, Espace pour te retrouver,
  clic pour poser ou retirer un repère, clic droit pour fixer une **destination**.
- **Itinéraire** : il suit les routes (et le pont) quand c'est plus court en pratique, sinon il coupe à travers champs.
  Il s'affiche sur la carte, la mini-carte et la **boussole** (drapeau et distance). Il s'efface à l'arrivée.
- **Lieux découverts** : les repaires et les lieux du bourg apparaissent quand tu passes près d'eux (+8 XP).
  Ta maison, ton hameau et Varenne sont connus d'emblée.
- **Varenne revit** en monde ouvert : marché garni, auberge, forge d'Hadrien, chapelle, lavoir, moulin qui tourne,
  enclos ; les villageois suivent l'horloge (champs, marché à midi, auberge le soir). On ne peut pas encore y acheter :
  les boutiques arrivent avec la vie du monde.
- **Sauvegarde** : le brouillard levé, les lieux découverts, tes repères et ta destination.
