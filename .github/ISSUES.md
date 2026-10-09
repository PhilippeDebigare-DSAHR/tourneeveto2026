# TournéeVéto — backlog du POC

Source de référence : [périmètre du MVP](docs/mvp.md).
Contexte : [contexte produit](PRODUCT.md).

Ce document contient les titres, étiquettes et descriptions des issues à créer.
Il ne signifie pas que des issues ont déjà été créées dans GitHub.
Les identifiants E01 à E06 et S01 à S12 sont des références locales, pas des numéros GitHub.

## Cadre et règles du backlog

- Horizon : POC de cinq jours. Les six fonctionnalités **Must** sont couvertes ; aucune fonctionnalité Should, Could ou Won't n'est ajoutée.
- Persona des stories : vétérinaire utilisant une tablette, avec une interface française.
- Architecture : Blazor WebAssembly statique sur GitHub Pages, sans backend, API applicative ni compte ; persistance locale dans IndexedDB.
- Données exclusivement fictives ; règles fixes et simplifiées, sans valeur de prescription ni de registre officiel.
- Prérequis des tests hors ligne : l'application et le jeu de démonstration ont été chargés une première fois en ligne sur la tablette. Le premier démarrage d'un appareil vierge sans réseau est exclu.
- Le stockage local n'est ni une sauvegarde distante ni une synchronisation. Aucun mécanisme de récupération sur un autre appareil n'est promis.
- Chaque story porte `story` et l'étiquette spécifique de son epic. Chaque epic porte `epic` et cette même étiquette spécifique.
- Chaque story possède trois scénarios d'acceptation, dont un scénario d'erreur explicite.
- Les erreurs techniques sont provoquées par le dispositif de test ; aucun écran d'administration ou outil d'injection d'erreurs n'est à développer.

## Vue d'ensemble

| Epic | Fonctionnalité Must | Étiquette spécifique | Stories |
|---|---|---|---|
| E01 | Ouvrir l'application statique et ses données de démonstration préchargées | `epic:demarrage` | S01, S02 |
| E02 | Consulter la tournée du jour et la fiche d'un élevage | `epic:tournee` | S03, S04 |
| E03 | Parcourir la grille de régie, marquer une action et ajouter une note | `epic:regie` | S05, S06 |
| E04 | Remplir le bilan de biosécurité de la visite | `epic:biosecurite` | S07, S08 |
| E05 | Réaliser et enregistrer une visite complète hors ligne dans IndexedDB | `epic:hors-ligne` | S09, S10 |
| E06 | Générer un rapport imprimable et exportable en PDF par le navigateur | `epic:rapport` | S11, S12 |

## E01 — Ouvrir l'application statique et ses données de démonstration préchargées

**Étiquettes :** `epic`, `epic:demarrage`  
**Priorité :** Must  
**Objectif :** accéder au POC publié sur GitHub Pages et disposer de données fictives sans service applicatif.

**Succès de l'epic :** l'URL publiée ouvre l'application et affiche le jeu de démonstration, sans requête à une API ni à un serveur applicatif.

### S01 — Accéder au POC publié sur GitHub Pages

**Étiquettes :** `story`, `epic:demarrage`  
**User story :** En tant que vétérinaire, je veux ouvrir l'application depuis son URL GitHub Pages sur ma tablette afin de commencer ma tournée sans installation native ni compte.

**Critères d'acceptation :**

```gherkin
Scénario : Ouvrir l'application à son adresse publiée
Étant donné que le POC est publié sur GitHub Pages et que ma tablette est en ligne
Quand j'ouvre son URL, y compris le sous-chemin du dépôt s'il existe
Alors l'écran d'entrée de l'application s'affiche en français sans erreur de chargement des ressources.

Scénario : Démarrer sans service applicatif
Étant donné que j'accède à l'application publiée
Quand l'application démarre et affiche son écran d'entrée
Alors aucun compte n'est demandé et aucune requête à une API ou à un serveur applicatif n'est effectuée.

Scénario : Signaler un échec de chargement des données au démarrage
Étant donné que l'interface a été chargée mais que les données nécessaires au démarrage sont illisibles
Quand l'application tente d'ouvrir le parcours de tournée
Alors un message en français signale l'échec et aucune tournée vide n'est présentée comme un chargement réussi.
```

### S02 — Disposer du jeu de démonstration préchargé

**Étiquettes :** `story`, `epic:demarrage`  
**User story :** En tant que vétérinaire, je veux disposer d'un jeu de données fictives préchargé afin de démontrer le parcours de visite sans importer de données réelles.

**Critères d'acceptation :**

```gherkin
Scénario : Initialiser les données de démonstration
Étant donné que l'application est ouverte pour la première fois en ligne et que son stockage local est disponible
Quand le chargement initial se termine
Alors les élevages, vaches, visites précédentes et actions du jeu de démonstration sont disponibles localement et leur caractère fictif est indiqué.

Scénario : Conserver les saisies lors d'une nouvelle ouverture
Étant donné que le jeu de démonstration est déjà chargé et qu'une visite modifiée est enregistrée localement
Quand je ferme puis rouvre l'application
Alors les données de démonstration restent disponibles sans doublons et la visite modifiée n'est pas remplacée par sa version initiale.

Scénario : Signaler un échec d'initialisation locale
Étant donné qu'IndexedDB est indisponible lors de la première initialisation
Quand l'application tente d'enregistrer le jeu de démonstration
Alors un message en français indique que les données n'ont pas été enregistrées localement et ne les présente pas comme prêtes pour un usage hors ligne.
```

## E02 — Consulter la tournée du jour et la fiche d'un élevage

**Étiquettes :** `epic`, `epic:tournee`  
**Priorité :** Must  
**Objectif :** préparer la visite avec les élevages prévus, leur effectif, leur historique et leurs suivis.

**Succès de l'epic :** la tournée présente les élevages prévus ; une fiche de référence affiche l'effectif, au moins une visite précédente et les actions en suspens associées.

### S03 — Consulter les élevages de la tournée du jour

**Étiquettes :** `story`, `epic:tournee`  
**User story :** En tant que vétérinaire, je veux consulter les élevages prévus dans ma tournée du jour afin de préparer les visites à réaliser.

**Critères d'acceptation :**

```gherkin
Scénario : Afficher la tournée de référence
Étant donné que le jeu de démonstration contient des visites prévues pour la date de tournée testée
Quand j'ouvre la tournée de ce jour
Alors tous les élevages prévus pour cette date sont identifiables et aucun élevage prévu uniquement à une autre date n'est inclus.

Scénario : Ouvrir l'élevage sélectionné
Étant donné que plusieurs élevages figurent dans la tournée du jour
Quand je sélectionne un élevage
Alors la fiche ouverte correspond à cet élevage et non à un autre élevage de la tournée.

Scénario : Signaler une tournée illisible
Étant donné que les données locales de la tournée sont illisibles
Quand j'ouvre la tournée du jour
Alors un message en français indique l'impossibilité de charger la tournée et ne prétend pas qu'aucune visite n'est prévue.
```

### S04 — Consulter le contexte et les suivis d'un élevage

**Étiquettes :** `story`, `epic:tournee`  
**User story :** En tant que vétérinaire, je veux consulter l'effectif, les visites précédentes et les actions en suspens d'un élevage afin de préparer mon intervention et de retrouver les suivis attendus.

**Critères d'acceptation :**

```gherkin
Scénario : Afficher une fiche complète de référence
Étant donné qu'un élevage de démonstration possède un effectif, au moins une visite précédente et des actions en suspens
Quand j'ouvre sa fiche
Alors ces trois informations s'affichent et correspondent exactement aux données de cet élevage.

Scénario : Distinguer les données de deux élevages
Étant donné que deux élevages possèdent des historiques et des actions distincts
Quand j'ouvre successivement leurs fiches
Alors chaque fiche affiche uniquement l'historique et les actions associés à l'élevage sélectionné.

Scénario : Signaler une fiche introuvable
Étant donné que l'élevage référencé par une entrée de tournée est absent du stockage local
Quand je tente d'ouvrir sa fiche
Alors un message en français indique que la fiche est introuvable et aucune fiche d'un autre élevage n'est affichée à sa place.
```

## E03 — Parcourir la grille de régie, marquer une action et ajouter une note

**Étiquettes :** `epic`, `epic:regie`  
**Priorité :** Must  
**Objectif :** consigner les actions et notes pour la bonne vache pendant la visite.

**Succès de l'epic :** les cinq catégories sont visibles ; une action cochée et une note réapparaissent pour la bonne vache après fermeture et réouverture de la visite.

### S05 — Parcourir les vaches à traiter par catégorie de régie

**Étiquettes :** `story`, `epic:regie`  
**User story :** En tant que vétérinaire, je veux parcourir les vaches à traiter dans les catégories de régie afin de repérer les actions prévues pendant la visite.

**Critères d'acceptation :**

```gherkin
Scénario : Présenter les cinq catégories
Étant donné qu'une visite de référence est ouverte
Quand j'affiche la grille de régie
Alors les catégories vêlage, tarissement, insémination, diagnostic de gestation et CCS élevée sont toutes visibles.

Scénario : Associer chaque action à la bonne vache
Étant donné que le jeu de référence contient des vaches à traiter dans chacune des cinq catégories
Quand je parcours la grille
Alors chaque action attendue apparaît dans sa catégorie avec l'identifiant de la bonne vache et sans vache d'un autre élevage.

Scénario : Signaler une grille impossible à charger
Étant donné que les données nécessaires à la grille d'une visite sont illisibles
Quand j'ouvre cette grille
Alors un message en français indique l'échec du chargement et aucune liste vide n'est présentée comme une absence d'actions à traiter.
```

### S06 — Enregistrer une action réalisée et une note pour une vache

**Étiquettes :** `story`, `epic:regie`  
**User story :** En tant que vétérinaire, je veux marquer une action réalisée et ajouter une note pour une vache afin de conserver une trace fidèle de mon intervention.

**Critères d'acceptation :**

```gherkin
Scénario : Enregistrer et retrouver une saisie
Étant donné qu'une action non réalisée appartient à une vache de la visite ouverte
Quand je marque cette action comme réalisée, saisis une note, enregistre puis ferme et rouvre la visite
Alors l'action reste réalisée et la note est restituée à l'identique pour cette vache et cette visite.

Scénario : Ne pas modifier les autres vaches
Étant donné que deux vaches de la visite possèdent chacune une action et une note distinctes
Quand je modifie et enregistre l'action et la note de la première vache
Alors l'action et la note de la seconde vache restent inchangées.

Scénario : Signaler une écriture de régie refusée
Étant donné que j'ai modifié une action et sa note et que l'écriture locale est refusée
Quand je tente d'enregistrer la visite
Alors un message en français indique que les modifications ne sont pas enregistrées et aucune confirmation de réussite n'est affichée.
```

## E04 — Remplir le bilan de biosécurité de la visite

**Étiquettes :** `epic`, `epic:biosecurite`  
**Priorité :** Must  
**Objectif :** renseigner et conserver la liste de contrôle de biosécurité associée à la visite.

**Succès de l'epic :** chaque élément peut être renseigné et les réponses sauvegardées sont restituées après rechargement.

### S07 — Renseigner les éléments du bilan de biosécurité

**Étiquettes :** `story`, `epic:biosecurite`  
**User story :** En tant que vétérinaire, je veux renseigner chaque élément de la liste de contrôle de biosécurité afin de documenter le bilan de la visite.

**Critères d'acceptation :**

```gherkin
Scénario : Renseigner toute la liste de contrôle
Étant donné qu'une visite est ouverte avec la liste de contrôle du POC
Quand je parcours et renseigne chaque élément
Alors tous les éléments du jeu de référence sont accessibles et chacun affiche la réponse que j'ai choisie.

Scénario : Corriger une réponse pendant la saisie
Étant donné qu'un élément comporte déjà une réponse dans la visite ouverte
Quand je choisis une autre réponse proposée pour cet élément
Alors la nouvelle réponse remplace la précédente sans modifier les réponses des autres éléments.

Scénario : Signaler une liste de contrôle indisponible
Étant donné que la liste de contrôle de la visite est illisible
Quand j'ouvre le bilan de biosécurité
Alors un message en français indique qu'il ne peut pas être chargé et aucun bilan vide n'est présenté comme complet.
```

### S08 — Retrouver les réponses de biosécurité sauvegardées

**Étiquettes :** `story`, `epic:biosecurite`  
**User story :** En tant que vétérinaire, je veux sauvegarder et retrouver les réponses de biosécurité de ma visite afin de les conserver jusqu'à la production du rapport.

**Critères d'acceptation :**

```gherkin
Scénario : Restituer le bilan après rechargement
Étant donné que j'ai renseigné chaque élément du bilan d'une visite
Quand j'enregistre la visite, recharge la page et rouvre son bilan
Alors chaque réponse sauvegardée est restituée à l'identique sur le bon élément.

Scénario : Isoler les bilans de deux visites
Étant donné que deux visites possèdent des réponses de biosécurité différentes
Quand j'enregistre une modification du bilan de la première visite puis ouvre la seconde
Alors les réponses de la seconde visite restent inchangées.

Scénario : Signaler un échec de sauvegarde du bilan
Étant donné que les réponses ont été modifiées et que la transaction IndexedDB échoue
Quand je tente d'enregistrer la visite
Alors un message en français indique que le bilan modifié n'a pas été enregistré et aucune confirmation de réussite n'est affichée.
```

## E05 — Réaliser et enregistrer une visite complète hors ligne dans IndexedDB

**Étiquettes :** `epic`, `epic:hors-ligne`  
**Priorité :** Must  
**Objectif :** démontrer le parcours complet en étable sans connexion après un premier chargement en ligne.

**Succès de l'epic :** hors ligne, ouvrir une visite, renseigner la régie et la biosécurité, enregistrer et recharger ; toutes les saisies sont conservées sans réseau nécessaire au parcours.

**Frontière avec les autres epics :** E03 et E04 vérifient les saisies métier et leur restitution ; E05 vérifie leur intégration de bout en bout, le cache applicatif et la persistance en absence de réseau.

### S09 — Ouvrir et parcourir une visite sans réseau

**Étiquettes :** `story`, `epic:hors-ligne`  
**User story :** En tant que vétérinaire, je veux ouvrir l'application et accéder à une visite préchargée sans réseau afin de travailler dans une étable sans couverture.

**Critères d'acceptation :**

```gherkin
Scénario : Ouvrir une visite en mode avion
Étant donné que l'application et les données de démonstration ont été chargées une première fois en ligne sur la tablette
Quand j'active le mode avion, rouvre l'application et sélectionne une visite de la tournée
Alors la tournée, la fiche d'élevage, la grille et la biosécurité sont accessibles sans requête réseau nécessaire à leur fonctionnement.

Scénario : Recharger l'application hors ligne
Étant donné que l'application préchargée est ouverte hors ligne à son URL GitHub Pages
Quand je recharge la page puis rouvre la visite
Alors l'application et les données de cette visite restent accessibles sans rétablir de connexion.

Scénario : Signaler des données locales de visite manquantes
Étant donné que l'interface est disponible hors ligne mais que les données de la visite sélectionnée sont absentes
Quand je tente d'ouvrir cette visite
Alors un message en français indique que ses données ne sont pas disponibles localement et aucune visite vide n'est présentée comme la visite attendue.
```

### S10 — Conserver une visite complète après enregistrement hors ligne

**Étiquettes :** `story`, `epic:hors-ligne`  
**User story :** En tant que vétérinaire, je veux enregistrer localement toute ma visite hors ligne afin de retrouver mes actions, notes et réponses après rechargement.

**Critères d'acceptation :**

```gherkin
Scénario : Vérifier le parcours complet hors ligne
Étant donné qu'une visite de référence préchargée est ouverte hors ligne
Quand je marque des actions réalisées, saisis des notes, renseigne chaque élément de biosécurité, enregistre puis recharge la page toujours hors ligne
Alors la même visite restitue toutes les actions, notes et réponses saisies pour les bonnes vaches et aucun accès réseau n'est nécessaire.

Scénario : Rendre explicite la portée du stockage
Étant donné que je travaille hors ligne et que le stockage IndexedDB est disponible
Quand l'enregistrement de ma visite réussit
Alors l'interface confirme un enregistrement local sur cet appareil et indique que les données ne sont ni synchronisées ni sauvegardées à distance.

Scénario : Éviter un enregistrement partiel en cas d'échec
Étant donné qu'une version de la visite est déjà enregistrée et que l'écriture de la nouvelle version échoue pendant l'enregistrement
Quand je consulte à nouveau la visite depuis le stockage local
Alors la dernière version enregistrée reste intacte, sans mélange partiel avec les nouvelles saisies, et l'échec a été signalé sans confirmation de réussite.
```

## E06 — Générer un rapport imprimable et exportable en PDF par le navigateur

**Étiquettes :** `epic`, `epic:rapport`  
**Priorité :** Must  
**Objectif :** produire sur place un compte rendu de visite entièrement côté navigateur.

**Succès de l'epic :** un aperçu présente le résumé, les actions réalisées, les actions à venir et la biosécurité ; l'interface déclenche la boîte d'impression et l'export « Imprimer en PDF » est vérifié manuellement.

### S11 — Générer l'aperçu du rapport de visite

**Étiquettes :** `story`, `epic:rapport`  
**User story :** En tant que vétérinaire, je veux générer un rapport avec le résumé, les actions réalisées, les actions à venir et le bilan de biosécurité afin de remettre un compte rendu fidèle au producteur.

**Critères d'acceptation :**

```gherkin
Scénario : Présenter les quatre sections et leurs données
Étant donné qu'une visite de référence enregistrée comporte des actions réalisées, des notes, des actions restant à faire et des réponses de biosécurité
Quand je génère son aperçu de rapport
Alors le rapport contient les quatre sections, identifie l'élevage et la visite, restitue les notes et réponses, et classe chaque action selon son état enregistré sans inclure les données d'une autre visite.

Scénario : Générer le rapport sans réseau
Étant donné que la visite complète est enregistrée localement et que la tablette est hors ligne
Quand je génère son aperçu
Alors le rapport restitue les données enregistrées sans appeler d'API, de serveur applicatif ni de service externe de génération.

Scénario : Signaler des données de rapport illisibles
Étant donné que les données enregistrées nécessaires au rapport de la visite sont illisibles
Quand je demande son aperçu
Alors un message en français indique que le rapport ne peut pas être généré et aucun rapport partiel n'est présenté comme complet.
```

### S12 — Imprimer le rapport ou l'exporter en PDF

**Étiquettes :** `story`, `epic:rapport`  
**User story :** En tant que vétérinaire, je veux ouvrir l'impression du navigateur pour mon rapport afin de le remettre sur papier ou de l'enregistrer en PDF.

**Critères d'acceptation :**

```gherkin
Scénario : Déclencher l'impression côté navigateur
Étant donné qu'un aperçu valide du rapport est affiché hors ligne sur le navigateur cible
Quand j'active la commande d'impression
Alors la boîte de dialogue d'impression du navigateur s'ouvre sans service externe et la sortie imprimable contient les quatre sections du rapport sans les commandes de navigation de l'application.

Scénario : Vérifier manuellement l'export PDF
Étant donné que la boîte d'impression du navigateur cible propose « Imprimer en PDF » pour le rapport de référence
Quand je sélectionne cette destination, enregistre le PDF puis l'ouvre
Alors les quatre sections et leurs données sont présentes, lisibles et non tronquées, y compris lorsque le rapport occupe plusieurs pages.

Scénario : Ne pas annoncer une impression réussie si le dialogue échoue
Étant donné qu'un aperçu valide est affiché mais que l'appel à l'impression du navigateur échoue
Quand j'active la commande d'impression
Alors un message en français signale l'impossibilité d'ouvrir l'impression, l'aperçu reste accessible et aucune impression ni aucun PDF n'est annoncé comme réussi.
```

## Dépendances de réalisation

- E02 dépend de la disponibilité du jeu de données et du point d'entrée E01.
- E03 et E04 dépendent du contexte d'élevage et de visite accessible dans E02 ; ils peuvent ensuite être réalisés en parallèle.
- Le préchargement et le cache de S09 peuvent être travaillés dès E01. La validation intégrée de S10 dépend des saisies de E03 et E04.
- E06 dépend des données et états de visite de E03 et E04. Sa validation hors ligne dépend également de E05.
- Aucun epic Must n'est supprimé si le délai devient contraignant : un arbitrage explicite du périmètre est nécessaire.

## Définition de terminé du POC

- Les critères de chaque story sont vérifiés avec le jeu fictif de référence et les cas d'erreur.
- Le déploiement réel sur GitHub Pages est testé à son URL, pas seulement en développement local.
- Le parcours intégré de S10 est exécuté sur la tablette cible en mode avion après le premier chargement en ligne, puis après rechargement.
- Le rapport de référence est comparé aux saisies ; l'ouverture du dialogue d'impression est testée et le PDF est vérifié manuellement.
- Le parcours complet est essayé sur tablette par un vétérinaire ; le temps de réalisation et les erreurs de saisie sont relevés, sans inventer de seuil de réussite absent du MVP.
- Les limites sont explicites : données fictives, règles simplifiées, données locales limitées à l'appareil, absence de synchronisation et de sauvegarde distante.
