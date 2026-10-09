---
name: TournéeVéto Fieldwork
colors:
  surface: '#f1fcf2'
  surface-dim: '#d1ddd3'
  surface-bright: '#f1fcf2'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#ebf7ed'
  surface-container: '#e5f1e7'
  surface-container-high: '#e0ebe1'
  surface-container-highest: '#dae5dc'
  on-surface: '#141e18'
  on-surface-variant: '#404941'
  inverse-surface: '#28332c'
  inverse-on-surface: '#e8f4ea'
  outline: '#717970'
  outline-variant: '#c0c9be'
  surface-tint: '#2e6a41'
  primary: '#003b1b'
  on-primary: '#ffffff'
  primary-container: '#14532d'
  on-primary-container: '#87c695'
  inverse-primary: '#96d5a3'
  secondary: '#9a4614'
  on-secondary: '#ffffff'
  secondary-container: '#fd925b'
  on-secondary-container: '#712c00'
  tertiary: '#6b0005'
  on-tertiary: '#ffffff'
  tertiary-container: '#96000b'
  on-tertiary-container: '#ff9d92'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#b1f2be'
  primary-fixed-dim: '#96d5a3'
  on-primary-fixed: '#00210d'
  on-primary-fixed-variant: '#12512c'
  secondary-fixed: '#ffdbcb'
  secondary-fixed-dim: '#ffb693'
  on-secondary-fixed: '#341000'
  on-secondary-fixed-variant: '#7a3000'
  tertiary-fixed: '#ffdad6'
  tertiary-fixed-dim: '#ffb4ab'
  on-tertiary-fixed: '#410002'
  on-tertiary-fixed-variant: '#93000b'
  background: '#f1fcf2'
  on-background: '#141e18'
  surface-variant: '#dae5dc'
typography:
  headline-xl:
    fontFamily: Work Sans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
  headline-xl-mobile:
    fontFamily: Work Sans
    fontSize: 26px
    fontWeight: '700'
    lineHeight: 34px
  headline-lg:
    fontFamily: Work Sans
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-md:
    fontFamily: Work Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Work Sans
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 26px
  body-lg-bold:
    fontFamily: Work Sans
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 26px
  body-md:
    fontFamily: Work Sans
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  cow-tag-display:
    fontFamily: JetBrains Mono
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 32px
  cow-tag-inline:
    fontFamily: JetBrains Mono
    fontSize: 18px
    fontWeight: '700'
    lineHeight: 22px
  label-md:
    fontFamily: Work Sans
    fontSize: 16px
    fontWeight: '500'
    lineHeight: 20px
  label-sm:
    fontFamily: JetBrains Mono
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 16px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-tablet: 1.25rem
  gutter-desktop: 1.5rem
  margin: 1rem
  margin-tablet: 1.5rem
  margin-desktop: 2rem
  space-xs: 0.375rem
  space-sm: 0.75rem
  space-md: 1.25rem
  space-lg: 1.75rem
  space-xl: 2.5rem
---

## Brand & Style

This design system is engineered specifically for ambulatory dairy veterinary practice in rural environments (stable blocks, milking parlors, low-connectivity sheds, and outdoor pens). The core design identity is rugged, utilitarian, and high-contrast, blending robust utilitarianism with uncompromising legibility under harsh or variable lighting conditions (glare, dust, moisture, and low-light barn corridors).

### Visual Principles
- **Utilitarian Clarity**: Zero ambient blur, zero translucent glassmorphism, and zero micro-details that wash out on ruggedized tablets or low-brightness displays.
- **Gloved-Hand Ergonomics**: Large physical touch targets (minimum 48–56px tap areas) with exaggerated mechanical affordances.
- **Clinical Reliability**: High semantic contrast that conveys state certainty immediately (e.g., synchronized vs. offline, treated vs. pending, protocol warnings).
- **Tactile Borders & Direct Planes**: Hierarchy is achieved through solid flat planes, high-contrast text ratios exceeding WCAG AAA, and distinct single-pixel structural strokes.

## Colors

The palette is tuned for field visibility and functional hierarchy. The dominant base is an off-white unbleached ecru (`#F7F8F5`), which eliminates mobile screen glare while preserving crisp separation against `#FFFFFF` elevated container cards.

### Core Swatches
- **Primary / Brand Action (`#14532D`)**: Deep Forest Green. Used for primary validation actions, active medical protocol confirmations, and healthy/completed herd states.
- **Neutral Primary / Text (`#17211B`)**: Dense Anthracite. Delivers near-maximum contrast (>14:1) against canvas and card surfaces for rapid outdoor reading.
- **Neutral Secondary / Caption (`#4B5563`)**: Slate Grey. Secondary metadata, timestamps, and physiological baseline parameters.
- **Canvas Base (`#F7F8F5`)**: Ecru off-white canvas.
- **Card & Surface (`#FFFFFF`)**: Pure white structured modules.
- **Structural Outlines (`#D1D5DB` default, `#9CA3AF` emphasis)**: High-contrast separators and element boundaries.
- **Warning / Protocol Caution (`#92400E` text/border on `#FEF3C7` background)**: Ochre alert for withdrawal periods (lait/viande), pending lab cultures, and heat tracking.
- **Critical / Emergency (`#B91C1C` text/border on `#FEE2E2` background)**: Severe pathology, toxemia warnings, and emergency treatment halts.
- **Offline / Sync State (`#78350F` / `#D97706` amber on neutral canvas)**: Discrete, persistent indicator guaranteeing visual certainty when connectivity drops in insulated steel barns.

## Typography

Typography prioritizes instantaneous parsing from an arm’s-length distance or mounted tablet cradle. 

- **Primary Typeface (`Work Sans`)**: Selected for neutral grotesque legibility, wide apertures, open counters, and high stability across harsh rendering engines.
- **Identifier Typeface (`JetBrains Mono`)**: Applied to cow ear-tag IDs (RFID/ATQ 8–15 digits), lactometer outputs, withdrawal day counters, and dosage values. Monospace geometry prevents optical misreadings between `0`, `O`, `8`, and `B` under dust and moisture.
- **Reading Baseline**: Running body text is set deliberately to `18px` with strict `26px` line-heights to eliminate optical strain in motion. Microtext under `13px` is forbidden.

## Layout & Spacing

The layout is built upon an adaptive columnar grid with resilient gutters and substantial margins.

### Breakpoints & Hierarchy
- **Mobile Handheld (<640px)**: 4-column layout, vertical single-stack flow, sticky bottom-docked quick-action bar for one-handed operation.
- **Rugged Tablet / Cradled (640px - 1024px)**: 8-column layout. Split view dividing the barn herd list on the left (40% width) and the animal’s active diagnostic examination card on the right (60% width).
- **Desktop / Clinic Station (>1024px)**: 12-column layout. Multi-column herd prescription and epidemiologic monitoring tables.

### Ergonomic Spacing Rules
- Interactive inputs and touch targets must preserve at least `space-sm` (12px) separation to prevent accidental adjacent activation with protective barn gloves.
- Edge boundaries adhere strictly to safe zones with padded margins protecting elements against edge swipes on armored tablet bumpers.

## Elevation & Depth

Visual depth is communicated exclusively through **flat tectonic layers and bold structural borders**, entirely eliminating diffuse Gaussian shadows that disappear under direct barn sunlight or daylight flushes.

### Layering Rules
- **Base Layer (Canvas)**: `#F7F8F5` surface.
- **Tier 1 (Cards, Modules, List Rows)**: `#FFFFFF` surface bounded by a continuous `1.5px solid #D1D5DB` border.
- **Tier 2 (Modals, Slide-over Intervention Sheets)**: `#FFFFFF` surface bounded by a `2px solid #9CA3AF` stroke. A hard non-blurred offset dropshadow (`4px 4px 0px rgba(23, 33, 27, 0.15)`) may be used to reinforce card separation.
- **Active / Focused Layer**: An intensified `2px solid #14532D` rim clearly flags the currently examined cow or active input field.

## Shapes

Shapes utilize minimal, disciplined rounding (`0.25rem` / `4px`) to reinforce industrial stability and prevent wasted tap canvas.

### Geometry Details
- **Buttons and Inputs**: Tight `4px` corner radius. This keeps the interactive silhouette crisp, predictable, and distinctly tactile.
- **Status Pills and Badges**: Max `4px` or fully rectangular with `2px` chamfer feel. Pill shapes (capsule radii) are avoided to maintain maximum area for tabular monospaced text.
- **Panels & Diagnostic Cards**: `4px` corner radius framed with structural line work.

## Components

### Buttons
- **Primary Button**: Solid `#14532D` background with `#FFFFFF` text. Minimum height `52px` (optimal `56px` in field mode). Border: `1px solid #14532D`.
- **Secondary / Action Button**: `#FFFFFF` background, `#17211B` text, `2px solid #D1D5DB` border. Hover/Active: `#F7F8F5` fill, `2px solid #17211B`.
- **Destructive Button**: Solid `#B91C1C` background with `#FFFFFF` text or `#FFFFFF` fill with `2px solid #B91C1C` and `#B91C1C` text.
- **Touch Target**: Any interactive icon button must have a bounded interactive bounding box of at least `48px x 48px`, even when the rendered glyph is `24px`.

### Cow Identifier Chips & Badges
- **Tag Badge**: White background with a distinct `1.5px solid #9CA3AF` border. Employs `JetBrains Mono` bold `18px` text. Format: `[CA 12 345 678]`.
- **Offline Mode Indicator**: Sticky pill in top app bar. Background: `#FEF3C7`, border: `1.5px solid #D97706`, text: `#78350F` (`13px` JetBrains Mono, uppercase: `HORS-LIGNE (SYNC EN ATTENTE)`).

### Input Fields & Rapid Keypads
- **Form Controls**: Minimum height `54px`. Background `#FFFFFF`, border `2px solid #D1D5DB`, text `#17211B` (`18px`).
- **Focus State**: `2px solid #14532D` with an offset outline of `2px solid #86EFAC`.
- **Quick Steppers & Numeric Entries**: Large `+` / `-` incremental buttons (minimum `56px x 56px`) flanking monospaced quantitative values for milk yield, temperature (°C), and dosage volume (ml).

### Checkboxes & Segmented Radios
- **Checkboxes**: Hard square (`24px x 24px`), border `2px solid #17211B`, filled with `#14532D` when selected. Tap envelope is padded to `48px x 48px`.
- **Segmented Protocol Selectors**: Full-width button toggles (e.g., `Gestation: POSITIVE / NÉGATIVE / DOUTEUSE`). Unselected: `#FFFFFF` with `#D1D5DB` borders; Selected: `#14532D` fill with crisp white bold text.

### Animal Health & Protocol Cards
- **Card Anatomy**: Pure white container `#FFFFFF`, padding `1.25rem`, border `1.5px solid #D1D5DB`.
- **Warning State Card (Retrait Lait / Viande)**: `#FEF3C7` background with a left accent border `6px solid #92400E`, accompanied by bold warning copy specifying exact withdrawal clearance dates.
- **Herd List Rows**: Alternating micro-separator (`1px solid #E5E7EB`), active row indicated by a left `4px solid #14532D` band and `#F7F8F5` background.

## Implémentation du socle visuel (CowCard)

La référence exécutable est [tokens.css](../../../src/TourneeVeto.Ui/wwwroot/tokens.css).
En cas de divergence entre les paragraphes et la palette YAML ci-dessus, les surfaces
vert pâle des six captures sont retenues (`#f1fcf2`, `#ebf7ed`, `#ffffff`).
Les tailles typographiques proviennent du guide, sans reproduire les microtextes
des captures : corps de carte 18 px, métadonnées au moins 13 px.
Work Sans et JetBrains Mono sont préférées si déjà installées ; aucune police
n'est fournie ou téléchargée. Les secours system-ui et Consolas garantissent
le fonctionnement hors ligne, avec des métriques légèrement différentes.

### Contraste AA

[DesignTokensTests](../../../tests/TourneeVeto.Tests/Ui/DesignTokensTests.cs)
vérifie les 70 paires autorisées, à partir du CSS réel, selon la luminance relative
WCAG (sRGB linéarisé), sans arrondir avant de comparer au seuil de 4,5:1.
Chaque `--color-on-*` s'utilise exclusivement sur son fond homonyme.
`--color-text` et `--color-text-muted` conviennent aux six surfaces claires.
Les couleurs sémantiques primaire, urgente, warning, ok, info, offline et error
sont également vérifiées comme texte sur chacune de ces six surfaces.
Le vert primaire assombri conserve le texte blanc au survol.
Les fonds de statut ne doivent pas être utilisés comme couleurs de texte.

Les observations et placeholders pâles des captures sont remplacés par
`--color-text-muted` (`#404941`). Les contours des champs et boutons utilisent
`--color-border-control` (`#717970`) au lieu du contour décoratif `#c0c9be` :
contraste non textuel supérieur à 3:1. Le focus sombre reste visible sur les
surfaces claires et, grâce au décalage de 3 px, autour des boutons verts.
L'urgence et l'état réalisé possèdent aussi un libellé textuel.

Les jetons `--font-size-label-sm` / `--line-height-label-sm` conservent les
métriques 13/16 px, `--font-size-label-md` / `--line-height-label-md` les
métriques 16/20 px et `--line-height-cow-inline` complète le style 18/22 px.
Les anciens jetons `--font-size-label` et `--line-height-label` restent
disponibles pour préserver les styles existants.

### Contrat et intégration

[CowCard](../../../src/TourneeVeto.Ui/Components/CowCard.razor) est une carte
de présentation : `Identifier` (sans préfixe #), `ActionTitle`, `ActionType` et
`Urgency` sont obligatoires. Les deux enums viennent de
[Domain](../../../src/TourneeVeto.Domain/Visits/ActionType.cs) ; la carte ne
calcule ni seuil CCS, ni échéance, ni urgence. Les règles de sélection des
actions restent à implémenter dans le domaine.

`EarTag` et `Protocol` sont facultatifs. `@bind-Note` et `@bind-IsCompleted`
permettent au parent de gérer les modifications ; la carte ne modifie pas ses
paramètres et ne prétend pas sauvegarder. Sans callback, le champ est en lecture
seule et le bouton désactivé. `Disabled` bloque également les interactions.
Le parent sera responsable de la persistance locale et de ses erreurs ; aucun
dépôt n'est injecté dans ce composant de présentation.

À 768 px : identifiant à gauche, observation à droite, bouton sous l'observation.
À partir de 1024 px : identifiant, observation et bouton sur trois colonnes,
comme la grille Web. Sous 640 px : une seule colonne. Les éléments se replient
sans texte tronqué. Minimum tactile global : 44 px ; bouton : 52 px ;
champ : au moins 54 px. Les styles restent isolés dans le `.razor.css`.

Le CSS global est chargé par l'hôte via
`_content/TourneeVeto.Ui/tokens.css`, avec une URL relative compatible avec
le sous-chemin GitHub Pages. Aucune page ni aucun autre composant de ces
maquettes n'est ajouté ; CowCard sera intégré lors de la réalisation de la grille.

Vérification navigateur sur le HTML généré par le composant Razor : largeurs
320, 375, 768, 1024 et 1440 px, sans débordement horizontal, boutons et champs
mesurés au-dessus de 44 px dans les six variantes (trois urgences × deux états).
Le contraste minimal mesuré sur les textes de ces cartes, survol compris, est
6,47:1 ; le focus clavier possède un contour visible de 3 px. Cette vérification
isolée ne constitue pas un audit Lighthouse de l'application ni un test du
parcours de persistance, qui restent à réaliser lorsque la grille sera intégrée.