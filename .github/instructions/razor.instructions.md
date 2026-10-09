---
applyTo: "**/*.razor,**/*.razor.css,**/*.razor.cs,src/TourneeVeto.Ui/wwwroot/**/*.css,src/TourneeVeto.Web/wwwroot/css/**/*.css,src/TourneeVeto.Web/wwwroot/index.html"
description: "Règles UI Blazor et CSS : maquettes, jetons sémantiques, contraste AA, typographie et ergonomie tactile hors ligne."
---
- Références visuelles : `design/tourneeveto_scrrens/` (captures et `code.html`) et `design/tourneeveto_scrrens/tourn_ev_to_fieldwork/DESIGN.md`. Les consulter localement, sans charger leurs scripts, images ni polices distantes.
- Couleurs, espacements, rayons et typographie : réutiliser les variables de `src/TourneeVeto.Ui/wwwroot/tokens.css` ; styles des composants dans le `.razor.css` isolé. Le fichier de jetons ne contient que les variables globales, pas de styles de composants.
- En cas de divergence du guide, conserver les surfaces vert pâle des captures et les tailles lisibles du guide. Ne pas reproduire les microtextes inférieurs à 13 px ; corps de carte à 18 px.
- Associer chaque `--color-on-*` exclusivement à son fond homonyme. Sur les surfaces claires, utiliser `--color-text` ou `--color-text-muted`, y compris pour les placeholders. Ne pas utiliser les fonds de statut comme texte.
- Vérifier chaque paire texte/fond avec la luminance relative WCAG sRGB : au moins 4,5:1, sans arrondir avant comparaison. Tester les nouveaux jetons et les états de survol dans `tests/TourneeVeto.Tests/Ui/DesignTokensTests.cs`.
- Contours interactifs et focus : contraste d'au moins 3:1 avec les surfaces adjacentes ; utiliser `--color-border-control`, réserver `--color-border` aux séparateurs décoratifs. Préserver le décalage du focus et son contraste autour des boutons sombres.
- Ne jamais communiquer un état uniquement par sa couleur : ajouter un libellé ou une icône accompagnée d'un texte accessible.
- Work Sans et JetBrains Mono avec les familles de secours locales définies dans les jetons ; aucun téléchargement de police, CDN ni `@import` distant.
- Tablette d'abord : mise en page pensée pour 768 px, puis adaptée au téléphone et au poste.
- Accessibilité AA : label sur chaque champ, focus visible au clavier, cibles tactiles d'au moins `--touch-target` (44 px) en largeur et hauteur ; boutons à 52 px, champs à 54 px au minimum via les jetons correspondants. Espacer les contrôles d'au moins `--space-sm` lorsque la mise en page le permet.
- Aucune logique métier : appeler TourneeVeto.Domain ; données via IVisitRepository injecté.
- Jamais de MarkupString avec une donnée saisie ou importée.
- Tous les textes visibles et messages d’erreur de l’interface doivent être en français.
- La persistance doit passer par les abstractions de dépôt prévues et fonctionner localement hors ligne; ne pas ajouter de backend ni d’appel réseau.
- Tout JavaScript doit résider dans src/TourneeVeto.Ui/wwwroot/js/ et être chargé par module isolé via IJSObjectReference.