---
applyTo: "src/TourneeVeto.Domain/**"
description: Règles de la couche domaine
---
- C# pur : aucune référence à Blazor, Microsoft.JSInterop ni aux API du navigateur.
- Recevoir « aujourd'hui » en paramètre (DateOnly) ; jamais DateTime.Now ni DateTime.Today.
- Records immuables, aucun état global ; toute règle métier a son test.