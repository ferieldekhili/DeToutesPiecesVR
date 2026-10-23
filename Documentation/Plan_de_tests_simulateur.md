# Plan de tests au XR Interaction Simulator — De Toutes Pièces VR

Objectif : vérifier sur ordinateur, sans casque, qu'une session complète fonctionne
(accueil → prise en main → inventaire → montage en 6 étapes → bilan) avant les essais sur Meta Quest.

## 1. Préparation

1. Ouvrir le projet dans Unity 6.5 puis la scène `Assets/_DeToutesPieces/Partie1_Noyau_Feriel/Scenes/Atelier.unity`.
2. Vérifier que la console ne contient aucune erreur (rouge).
3. Lancer **Play**, puis **cliquer une fois dans la vue Game** pour lui donner le clavier.
4. Pour repartir comme un premier utilisateur : appuyer sur **F8** (efface réglages et progression, relance la scène).

Aucun casque n'est nécessaire : le simulateur crée un casque et deux manettes virtuels.
OpenXR n'est pas démarré sur ordinateur (il l'est pour Android / Quest).

## 2. Commandes du simulateur

Le simulateur lit la **position physique** des touches (clavier américain). Sur un Mac **AZERTY** :

| Action | AZERTY | QWERTY |
|---|---|---|
| Contrôler la main droite / gauche / les deux | `$` / `^` / `^`+`$` | `]` / `[` / `[`+`]` |
| Contrôler la tête (regarder) | `H` | `H` |
| Avancer / reculer | `Z` / `S` | `W` / `S` |
| Gauche / droite | `Q` / `D` | `A` / `D` |
| Descendre / monter | `A` / `E` | `Q` / `E` |
| Tourner la main tenue | flèches, ou clic droit maintenu + souris | idem |
| Tourner autour de l'axe (serrage à la clé) | molette (ou deux doigts sur le trackpad) | idem |
| Saisir (gâchette latérale) | **maintenir `G`** | `G` |
| Pointer et cliquer (boutons, saisie à distance) | souris + clic gauche | idem |
| Boutons de la main gauche | `Maj` + touche | `Maj` + touche |
| Remettre les manettes en place | `R` | `R` |

Commandes de l'application :

| Action | Manette | Clavier |
|---|---|---|
| Indice (texte → surbrillance → démonstration) | A (`1`) | `F` |
| Annuler la dernière action | B (`2`) | `U` |
| Étape suivante / Continuer / Passer | bouton du panneau | `Entrée` |
| Fenêtre d'outils | X (`Maj`+`1`) | `O` |
| Pause et réglages | Menu (`Maj`+`M`) | `Échap` |

Raccourcis de test (**éditeur uniquement**, absents du casque) :

| Touche | Effet |
|---|---|
| `F5` | réalise automatiquement l'opération désignée par la flèche |
| `F6` | termine automatiquement l'étape ou la phase en cours |
| `F8` | efface réglages et progression, puis relance la scène |

## 3. Scénarios de test

Cocher chaque ligne ; noter toute anomalie dans le tableau de la section 5.

### T1 — Accueil
- [ ] L'écran d'accueil apparaît devant soi ; le panneau de gauche affiche « Bienvenue ».
- [ ] Choisir **Gauche** puis **Droite** : le bouton choisi devient jaune.
- [ ] Choisir **Assis** : l'établi descend ; **Debout** : il remonte.
- [ ] Au premier lancement, « Commencer sans prise en main » et « Reprendre » sont grisés.
- [ ] Cliquer **Commencer avec la prise en main** : l'écran disparaît.

### T2 — Prise en main (zone d'exercice orange sur le support)
- [ ] Saisir le cube (viser + `G` maintenu) et le poser sans le tourner : message rouge « mauvaise orientation ».
- [ ] Le tourner d'un quart de tour (flèches) jusqu'à ce que la silhouette devienne **verte**, relâcher : il s'aligne.
- [ ] Prendre la vis d'exercice (coupelle jaune) et la lâcher près de l'anneau jaune : « Vis engagée ».
- [ ] Prendre la clé Allen, approcher l'embout court de la vis dans son axe : la clé s'emboîte, la flèche circulaire tourne.
- [ ] Molette dans le sens de la flèche : l'arc vert progresse, « Vis serrée ». Molette en sens inverse : message « Sens inverse ».
- [ ] Le panneau affiche « Prise en main terminée » ; **Continuer** passe à l'inventaire.

### T3 — Inventaire
- [ ] Une étiquette bleue flotte au-dessus de chaque type : Tablette, Pied ×4, Plateau, Vis ×16, Clé Allen.
- [ ] Saisir un élément de chaque type : son étiquette passe au vert, le compteur avance (x/5).
- [ ] **Continuer** : les pièces déplacées retournent à leur place, le montage commence (étape 1/6).

### T4 — Montage, étapes 1 à 5 (vis engagées, non serrées)
- [ ] La flèche jaune désigne la prochaine opération ; seules les silhouettes de l'étape sont visibles.
- [ ] Un pied à l'envers ou mal tourné est refusé avec un message.
- [ ] Une vis lâchée près d'un trou avant que le pied soit posé ne s'engage pas.
- [ ] Une pièce lâchée hors de portée revient à sa place (« Pièce remise à sa place »).
- [ ] « Étape suivante » reste grisé tant que l'étape n'est pas terminée.
- [ ] Aux étapes 3 et 5, le support tourne pour présenter l'autre côté.
- [ ] La clé ne s'emboîte pas avant l'étape 6 (message « Pas encore »).
- [ ] `B` / `U` annule la dernière action (la pièce ou la vis revient).

### T5 — Aides
- [ ] Indice 1 fois : texte dans le panneau ; 2 fois : l'élément à utiliser clignote en jaune ; 3 fois : une copie translucide montre le geste.
- [ ] Fenêtre d'outils (`O`) : elle se déroule ; l'élément utile est surligné ; « Prendre » le fait flotter devant soi.
- [ ] Notice virtuelle (tableau de droite) : maquette de l'étape, « Précédente / Suivante » en aperçu, « En cours » pour revenir.

### T6 — Étape 6 et bilan
- [ ] Serrer les vis à la clé (ou `F5` pour avancer) ; le support tourne vers les vis de l'arrière.
- [ ] Regarder la face avant du meuble 2 s (`H` pour bouger la tête) : contrôle d'équerre validé.
- [ ] **Terminer** : l'écran de bilan affiche temps, erreurs, aides, sans note ; rappel « 10 kg, ne pas s'asseoir ».
- [ ] **Recommencer** relance la session à l'accueil.

### T7 — Pause et réglages
- [ ] `Échap` ouvre le menu pause ; le temps actif s'arrête.
- [ ] Volume –/+ : les sons baissent / montent ; Vibrations : Activées / Désactivées.
- [ ] Taille des textes +/– : les panneaux s'agrandissent / rétrécissent.
- [ ] Changer la main dominante : A/B passent sur l'autre manette.
- [ ] **Reprendre** ferme le menu.

### T8 — Reprise d'une session interrompue
- [ ] Terminer au moins l'étape 3, puis arrêter Play.
- [ ] Relancer Play : « Reprendre à l'étape 4 » est actif à l'accueil.
- [ ] Cliquer dessus : les étapes 1 à 3 sont reconstituées (5 pièces posées, 8 vis engagées), message « Reprise au dernier jalon ».

## 4. Limites connues à observer

- Les modèles 3D sont schématiques (dimensions estimées, la notice ne donnant pas de cotes).
- Les tolérances (alignement 6 cm / 20°, embout 3,5 cm / 40°, serrage 270°) sont des valeurs de départ, à régler sur casque.
- Au clavier, orienter un pied est moins précis qu'avec une manette : noter si c'est trop difficile.
- Les sons sont générés par code (provisoires).

## 5. Relevé des anomalies

| N° | Test | Ce qui s'est passé | Attendu | Gravité (bloquant / gênant / mineur) |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

## Mode clavier-souris (touche B ou F2)

La touche **B** (ou **F2**) bascule entre le mode VR (casque ou XR Interaction Simulator) et le mode
clavier-souris. Dans une version compilée sans casque actif, l'application démarre directement en
clavier-souris. **H** affiche ou masque l'aide des commandes.

| Action | Commande |
|---|---|
| Regarder | clic droit maintenu + souris |
| Se déplacer | ZQSD (WASD en QWERTY) ou flèches ; A / E pour descendre / monter ; Maj pour aller plus vite |
| Saisir / poser une pièce | clic gauche maintenu ; relâcher pour poser (mêmes validations qu'en VR) |
| Rapprocher / éloigner la pièce | molette |
| Tourner la pièce | R / T / Y (quart de tour), Ctrl + souris (rotation libre), Ctrl + molette (roulis) |
| Serrer à la clé Allen | tenir la clé, viser une vis engagée (la clé s'emboîte), molette vers l'avant = sens horaire |
| Boutons des panneaux | clic gauche |
| Raccourcis | F indice, U annuler, Entrée suivant, O fenêtre d'outils, Échap pause |

Tests à réaliser :
1. Appuyer sur B à l'accueil : l'écran d'accueil revient devant la caméra, l'aide s'affiche en bas à gauche.
2. Cliquer sur « Commencer avec la prise en main », saisir le cube d'exercice et le poser sur sa silhouette
   (refus si l'orientation est mauvaise ; R / T / Y pour le tourner).
3. Étapes 1 à 5 : poser pieds, tablette et plateau sur les silhouettes (elles passent au vert quand la pose est bonne),
   puis glisser les vis du bac vers les repères jaunes.
4. Étape 6 : saisir la clé Allen, viser chaque vis, tourner la molette vers l'avant ; tourner vers l'arrière doit
   déclencher l'avertissement « sens inverse ».
5. Appuyer de nouveau sur B : retour en VR, le simulateur reprend la main.
