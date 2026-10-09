# TournéeVéto — instructions pour Copilot
Produit : voir PRODUCT.md. Décisions : docs/adr/. Backlog : #file:./ISSUE.md  (critères Gherkin)  

## Stack
.NET 10 · C# · Blazor WebAssembly autonome (PWA) · IndexedDB via JS interop · xUnit + bUnit · Playwright pour .NET.
Application 100 % statique publiée sur GitHub Pages sous /tourneeveto/. Hôtes WPF et MAUI (Blazor Hybrid) au module 10.

## Commandes
dotnet build · dotnet test · dotnet format --verify-no-changes · dotnet publish src/TourneeVeto.Web -c Release
Ne lance jamais dotnet run ni dotnet watch : ils ne rendent pas la main (l'humain lance l'application).

## Structure
src/TourneeVeto.Domain/  règles métier pures (aucune dépendance à Blazor, au JS ni au navigateur)
src/TourneeVeto.Ui/      Razor Class Library : pages, composants, accès IndexedDB (Data/, wwwroot/js/)
src/TourneeVeto.Web/     hôte Blazor WebAssembly : Program.cs, index.html, service worker
tests/TourneeVeto.Tests/ xUnit (domaine) + bUnit (composants) · tests/TourneeVeto.E2E/ Playwright

## Principes non négociables
1. Zéro backend, zéro secret : tout ce qui est dans wwwroot/ est public.
2. Hors ligne d'abord : une visite complète se fait sans réseau.
3. Tests d'abord : toute règle métier a son test xUnit ; chaque parcours clé, son test Playwright.
4. Accessibilité AA, tablette d'abord : cibles tactiles d'au moins 44 px (gants, lumière vive).
5. Budget : téléchargement initial < 4 Mo compressés (trimming actif) ; Lighthouse accessibilité ≥ 90.

## Règles du projet
- Dates : DateOnly ; « aujourd'hui » vient d'un TimeProvider injecté, jamais de DateTime.Now dans le domaine.
- Données fictives uniquement (DemoData) ; règles métier simplifiées, documentées dans PRODUCT.md.
- Aucune logique métier dans un .razor : appeler TourneeVeto.Domain.
- JavaScript uniquement dans src/TourneeVeto.Ui/wwwroot/js/, chargé en module isolé (IJSObjectReference).
- Textes d'interface en français.
- Si une erreur liée au NuGet privé DSAHR survient, valider que l'erreur ne cause pas de problème et ignorer.

## Interdits
- Pas de backend, pas de secret, pas de clé d'API, aucun appel réseau sortant.
- Ne jamais lire data/reel/, exports/ ni les fichiers .pfx.
- Pas de nouveau paquet NuGet ou npm sans le signaler dans la réponse.