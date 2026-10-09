---
name: indexeddb-interop
description: "Créer, modifier ou tester la persistance IndexedDB de TournéeVéto depuis C# : dépôt IVisitRepository, module JavaScript ES isolé visitStore.js, visites, troupeau, photos byte[], sérialisation DateOnly, migrations, quota et navigation privée. Utiliser pour tout accès au stockage local ou toute évolution de son schéma, hors ligne d'abord et sans backend."
---
# IndexedDB via un dépôt C# et un module ES isolé

## Contexte obligatoire

Lire les [instructions du projet](../../copilot-instructions.md),
l'[ADR de persistance existant](../../../docs/adr/0001-statique.md) et les
[règles de tests](../../instructions/tests.instructions.md).
L'ADR demandé comme `0001-stockage.md` est actuellement nommé `0001-statique.md` :
ne pas créer une décision concurrente. Consulter le backlog pour les critères
de persistance, de rechargement et d'échec d'écriture.

Zéro backend, secret, compte, synchronisation ou appel réseau sortant.
Ne jamais lire `data/reel/`, `exports/` ni les fichiers `.pfx`.
Seules les données fictives sont autorisées. Une sauvegarde locale n'est pas une
sauvegarde distante et peut être perdue si le navigateur efface ses données.

## Procédure

1. **Lire avant de modifier.** Examiner les modèles `Visit`, `Cow`, `Location`,
   le dépôt existant, son adaptateur et le module. Clarifier toute ambiguïté de
   relation, de suppression ou de migration. Demander à l'utilisateur avant
   d'abandonner une implémentation bloquée. Réutiliser les dépendances présentes ;
   signaler tout nouveau paquet dans la réponse.
2. **Fixer le contrat C#.** L'interface
   [IVisitRepository](../../../src/TourneeVeto.Ui/Data/IVisitRepository.cs) est
   dans `TourneeVeto.Ui/Data/`, pas dans le domaine pur. Les écrans injectent
   cette interface ; aucun accès JS ou IndexedDB direct dans les composants.
   Définir GetAll, Get, Save, Delete et les opérations troupeau/photos avant
   le JavaScript. Les méthodes asynchrones portent le suffixe `Async`.
   Ne pas ajouter de calcul clinique dans le dépôt.
3. **Écrire les tests de contrat.** bUnit utilise `BunitContext` et
   `JSInterop.SetupModule` pour déclarer chaque appel, sans vraie base IndexedDB.
   Couvrir les absences, la sérialisation, les paramètres invalides, l'import
   concurrent, les erreurs et la libération du module.
4. **Importer à la demande.** L'adaptateur
   [IndexedDbVisitRepository](../../../src/TourneeVeto.Ui/Data/IndexedDbVisitRepository.cs)
   importe exactement `./_content/TourneeVeto.Ui/js/visitStore.js` par
   `IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)`.
   Pas de script global, de balise script ou de CDN. Tout JavaScript applicatif
   reste dans `src/TourneeVeto.Ui/wwwroot/js/`.
   Sérialiser les imports concurrents et libérer `IJSObjectReference` via
   `IAsyncDisposable`. Enregistrer le dépôt comme service scoped dans l'hôte.
5. **Respecter le protocole.** Voir le
   [contrat de stockage](./references/storage-contract.md). Les documents métier
   passent en JSON camelCase avec `System.Text.Json` et un contexte généré pour
   le trimming. `DateOnly` = chaîne ISO `yyyy-MM-dd`, jamais conversion par
   `new Date()` ni fuseau horaire. Guid = chaîne canonique.
   Les photos `byte[]` passent par l'interop binaire optimisé (`Uint8Array`),
   jamais en base64 dans les documents de visite ou de troupeau.
6. **Ouvrir et migrer explicitement.** Le module
   [visitStore.js](../../../src/TourneeVeto.Ui/wwwroot/js/visitStore.js) est la
   seule autorité du nom et de la version de base. Une base absente est créée
   par `onupgradeneeded`. Toute nouvelle version a une migration incrémentale
   non destructive et un test sur une ancienne base peuplée.
   Ne jamais supprimer la base pour corriger une erreur. Fermer les connexions
   sur `versionchange` ; une montée bloquée par un autre onglet doit échouer
   explicitement, pas attendre indéfiniment.
7. **Écrire atomiquement.** Attendre `transaction.oncomplete`, pas seulement le
   succès de `put`. Une visite et sa photo sont enregistrées dans la même
   transaction ; les anciennes données restent intactes si celle-ci échoue.
   La suppression d'une visite supprime sa photo sans orphelin. Le remplacement
   du troupeau est explicite et atomique. Ne pas attendre de réseau, timer ou
   interop dans une transaction active. Ne jamais réensemencer automatiquement
   à l'ouverture et écraser les saisies.
8. **Rendre les erreurs observables.** Quota dépassé, stockage refusé ou absent,
   migration bloquée, version plus récente et document invalide ont un code
   stable et un message français via `VisitStorageException`.
   Une erreur inconnue reste une erreur avec ses détails ; pas de retour vide,
   de faux succès, de repli vers une liste en mémoire ou `localStorage`.
   La navigation privée n'est pas détectable de façon fiable : tester les
   opérations, signaler les refus et prévenir que la durée de conservation
   n'est pas garantie, même après une écriture réussie.
9. **Vérifier réellement.** Compiler puis exécuter les tests C# ciblés et les
   [scénarios navigateur](./references/test-matrix.md) : base absente, CRUD après réouverture,
   photos binaires, migration, quota et refus. bUnit ne valide pas IndexedDB.
   Chaque parcours UI clé branché ensuite au dépôt a son test Playwright hors
   ligne après chargement initial, avec rechargement et échec de sauvegarde.
   Ne jamais lancer `dotnet run` ni `dotnet watch` ; l'humain démarre l'application.
   Vérifier `/tourneeveto/`, le cache PWA du module et le trimming lorsqu'ils
   sont concernés. IndexedDB seul ne met pas l'application en cache.
10. **Livrer.** Lister les fichiers modifiés et les vérifications exécutées,
    les limites, erreurs bloquantes et nouveaux paquets éventuels. Ne pas
    annoncer une disponibilité hors ligne ni une sauvegarde réussie sans preuve.