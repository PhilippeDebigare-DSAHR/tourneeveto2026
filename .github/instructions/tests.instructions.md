---
applyTo: "tests/**/*.cs"
description: Règles d'écriture des tests xUnit, bUnit et Playwright de TournéeVéto
---
- Figer « aujourd'hui » avec FakeTimeProvider (Microsoft.Extensions.TimeProvider.Testing) dans tout test qui dépend de la date.
- Noms de test en français, une classe par règle métier (« Tarissement_est_proposé_60_jours_avant_le_vêlage »).
- Pas de mock du domaine : il est pur, on le teste directement.
- bUnit : BunitContext ; déclarer chaque appel JS (JSInterop.SetupModule) ; jamais de vraie base IndexedDB.
- Données : DemoData.Generate(today, seed) avec un seed fixe.
- Playwright : tests end-to-end pour les parcours clés, vérifier l'accessibilité AA et les cibles tactiles d'au moins 44 px.
- Toujours utiliser un seed fixe pour les données afin d'assurer la reproductibilité des tests.
