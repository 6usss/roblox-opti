# Roblox Instance Optimizer

Outil Windows WPF pour piloter plusieurs instances Roblox.

## Fonctionnalites MVP

- Detection des processus `RobloxPlayerBeta.exe`
- Affichage PID, nom de fenetre, RAM actuelle et affinite CPU
- Reglage de priorite CPU
- Allocation d'un nombre de coeurs logiques par instance
- Nettoyage RAM automatique type Mem Reduct via `EmptyWorkingSet`
- Seuil RAM par instance: quand Roblox depasse le seuil, l'app demande a Windows de reduire sa memoire active
- Parametres par defaut sauvegardes et appliques automatiquement aux nouvelles instances
- Repartition automatique des coeurs CPU entre les instances Roblox

## Utilisation

1. Lance Roblox.
2. Lance `RobloxInstanceOptimizer.App` en administrateur.
3. Clique sur `Scanner`.
4. Regle le seuil de nettoyage RAM en Mo, le nombre de coeurs et la priorite.
5. Clique sur `Appliquer selection` ou `Appliquer tout`.

## Parametres par defaut

La zone `Defaut` permet de choisir les valeurs appliquees aux nouvelles instances Roblox:

- seuil de nettoyage RAM
- nombre de coeurs
- priorite CPU
- auto-scan
- nettoyage RAM auto
- application automatique aux nouvelles instances

Clique sur `Sauvegarder defauts` pour les garder au prochain lancement. La config est stockee dans `%AppData%\RobloxInstanceOptimizer\settings.json`.

## Repartition CPU automatique

Si `Repartir coeurs auto` est coche, l'app attribue une plage differente a chaque instance.

Exemple avec 36 coeurs et `Coeurs = 2`:

- instance 1: coeurs 0-1
- instance 2: coeurs 2-3
- instance 3: coeurs 4-5
- etc.

Quand toutes les plages disponibles sont utilisees, l'app boucle au debut.

Valeurs de depart conseillees:

- Compte principal: 2048 a 3072 Mo, 2 a 4 coeurs, priorite `Normal`
- Compte secondaire leger: 1536 a 2048 Mo, 1 a 2 coeurs, priorite `BelowNormal`
- Evite de descendre sous 1536 Mo sauf si le jeu est tres leger

## Note importante sur la RAM

Le mode par defaut ne bloque plus la RAM. Il utilise `EmptyWorkingSet`, une approche proche de Mem Reduct.

Quand Roblox depasse le seuil choisi, l'app demande a Windows de vider une partie de la memoire active du processus. C'est plus stable qu'un hard limit, mais ce n'est pas une limite absolue: Roblox peut reprendre de la RAM ensuite, donc l'app recommence automatiquement.

Un vrai hard limit via Job Object peut faire crash Roblox si le jeu a besoin de plus de memoire. Ce mode est garde dans le code pour experimentation future, mais l'interface utilise maintenant le nettoyage RAM auto.
