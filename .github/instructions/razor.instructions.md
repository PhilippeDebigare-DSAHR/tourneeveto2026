---
applyTo: "**/*.razor,**/*.razor.css,**/*.razor.cs"
description: Règles des pages et composants Razor
---
- Couleurs, espacements et rayons : uniquement les variables de wwwroot/tokens.css (créé au lab 4.1) ; styles dans le .razor.css isolé.
- Tablette d'abord : mise en page pensée pour 768 px, puis adaptée au téléphone et au poste.
- Accessibilité AA : label sur chaque champ, focus visible, cibles tactiles d'au moins 44 px.
- Aucune logique métier : appeler TourneeVeto.Domain ; données via IVisitRepository injecté.
- Jamais de MarkupString avec une donnée saisie ou importée.
