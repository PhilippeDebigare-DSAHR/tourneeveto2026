---
applyTo: "src/TourneeVeto.Domain/**"
description: Règles de la couche domaine
---
- C# pur : aucune référence à Blazor, Microsoft.JSInterop ni aux API du navigateur.
- Recevoir « aujourd'hui » en paramètre (DateOnly) ; jamais DateTime.Now ni DateTime.Today.
- Records immuables, aucun état global ; toute règle métier a son test.
- Les règles métier doivent rester simplifiées et ne jamais être présentées comme un avis clinique, une prescription ou une exigence réglementaire.
- Le domaine ne doit effectuer aucune entrée/sortie, persistance, appel réseau, accès navigateur ou génération de valeurs aléatoires non déterministe.
- Valider les entrées métier et définir le comportement attendu des valeurs invalides ou des dates limites.