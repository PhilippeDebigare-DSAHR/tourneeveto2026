# Contrat de persistance locale

## API C#

`IVisitRepository` est dans `src/TourneeVeto.Ui/Data/` :

| Opération | Comportement |
| --- | --- |
| `InitializeDemoAsync(seed)` | Initialise atomiquement une base vierge, ou adopte les données existantes sans les modifier ; erreur si elles sont illisibles. |
| `GetAllAsync()` | Toutes les visites ; liste vide seulement si la lecture réussit. |
| `GetAsync(id)` | Visite ou `null` si cet identifiant est absent. |
| `SaveAsync(visit, photo = null)` | Upsert de la visite ; photo fournie enregistrée atomiquement. |
| `DeleteAsync(id)` | Suppression idempotente de la visite et de sa photo. |
| `GetHerdAsync()` | Snapshot `DemoDataSet`, ou `null` avant toute sauvegarde. |
| `SaveHerdAsync(herd)` | Remplacement explicite et atomique du snapshot élevages + vaches. |
| `GetPhotoAsync(photoId)` | Photo et octets, ou `null` si absente. |
| `SavePhotoAsync(visitId, photo)` | Remplacement de la photo et mise à jour du lien de la visite dans une transaction. |
| `DeletePhotoAsync(visitId)` | Suppression de la photo et remise à `null` de `Visit.PhotoId`. Visite absente : erreur. |

Le modèle actuel autorise **une photo par visite**, détenue exclusivement par
cette visite. `VisitPhoto` contient `Id`, `ContentType`, `Data` (`byte[]`).
Les octets doivent être non vides et le type MIME une image ; aucune limite
arbitraire de taille ajoutée, le quota du navigateur reste applicable.
Une photo ne peut pas être partagée entre visites. Un `PhotoId` sans photo
existante ou fournie est rejeté. Remplacer ou retirer ce lien supprime
l'ancienne photo dans la même transaction.

L'initialisation des données fictives relève de `DemoStartupService` :
appeler `InitializeDemoAsync` puis relire le troupeau et les visites.
La décision d'initialiser est prise dans la transaction, pas à partir d'une lecture C#
préalable qui pourrait devenir obsolète pendant le démarrage d'un autre onglet.
L'ouverture de base n'initialise jamais les données métier.
Ne pas confondre le snapshot du troupeau avec les futures saisies de grille :
le modèle `Visit` contient désormais les actions fictives, mais pas encore la biosécurité.

## Schéma IndexedDB

- Base : `tourneeveto`, version **2**.
- Version 1 : `visits` (clé `id`), `herds` (clé `id`, snapshot `current`).
- Version 2 : ajoute `photos` (clé `id`) sans toucher aux documents existants.
- Photo : `{ id, visitId, contentType, data: Uint8Array }`.
- Snapshot : `{ id: "current", locations: [...], cows: [...] }`.
- Marqueur dans `herds` : `{ id: "demo-initialization", format: 1, adopted: boolean }`,
  complété par `referenceDate` et `seed` pour une base initialisée avec le jeu complet.
  Son ajout ne nécessite aucun nouveau store ni montée de version. Un snapshot antérieur
  est conservé tel quel ; ses visites et photos ne sont ni remplacées ni complétées.
- `Visit.actions` : tableau de `{ id, cowId, type, date, isCompleted, notes }`.
  Une ancienne visite sans cette propriété reste lisible comme une collection vide.
  Une propriété explicitement invalide et les références incohérentes au démarrage
  sont rejetées sans réinitialisation.
- Pas de seed automatique, pas de suppression de base, pas de stockage distant.
- L'adaptateur ouvre une connexion pour chaque opération et la ferme après
  la transaction ; `versionchange` ferme aussi la connexion.

Une future migration doit préserver les données et augmenter la version.
Une base de version supérieure est rejetée (`VersionError`), jamais recréée.
Un upgrade bloqué est signalé avec la consigne de fermer les autres onglets.

## Transport et erreurs

Visites et troupeau : JSON source-généré en camelCase ; dates `DateOnly` ISO,
dates facultatives `null`, enums numériques existants, Guid en chaîne.
Les réponses JSON sont `{ value, error }`, où `error` est `null` en cas
de succès ou `{ code, detail }` en cas d'échec. La présence d'une erreur
provoque une exception C# ; une mutation réussie retourne `value: true`.

Photos : `byte[]` en argument séparé (interop optimisé), jamais base64.
`getPhoto` retourne `{ result: "<enveloppe JSON de métadonnées>", data }`,
avec `data: Uint8Array` seulement pour une photo trouvée. Le DTO binaire
est explicitement conservé au trimming ; tous les noms wire sont figés.

Codes : `quota`, `unavailable`, `blocked`, `version`, `invalid`, `unknown`.
Le dépôt traduit ces codes en erreurs françaises, conserve les détails et
les exceptions d'interop. L'interface doit conserver les saisies en cas
d'échec et ne confirmer un enregistrement local qu'après le retour réussi.
Même si IndexedDB fonctionne en navigation privée, ses données peuvent
disparaître à la fermeture ; aucune détection ou garantie de durabilité.