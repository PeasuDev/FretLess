# Fretless Component Library — Design

**Date:** 2026-05-07
**Status:** Spec — phase B (catalog + primitive extraction). Phase C (design tokens + CSS migration) is deferred to a separate spec, to be brainstormed after this phase ships.

## Problem

The Blazor client has grown ten substantial UI components, but several issues have accumulated:

- The standalone `Fretboard.razor` (12-fret SVG with full controls) and the mini-fretboard inline SVG inside `SpiralWheel.razor` lines 359–414 are visually similar but maintained separately. They have already drifted.
- The mini-piano inline SVG inside `SpiralWheel.razor` lines 461–506 is similarly ad-hoc; there is no standalone keyboard component.
- `SpiralWheel.razor` is over 520 lines and mixes the spiral SVG, instrument cards, position selector, step-pattern chips, settings panels, and persistence logic.
- There is no single place where every UI element can be viewed in isolation, exercised, and visually compared. Inconsistencies in spacing, color use, label sizing, and hover/focus treatment are easy to introduce and hard to spot.
- Per-component CSS lives in `*.razor.css` files with no shared tokens. (Token migration is phase C, but the lack of a central reference makes phase B's gallery valuable in itself.)

## Goal

Introduce a `/components` page — linked from the main nav as **"Library"** — that serves as a living gallery of every UI element in the app, then extract the genuine duplicates into reusable atoms and primitives so the visual layer is modular and consistent.

The gallery is the unit of "done": every atom, primitive, and component is reachable from the sidebar and renders correctly. Phase C (CSS tokens) is deferred but the Tokens section is built read-only in this phase to make the existing palette legible.

## Architecture

### Three-tier decomposition, enforced by folder location

```
Fretless.Web.Client/
  Components/
    Atoms/                    ← stateless, ~20–60 LOC each
      NoteChip.razor
      MoodChip.razor
      RomanNumeralButton.razor
      PositionPill.razor
      StringTuningRow.razor
      StepChip.razor
      ScaleNoteChip.razor
      IconButton.razor        ← unified ⚙ / ✕ button (Variant="settings|remove")
    Primitives/               ← pure SVG renderers; no IJSRuntime, no MidiService
      FretboardView.razor
      KeyboardView.razor
      NoteCircleView.razor
      SpiralView.razor
      StabilityCluster.razor
      MotionGraphicsCanvas.razor   ← stays as a component (injects IJSRuntime)
    (existing high-level components remain in Components/)
    Fretboard.razor
    ChordWheel.razor
    SpiralWheel.razor
    ChordBuilderPanel.razor
    ChordPairings.razor
    HarmonicStabilityDisplay.razor
    MoodLegend.razor
    TuningSelector.razor
    ScaleSelector.razor
  Library/                    ← the gallery feature
    ComponentLibraryPage.razor      (the /components route)
    LibrarySidebar.razor
    LibraryEntry.razor
    LibraryFrame.razor              (visual chrome shared by all demos)
    LibraryRegistry.cs              (data-driven entry list)
    Demos/
      Atoms/        (one *Demo.razor per atom)
      Primitives/   (one *Demo.razor per primitive)
      Components/   (one *Demo.razor per existing component)
      Layout/       (NavMenuDemo, ReconnectModalDemo)
```

### Boundary rules

- **Atoms** never import another `Components/*` file. Pure markup, parameters, a tiny bit of CSS.
- **Primitives** are stateless renderers given parameters. State (selection, MIDI active, playing note) comes in as parameters; clicks come out as `EventCallback`s. They MUST NOT inject `MidiService` or `NavigationManager`, MUST NOT read or write `localStorage`, MUST NOT play audio, and MUST NOT persist or compute domain state across renders. They MAY inject `IJSRuntime` if and only if the rendering surface itself requires it (i.e. canvas primitives like `MotionGraphicsCanvas`). SVG-based primitives never need `IJSRuntime` and must not inject it.
- **Components** are the existing ones. They inject services, hold state, persist to localStorage, and compose primitives. Once a primitive exists for a shape, no component inlines that shape's SVG again.

### Behavior preservation rule

Every refactor in phase B is a like-for-like extraction. Audio, MIDI hookups, localStorage persistence, animation timing, click handlers, scale highlighting, position selection — none of that moves or changes. The git diff for each component should read as "delete inline SVG, replace with primitive call passing the same parameters." If extracting a primitive surfaces a bug, file it; do not fix it in the same PR.

## Inventory

### Atoms

| Name | Replaces inline markup in | Key params |
|---|---|---|
| `NoteChip` | `ChordBuilderPanel` `.note-chip`, `ChordPairings` chip content | `Note`, `Color`, `Removable`, `OnRemove` |
| `MoodChip` | `MoodLegend` `.mood-item` | `Mood`, `Color`, `Active`, `OnToggle` |
| `RomanNumeralButton` | `Fretboard` position buttons; `SpiralWheel` overlay-position buttons | `Numeral`, `Active`, `OnClick` |
| `PositionPill` | "Positions" / "Steps" labels with chips | `Label`, `ChildContent` |
| `StringTuningRow` | `TuningSelector` per-string row | `StringNum`, `MidiNote`, `Gauge`, `OnChanged` |
| `StepChip` | `SpiralWheel` `.spiral-step-chip` | `Text` (W/H) |
| `ScaleNoteChip` | `ScaleSelector` + `SpiralWheel` scale-note chips | `Note`, `IsRoot`, `IsPlaying` |
| `IconButton` | ⚙ / ✕ icon buttons in `SpiralWheel` instrument cards | `Icon`, `Variant` (settings/remove), `Active`, `Title`, `OnClick` |

### Primitives

| Name | Replaces SVG in | Inputs (highlights) | Outputs |
|---|---|---|---|
| `FretboardView` | `Fretboard.razor` body; mini-fretboard inside `SpiralWheel.razor` | `Tuning`, `FretCount`, `HighlightedSemitones`, `RootSemitone?`, `PlayingSemitone?`, `MidiActiveMidiNotes`, `Compact` | `OnFretClicked(stringIdx, fret)` |
| `KeyboardView` | mini-piano inside `SpiralWheel.razor` | `HighlightedSemitones`, `RootSemitone?`, `PlayingSemitone?`, `ShowLabels`, `OctaveCount` | `OnKeyClicked(semitone)` |
| `NoteCircleView` | SVG body of `ChordWheel.razor` | `SelectedNote?`, `KeyRoles?`, `MidiActiveSemitones`, `ShowComplementary` | `OnNoteClicked(note)` |
| `SpiralView` | SVG body of `SpiralWheel.razor` | `Zoom`, `Rotation`, `SelectedNotes`, `SelectedKey?`, `SelectedScaleNotes?`, `MidiActiveSemitones`, `PlayingScaleNote?` | `OnNoteClicked(subdivisionIndex, cycle?)` |
| `StabilityCluster` | SVG body of `HarmonicStabilityDisplay.razor` | `Notes`, `Stability`, `ViewSize` | (display only) |
| `MotionGraphicsCanvas` | (existing — already correctly shaped) | (existing) | (existing) |

### Components

Each existing component is kept and refactored to compose primitives:

| Component | Change |
|---|---|
| `Fretboard` | Replace inline SVG with `<FretboardView .../>`; keep tuning, audio, scale, position, MIDI UI |
| `ChordWheel` | Replace inline circle SVG with `<NoteCircleView .../>` |
| `SpiralWheel` | Replace mini-fretboard with `<FretboardView Compact />`; replace mini-piano with `<KeyboardView Compact />`; replace spiral with `<SpiralView />` |
| `HarmonicStabilityDisplay` | Replace inline cluster SVG with `<StabilityCluster .../>` |
| `ChordBuilderPanel` | Use `<NoteChip />` |
| `ChordPairings` | Use `<NoteChip />` for chip content |
| `MoodLegend` | Use `<MoodChip />` |
| `TuningSelector` | Use `<StringTuningRow />` |
| `ScaleSelector` | Use `<ScaleNoteChip />` |

## Gallery page

### Routing & nav

- New page `Library/ComponentLibraryPage.razor` with `@page "/components"`.
- New `<NavLink>` in `NavMenu.razor` after "Explore Scales", labelled **"Library"**, no icon.
- URL hash carries selection: `/components#primitives/fretboard-view`. Hash is parsed in `OnInitialized` and on `LocationChanged`. Clicking a sidebar entry pushes the new hash via `NavigationManager.NavigateTo(..., replace: true)`.

### Layout

Sidebar + detail pane (option A from brainstorming):

- **Sidebar (`LibrarySidebar.razor`):** Five top-level groups — **Atoms**, **Primitives**, **Components**, **Layout**, **Tokens**. Entries are data-driven from `LibraryRegistry.cs` (one `LibraryEntry` record per demo: `Slug`, `Title`, `Group`, `DemoType` for `<DynamicComponent>`, `Description`). Adding a new component is one new line in the registry plus one new `*Demo.razor` file.
- **Detail pane (`LibraryEntry.razor`):** Renders a `<DynamicComponent Type="@entry.DemoType" />`. Active entry highlighted with the gold accent.

### Per-component demo file pattern

Each demo file uses a shared `<LibraryFrame>` helper for visual chrome and contains:

1. **Title block:** title, one-line description, link to the source file path (relative to repo root, displayed as text — no live link needed).
2. **Variants section:** 3–5 hand-curated named variants. Each variant is a labelled card that renders the component plus a `<code>` block showing the markup that produced it.
3. **Controls section** (optional, included where interaction matters): a small horizontal strip of inputs hand-wired for that component. Editing a control re-renders the component in a "playground" card *below* the named variants — the named variants stay fixed.

Demo file size budget: target ~50–120 LOC each. If a demo balloons past 150 LOC the variants are too clever; break them up or push interactivity into the playground card.

### Tokens group (read-only)

A static page summarizing the design surface as it exists today:

- Mood→color swatches, sourced from `MusicTheoryService.MoodColors`.
- Note-hue swatches, sourced from `MusicTheoryService` (12 hues at 30° intervals).
- Accent colors (gold `#FFD700`, cyan `#00e5ff`, white selection, dark background palette).
- Font stack (`'Helvetica Neue', Helvetica, Arial, sans-serif`).
- Standard SVG label sizes used across components.

No CSS migration in this phase — the page just makes the visual inconsistencies legible, which is the goal.

## Execution order

Each step is independently shippable; each leaves the app green.

1. **Scaffold the gallery shell.** `ComponentLibraryPage`, `LibrarySidebar`, `LibraryEntry`, `LibraryFrame`, `LibraryRegistry` (empty). "Library" `<NavLink>` in `NavMenu`. Page renders a "select a component" placeholder. Verifiable end-to-end before any component work.
2. **Add demo files for existing components as-is.** No primitive extraction yet — each existing component (`Fretboard`, `ChordWheel`, `SpiralWheel`, `ChordBuilderPanel`, `ChordPairings`, `HarmonicStabilityDisplay`, `MoodLegend`, `TuningSelector`, `ScaleSelector`) gets a `*Demo.razor` with 2–3 variants and a controls strip. Visual baseline before any refactor.
3. **Extract atoms, one per PR.** `NoteChip`, `MoodChip`, `RomanNumeralButton`, `ScaleNoteChip`, `StepChip`, `PositionPill`, `StringTuningRow`, `IconButton`. After each, register the atom's demo and verify all consuming components render identically.
4. **Extract primitives, one per PR.** `FretboardView` (consumed by `Fretboard` then by `SpiralWheel`); `KeyboardView` (`SpiralWheel`); `NoteCircleView` (`ChordWheel`); `SpiralView` (`SpiralWheel`); `StabilityCluster` (`HarmonicStabilityDisplay`). Each adds the primitive's demo with named variants ("default", "with selection", "with MIDI active").
5. **Tokens reference page.** Read-only swatches and type samples sourced from `MusicTheoryService` and existing CSS.
6. **Final pass.** Sidebar entries cleaned up, every component reachable, deep-link URLs verified, `dotnet format` + `./scripts/verify.ps1` clean.

## Verification per step

After each step:

- `./scripts/verify.ps1` (restore, format-verify, build, test).
- `./scripts/review.ps1` and visually confirm:
  - The original feature pages (`Home` → `SpiralWheel`) look and behave identically (selection, audio, MIDI, localStorage persistence, scale playback).
  - The gallery `/components` page renders the affected entries without errors.
  - Deep-linking via URL hash works for the affected entries.

There is no test project today; manual verification is the only gate. Don't claim "tests pass" as evidence of anything beyond build success.

## Risks

- **`SpiralWheel.razor` is the highest-risk target.** It's the main feature page, has localStorage persistence under `fretless.spiralWheel.settings.v1`, MIDI hookups, and complex zoom/rotation state. The three primitive swaps (`FretboardView`, `KeyboardView`, `SpiralView`) MUST be three separate PRs with manual verification between them — not one mega-PR.
- **`IconButton` unification across `Variant="settings"` and `Variant="remove"`** is a small judgment call during implementation. Settings has an `Active` highlight state; remove does not. If a single atom with a `Variant` parameter feels forced, splitting into `SettingsToggleButton` and `RemoveButton` is acceptable.
- **The `Compact` flag on `FretboardView` and `KeyboardView`** is the only behavioral knob added during extraction. Today the standalone vs. mini variants differ in dimensions and label size; `Compact` switches between those two modes. Any other shape difference must come in as additional named parameters — never via internal branching on a flag with side effects.

## Out of scope (deferred to phase C)

A separate spec/plan will cover:

- Central `tokens.css` with CSS custom properties for color, spacing, typography, and the mood palette.
- Migration of component-scoped `*.razor.css` files to use those tokens.
- Renaming of CSS classes for cross-component consistency.
- Visual polish / unifying the look-and-feel inconsistencies the gallery surfaces.

Also out of scope for this spec, with no current plan to address:

- New components or new behaviors. (If a primitive extraction reveals a bug, file it; don't fix it in the same PR.)
- A generic Storybook-style Args/Controls framework. We hand-write per-component controls.
- Automated tests beyond `./scripts/verify.ps1` (no test project exists today; the existing pattern is manual review via `./scripts/review.ps1`).
