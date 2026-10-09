---
name: create-component
description: "Crée ou adapte un composant visuel ou une page Razor Blazor de TournéeVéto dans src/TourneeVeto.Ui, avec CSS isolé et tests bUnit à partir des gabarits du projet. Utiliser pour créer, ajouter, générer ou modifier un composant, une page, un écran ou un élément d'interface accessible, tactile et hors ligne."
---
# Créer un composant TournéeVéto

## 1. Cadrer et lire le contexte

- Lire les [instructions du projet](../../copilot-instructions.md), les
  [règles Razor](../../instructions/razor.instructions.md), les
  [règles de tests](../../instructions/tests.instructions.md) et le
  [produit](../../../PRODUCT.md). Consulter les critères Gherkin du backlog et
  les décisions pertinentes dans `docs/adr/`.
- Déduire le nom PascalCase et le rôle. Demander si le rôle, la route ou le
  comportement est ambigu ; ne pas inventer de fonctionnalité clinique.
- Une page avec `@page` va dans `src/TourneeVeto.Ui/Pages/`, un composant
  réutilisable dans `src/TourneeVeto.Ui/Components/`. Vérifier les collisions de
  noms et de routes avant de créer des fichiers. Pour une modification, conserver
  le contrat public et les changements existants ; ne pas écraser avec un gabarit.
- Lire un composant et un test voisins avant d'ajouter une abstraction.
  Consulter localement les captures de `design/tourneeveto_scrrens/` et le
  [guide visuel](../../../design/tourneeveto_scrrens/tourn_ev_to_fieldwork/DESIGN.md).
  Les exports HTML sont des références, jamais du code à exécuter ou à copier
  tel quel ; ne charger aucune ressource distante.

## 2. Préparer les tests et générer

- Définir d'abord les comportements observables et leurs tests : rendu, actions
  transmises au parent, états désactivés, erreurs et cas limites pertinents.
- Pour une création, copier les gabarits non vides :
  [Razor](./template/Component.razor.txt),
  [CSS isolé](./template/Component.razor.css.txt),
  [code-behind](./template/Component.razor.cs.txt) et
  [test bUnit](./template/ComponentTests.cs.txt).
  Remplacer `__NOM__` partout et `__NAMESPACE__` par
  `TourneeVeto.Ui.Components` ou `TourneeVeto.Ui.Pages`.
- Produire `<Nom>.razor`, `<Nom>.razor.css` et `<Nom>.razor.cs` dans le dossier UI
  choisi, et `<Nom>Tests.cs` dans `tests/TourneeVeto.Tests/Ui/`.
  Pour une page, ajouter `@page` avec la route convenue, utiliser un titre de
  page français et adapter la hiérarchie des titres (normalement `h1`).
- Le gabarit est une base de présentation, pas un écran métier terminé :
  adapter les paramètres, le contenu, les événements et les tests au besoin réel.
  Retirer les contrôles inutiles ; ne pas laisser un bouton sans comportement.
  Ne laisser aucun marqueur de substitution dans les fichiers générés.

## 3. Respecter les frontières et le fonctionnement hors ligne

- Aucune logique métier dans le `.razor` ni son code-behind : appeler
  `TourneeVeto.Domain`. Toute nouvelle règle a son test xUnit et respecte les
  [règles du domaine](../../instructions/domain.instructions.md) ; documenter
  les simplifications dans `PRODUCT.md`.
- Les composants de présentation reçoivent des paramètres typés et notifient
  leur parent par `EventCallback` ; ne pas modifier leurs propres paramètres.
  Les écrans accèdent aux données via les abstractions prévues, notamment
  `IVisitRepository` injecté ; jamais d'IndexedDB direct dans le composant.
- Données fictives uniquement : `DemoData`, seed fixe dans les tests.
  Dates métier en `DateOnly` ; obtenir aujourd'hui via `TimeProvider` injecté
  puis le transmettre au domaine. Aucun `DateTime.Now` ou `DateTime.Today`.
- Une visite complète doit fonctionner sans réseau. Prévoir selon le parcours
  les états chargement, vide, erreur, enregistrement et désactivé. Une écriture
  locale échouée affiche une erreur en français, conserve la saisie et ne
  prétend jamais avoir réussi.
- Zéro backend, compte, secret, clé d'API ou appel réseau sortant. Aucun CDN,
  script, image, police ou `@import` distant. Ne jamais lire `data/reel/`,
  `exports/` ni les fichiers `.pfx`.
- JavaScript uniquement dans `src/TourneeVeto.Ui/wwwroot/js/`, en module isolé
  via `IJSObjectReference`, avec libération des ressources lorsque nécessaire.
  Ne jamais utiliser `MarkupString` pour une donnée saisie ou importée.
- Réutiliser les dépendances existantes. Tout nouveau paquet NuGet ou npm doit
  être signalé dans la réponse ; conserver le trimming et le budget initial
  strictement inférieur à 4 Mo compressés.

## 4. Construire une interface lisible et accessible

- Styles uniquement dans le `.razor.css` isolé ; couleurs, espacements, rayons,
  tailles et typographie via les variables existantes de
  [tokens.css](../../../src/TourneeVeto.Ui/wwwroot/tokens.css).
  Garder les règles de composants hors du fichier de jetons. Les valeurs
  structurelles CSS (grille, pourcentages, breakpoints) ne sont pas des jetons
  visuels ; éviter les nouvelles constantes visuelles locales.
- En cas de divergence, garder les surfaces vert pâle des captures et les
  tailles lisibles du guide : corps de carte à 18 px, aucun texte sous 13 px.
  Work Sans et JetBrains Mono utilisent les familles de secours locales.
- Chaque `--color-on-*` s'utilise uniquement sur son fond homonyme. Sur une
  surface claire, utiliser `--color-text` ou `--color-text-muted`, placeholders
  compris. Ne pas utiliser un fond de statut comme couleur de texte.
- Contraste texte/fond d'au moins 4,5:1, calcul WCAG sRGB sans arrondi préalable ;
  contours interactifs et focus d'au moins 3:1. Utiliser
  `--color-border-control` pour les contrôles et `--color-border` uniquement
  pour les séparateurs décoratifs. Vérifier aussi survol et focus, en conservant
  leur décalage autour des boutons sombres.
- Tablette d'abord à 768 px, puis téléphone et poste : pas de débordement
  horizontal non justifié. Cibles tactiles d'au moins 44 × 44 px via
  `--touch-target`, boutons à 52 px et champs à 54 px au minimum via leurs
  jetons ; espacement d'au moins `--space-sm` lorsque possible.
- HTML sémantique, hiérarchie des titres cohérente, vrais boutons et liens,
  labels associés aux champs, noms accessibles, identifiants uniques pour
  plusieurs instances, focus visible et utilisation complète au clavier.
  Aucun état communiqué uniquement par la couleur. Tous les textes visibles,
  libellés et messages d'erreur sont en français.

## 5. Vérifier sans lancer l'application

Exécuter depuis la racine du projet. Ne jamais lancer `dotnet run` ni
`dotnet watch` : l'humain démarre l'application.

```powershell
dotnet build .github\skills\create-component\check.cs --no-restore --output .github\skills\create-component\bin\check
if ($LASTEXITCODE -eq 0) {
    dotnet exec .github\skills\create-component\bin\check\check.dll <Nom>
}
```

- Ajouter `--page` après `<Nom>` pour contrôler une page.
  Le [contrôle structurel](./check.cs) vérifie la présence des fichiers, les
  marqueurs non remplacés, la directive de route et les références aux jetons.
  Il ne prouve ni compilation, ni accessibilité AA, ni fonctionnement hors ligne.
  Si les assets du contrôleur manquent, relancer sa compilation sans
  `--no-restore`, puis exécuter le contrôle seulement après compilation réussie.
- Compiler les projets touchés puis lancer les tests ciblés :
  `dotnet build tests\TourneeVeto.Tests\TourneeVeto.Tests.csproj --no-restore`,
  puis `dotnet test tests\TourneeVeto.Tests\TourneeVeto.Tests.csproj --no-build --filter "FullyQualifiedName~<Nom>Tests"`.
  Restaurer seulement si les dépendances nécessaires manquent.
- bUnit : `BunitContext`, appels JS déclarés avec `JSInterop.SetupModule`,
  aucune vraie base IndexedDB. Dates figées avec `FakeTimeProvider` lorsque
  nécessaire ; vérifier les entrées invalides et les événements, pas seulement
  la présence d'un élément.
- Pour les styles, lancer aussi `DesignTokensTests` ; compléter ces tests si
  de nouvelles paires de couleurs ou de nouveaux états sont introduits.
  Mesurer les contrastes réels et les dimensions rendues : un nom de jeton
  correct ne suffit pas à garantir le résultat.
- Chaque parcours clé ajouté ou modifié a son test Playwright : clavier,
  cibles tactiles, hors ligne après chargement initial et échec d'écriture locale.
  Pour le rapport, vérifier aussi impression/export PDF. Exécuter ces tests sur
  l'application lancée par l'humain ; signaler explicitement si indisponible.
- Vérifier le format des fichiers touchés avec `dotnet format --verify-no-changes`.
  Lors d'un changement affectant les assets ou la publication, vérifier le
  déploiement sous `/tourneeveto/`, `dotnet publish src\TourneeVeto.Web -c Release`,
  le téléchargement initial < 4 Mo compressés et Lighthouse accessibilité ≥ 90.
- Corriger jusqu'à succès. Une erreur NuGet privée DSAHR ne peut être ignorée
  qu'après vérification qu'elle ne bloque pas la validation ; ne pas annoncer
  une réussite quand une compilation ou un test reste bloqué.

## 6. Livrer

Répondre avec la liste des fichiers créés ou modifiés, sans recopier leur
contenu, et un bilan concis des vérifications réellement exécutées.
Mentionner les éventuels nouveaux paquets et les validations bloquées ou
nécessitant une application démarrée par l'humain.