# Matrice de tests IndexedDB

## Tests automatisés C#

Depuis la racine, sans démarrer l'application :

```powershell
dotnet build tests\TourneeVeto.Tests\TourneeVeto.Tests.csproj --no-restore
if ($LASTEXITCODE -eq 0) {
    dotnet test tests\TourneeVeto.Tests\TourneeVeto.Tests.csproj --no-build --filter "FullyQualifiedName~IndexedDbVisitRepositoryTests"
}
```

Restaurer seulement si les dépendances requises manquent. bUnit déclare
l'import exact et chaque méthode du module. Ces tests vérifient le contrat
C#/JS, pas les transactions du navigateur.

## Tests dans un vrai navigateur

S02 ajoute les tests Playwright .NET reproductibles dans
[TourneeVeto.E2E](../../../../tests/TourneeVeto.E2E/README.md) :
première initialisation, deux onglets, conservation des notes/actions/photos,
suppression de toutes les visites, rollback de quota, stockage refusé ou absent,
snapshot illisible, marqueur sans troupeau, référence d'action invalide et adoption
d'une base v1 peuplée. Ces tests utilisent une origine de test éphémère sans serveur.
Les trois tests de l'application publiée exigent une URL locale fournie par l'humain ;
leur absence est signalée comme un skip explicite, pas comme une validation réussie.

Utiliser une origine locale **réservée aux tests**, jamais le stockage de
l'utilisateur. Avant toute suppression de base, vérifier que les données
ont été créées par le test. Le module est importé par ES module depuis les
assets UI ; les essais peuvent être exécutés avec les outils Playwright du
navigateur sur ces assets statiques, sans `dotnet run` ni `dotnet watch`.

| Scénario | Préparation et action | Résultat attendu |
| --- | --- | --- |
| Base absente | Importer et appeler GetAll, Get, GetHerd, GetPhoto. | Base v2 créée, lectures vides/null, aucune donnée métier inventée. |
| Visite | Save puis Get/GetAll ; modifier le même id et réouvrir. | Dates ISO et textes français exacts, une seule visite, notes modifiées conservées. |
| Troupeau | Sauver un snapshot avec élevages, vaches, dates nullables puis réouvrir. | Identifiants, relations, dates et valeurs null intactes ; remplacement explicite seulement. |
| Photo | Save avec PhotoId et Uint8Array `[0,127,128,255]`, puis GetPhoto. | Métadonnées exactes et même Uint8Array, pas de base64. |
| Photo exclusive | Sauver une deuxième visite avec le PhotoId de la première. | `invalid`, aucune mutation de l'une ou l'autre visite. |
| Photo remplacée | SavePhoto avec un nouvel id, puis Get de l'ancienne photo. | Lien actualisé, ancienne photo absente. |
| Suppression | DeletePhoto puis Delete ; Delete une deuxième fois. | Lien remis à null, aucun orphelin, suppression de visite idempotente. |
| Quota | Dans le navigateur de test, faire lever `QuotaExceededError` au put de visite après le put de sa nouvelle photo. | `quota`, ancienne visite et ancienne photo intactes, nouvelle photo annulée. Restaurer l'API en `finally`. |
| Stockage refusé | Faire lever `SecurityError` par indexedDB.open dans le test. | `unavailable`, jamais de liste vide ni de confirmation de sauvegarde. Restaurer l'API en `finally`. |
| API absente | Masquer temporairement indexedDB dans le test. | `unavailable`. Restaurer son descripteur d'origine. |
| Migration | Créer une base v1 avec visites/herds peuplés, fermer, ouvrir via le module v2. | Documents conservés, nouveau store photos utilisable. |
| Upgrade bloqué | Maintenir une connexion v1 sans fermeture automatique pendant l'ouverture v2. | `blocked` immédiat ; fermer l'ancien onglet puis réessayer sans perdre les données. |
| Version future | Créer une base v3, puis ouvrir via le module v2. | `version`, aucune suppression ou rétrogradation. |
| Navigation privée | Tester selon les navigateurs ciblés. | Refus éventuel signalé ; un succès ne garantit pas la conservation après fermeture. |
| Hors ligne | Charger le module puis couper le réseau et effectuer CRUD. | Aucun appel réseau pendant les opérations IndexedDB ; le module doit déjà être disponible. |

Les erreurs de quota et de stockage privé sont **simulées de façon
déterministe**, car saturer le disque ou identifier la navigation privée
n'est ni sûr ni portable. La transaction de rollback doit être réelle.

## Intégration future aux écrans

Quand un parcours est branché à `IVisitRepository`, ajouter un test
Playwright après premier chargement : enregistrement hors ligne, reload,
message « enregistré localement sur cet appareil », absence de confirmation
et conservation de la saisie après échec. Tester séparément le cache PWA :
la présence d'IndexedDB ne prouve pas que l'application peut redémarrer hors ligne.