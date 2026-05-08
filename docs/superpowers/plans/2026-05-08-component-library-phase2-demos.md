# Component Library — Phase 2 Implementation Plan (As-Is Demos)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `*Demo.razor` files for the 8 remaining existing components to the `/components` library gallery so every component is reachable from the sidebar with hand-curated variants. No primitive extraction — phase B step 2 only.

**Pilot already done:** `MoodLegendDemo.razor` (commit `678bac5`) establishes the per-component pattern. All 8 follow the same shape.

**Spec reference:** `docs/superpowers/specs/2026-05-07-component-library-design.md`, execution step 2.

**Branch / worktree:** `library/phase2-demos` at `.worktrees/library-phase2-demos`. Tip after pilot is `678bac5`.

---

## The pattern (copy from `MoodLegendDemo.razor`)

Every demo file has exactly this skeleton:

```razor
@namespace Fretless.Web.Client.Library.Demos.Components
@using Fretless.Web.Client.Models   <!-- only if the demo references model types -->

<LibraryFrame Title="<ComponentName>"
              Description="<one-sentence what it does + key parameters>"
              SourcePath="Fretless.Web/Fretless.Web.Client/Components/<ComponentName>.razor">

    <Variants>
        <div class="variant-card">
            <h3 class="variant-card__label"><variant-name></h3>
            <div class="variant-card__render">
                <ComponentName <params> />
            </div>
            <pre class="variant-card__code"><code>&lt;ComponentName <params> /&gt;</code></pre>
        </div>
        <!-- 2-3 more variant-cards -->
    </Variants>

    <Controls>
        <div class="playground__strip">
            <!-- buttons / selects driving _playground* fields, plus a "Current: ..." readout -->
        </div>
        <div class="playground__render">
            <ComponentName @bind-<TwoWayParam>="_playgroundValue" />
        </div>
    </Controls>
</LibraryFrame>

@code {
    private <Type>? _playgroundValue;
}
```

Then add **one** entry to `LibraryRegistry.All` keyed off `typeof(<ComponentName>Demo)`. The `using Fretless.Web.Client.Library.Demos.Components;` directive is already at the top of `LibraryRegistry.cs` from the pilot.

**Size budget per spec:** ~50–120 LOC, hard cap 150. If a demo balloons, the variants are too clever — pick simpler ones.

**Build verification per task:** `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`. Expect 0 errors.

**Commit message convention:** `library(phase2): <Component> demo`.

---

## Conventions used by every task below

- All paths repo-root relative; commands run from worktree root.
- After each demo: build, then commit only the new demo file + the registry update.
- Don't add `*.razor.css` files for demos — the variant-card / playground styles are supplied by `LibraryFrame.razor.css` via `::deep`.
- If a component requires services that aren't easily faked in a static variant, render it in the playground only and skip the variant cards for that aspect.
- If choosing variants is non-obvious, default to: (a) "minimal/empty state", (b) "common configured state", (c) "edge case" (max content, all selected, etc.).

---

## Task 1: ChordBuilderPanel demo (~87 LOC component)

**Parameters:** `ActiveChord` (`NoteChord?`), `BuiltNotes` (`List<string>`), `NoteRemoved` (`EventCallback<string>`).

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/Demos/Components/ChordBuilderPanelDemo.razor`
- Modify: `Fretless.Web/Fretless.Web.Client/Library/LibraryRegistry.cs`

- [ ] **Step 1: Read `ChordBuilderPanel.razor`** to confirm the parameter shape and what an empty vs populated panel looks like (chip rendering, "active chord" badge, etc.).

- [ ] **Step 2: Write the demo** with three variants:
  - "Empty" — `<ChordBuilderPanel BuiltNotes="@(new List<string>())" />`
  - "With built notes" — pass a list like `["C", "E", "G"]`
  - "With ActiveChord" — pass a `NoteChord` constructor (look up the type) plus the same built notes

  Playground: a single MoodLegend-style strip with an "Add note" / "Remove last" button pair, driving `_playgroundNotes` (`List<string>`).

- [ ] **Step 3: Register**

```csharp
new(
    Slug: "chord-builder-panel",
    Title: "ChordBuilderPanel",
    Description: "Built-up chord chip row with optional active-chord badge.",
    Group: LibraryGroup.Components,
    DemoType: typeof(ChordBuilderPanelDemo),
    SourcePath: "Fretless.Web/Fretless.Web.Client/Components/ChordBuilderPanel.razor"),
```

- [ ] **Step 4: Build** → `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj` → 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/Demos/Components/ChordBuilderPanelDemo.razor Fretless.Web/Fretless.Web.Client/Library/LibraryRegistry.cs
git commit -m "library(phase2): ChordBuilderPanel demo"
```

---

## Task 2: ChordPairings demo (~82 LOC component)

**Parameters:** `ActiveChord` (`NoteChord?`), `ChordSelected` (`EventCallback<NoteChord>`).

**Files:**
- Create: `Library/Demos/Components/ChordPairingsDemo.razor`
- Modify: `LibraryRegistry.cs`

- [ ] **Step 1: Read `ChordPairings.razor`** to understand what "pairings" display when ActiveChord is set vs null.

- [ ] **Step 2: Write demo** with three variants:
  - "No active chord" — `<ChordPairings />`
  - "With C major active" — pass a NoteChord built for C major
  - "With minor chord active" — pass a different NoteChord (e.g., A minor)

  Playground: a "Selected chord:" readout + reset button. Bind to `OnChordSelected` so clicking a paired chip updates the readout.

- [ ] **Step 3-5:** Register, build, commit (`library(phase2): ChordPairings demo`).

---

## Task 3: HarmonicStabilityDisplay demo (~178 LOC component)

**Parameters:** `Notes` (`IReadOnlyList<string>`).

**Files:** create demo, modify registry.

- [ ] **Step 1: Read `HarmonicStabilityDisplay.razor`** to see the SVG cluster and how stability scoring varies with note count.

- [ ] **Step 2: Write demo** with four variants (this one earns four because the SVG is the whole point):
  - "Empty (no notes)"
  - "Triad — major (C E G)"
  - "Triad — diminished (C Eb Gb)"
  - "Tense cluster (C C# D)"

  No playground needed — the variant cards already cover the visual range. Or include a single "Type a comma-separated note list" input → splits into a list → renders. Optional.

- [ ] **Step 3-5:** Register, build, commit.

---

## Task 4: TuningSelector demo (~144 LOC component)

**Parameters:** `Tuning` (`TuningPreset`, default `TuningService.Standard`), `TuningChanged` (`EventCallback<TuningPreset>`), `OnDone` (`EventCallback`), `Inline` (`bool`, default `false`).

**Files:** create demo, modify registry.

- [ ] **Step 1: Read `TuningSelector.razor`** + `TuningService.cs` to know the available presets.

- [ ] **Step 2: Write demo** with three variants:
  - "Standard E tuning"
  - "Drop D"
  - "Inline mode" — `<TuningSelector Inline="true" />` (different layout per the `Inline` flag)

  Playground: dropdown of presets (use `Enum.GetValues<TuningPreset>()` if it's an enum, or `TuningService.Presets` if a list); show selected preset's MIDI notes below.

- [ ] **Step 3-5:** Register, build, commit.

---

## Task 5: ScaleSelector demo (~105 LOC component)

**Parameters:** `ActiveOverlay` (`ScaleOverlay?`), `ActiveOverlayChanged` (`EventCallback<ScaleOverlay?>`).

**Files:** create demo, modify registry.

- [ ] **Step 1: Read `ScaleSelector.razor`** + `ScaleOverlay` model.

- [ ] **Step 2: Write demo** with three variants:
  - "No overlay"
  - "Major scale overlay" — pick a sensible default (e.g., C major)
  - "Minor scale overlay" — a different scale

  Playground: `<ScaleSelector @bind-ActiveOverlay="_playgroundOverlay" />` + readout of which overlay is currently selected.

- [ ] **Step 3-5:** Register, build, commit.

---

## Task 6: ChordWheel demo (~293 LOC component)

**Parameters identified:** `OnChordSelected` (`EventCallback<NoteChord?>`). May have more — read the source to confirm.

**Risk:** ChordWheel injects services (probably `MidiService`, `IJSRuntime` for audio). The demo just renders `<ChordWheel />`; the services are supplied via DI as in the rest of the app. Don't try to mock them.

**Files:** create demo, modify registry.

- [ ] **Step 1: Read `ChordWheel.razor`** end-to-end to identify all parameters and any internal state worth showing.

- [ ] **Step 2: Write demo** with three variants:
  - "Default" — `<ChordWheel />`
  - "With a chord pre-selected" — if there's an `ActiveChord` or similar input parameter, set it
  - "Compact / specific configuration" — if no other levers, drop the third variant and rely on the playground

  Playground: just `<ChordWheel OnChordSelected="@(c => _playgroundChord = c)" />` with a "Selected:" readout.

- [ ] **Step 3-5:** Register, build, commit.

---

## Task 7: Fretboard demo (~487 LOC component)

**Parameters:** `ActiveChord` (`NoteChord?`), `Tuning` (`TuningPreset?`), `TuningChanged` (`EventCallback<TuningPreset>`). Read source for the rest (likely scale, position, MIDI active state).

**Files:** create demo, modify registry.

- [ ] **Step 1: Read `Fretboard.razor`** end-to-end.

- [ ] **Step 2: Write demo** with three variants:
  - "Default empty board" — `<Fretboard />`
  - "With a chord highlighted" — pass `ActiveChord`
  - "Drop D tuning" — pass `Tuning="TuningService.DropD"` (or whatever the preset is named)

  Playground: a tuning-preset select + active-chord button row driving `_playgroundChord` and `_playgroundTuning`. Show the current state in a strip.

- [ ] **Step 3-5:** Register, build, commit.

---

## Task 8: SpiralWheel demo (~564 LOC component)

**Parameters identified:** none directly via `[Parameter]`. SpiralWheel is self-contained: it loads its own state from `localStorage` under `fretless.spiralWheel.settings.v1`. Read the source to confirm there's no parameter surface.

**Risk note from spec:** "SpiralWheel.razor is the highest-risk target." That risk applies to **primitive extraction** (phase 4), not to adding an as-is demo. The demo here is a thin wrapper that just renders `<SpiralWheel />`.

**Files:** create demo, modify registry.

- [ ] **Step 1: Read `SpiralWheel.razor`** to confirm it has no parameters and to write an accurate `Description`.

- [ ] **Step 2: Write demo** with one or two variants — the demo collapses if no parameters drive variation:
  - "Default" — `<SpiralWheel />`
  - (Optional) A second variant only if there's a meaningful constructor / DI override worth showing. Otherwise stop at one variant.

  No playground (nothing to drive). Description should call out that interaction state lives in localStorage and is shared with the home page — clicking notes here will affect `/`.

- [ ] **Step 3-5:** Register, build, commit.

---

## Task 9: Format check + build verify

- [ ] **Step 1:** `dotnet format Fretless.Web/Fretless.Web/Fretless.Web.csproj --verify-no-changes` → exit 0. If it reports changes, run without `--verify-no-changes`, commit as `library(phase2): dotnet format pass`.

- [ ] **Step 2:** `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj` → 0 errors. (Pre-existing 18 warnings about NuGet CVEs and unused vars are fine.)

- [ ] **Step 3:** `dotnet test Fretless.Tests/Fretless.Tests.csproj --no-build` → 48 passed. Demo additions shouldn't change test counts.

---

## Task 10: Manual smoke test

Launch from the worktree root:

```powershell
dotnet run --project Fretless.Web/Fretless.Web/Fretless.Web.csproj --urls=http://localhost:5520
```

In a browser, visit `http://localhost:5520/components` and verify:

- [ ] Sidebar's **Components** group lists all 9 entries (MoodLegend + the 8 from this plan), in registry order.
- [ ] Click each entry. Each demo renders title, description, source path, 1–4 variant cards with code blocks, and (where present) a working playground.
- [ ] Deep-link works: `http://localhost:5520/components#components/<slug>` selects the entry on hard reload.
- [ ] No console errors in DevTools across all 9 demos.
- [ ] `/` (Home → SpiralWheel) still loads and behaves identically — phase 2 must not regress the main feature page.

Stop the dev server (Ctrl+C in the terminal or via TaskStop if launched in background).

---

## Task 11: Final pass — merge to master

- [ ] **Step 1:** From worktree, confirm clean status: `git status --short` empty.

- [ ] **Step 2:** Switch to master worktree:

```powershell
cd C:/Users/tpeas/source/repos/PeasuDev/Nautilus
git status --short      # should be clean unless other WC work landed
```

- [ ] **Step 3:** Fast-forward merge:

```powershell
git merge --ff-only library/phase2-demos
```

If FF refuses, master has diverged — surface the situation, don't auto-resolve.

- [ ] **Step 4:** Push: `git push origin master`.

- [ ] **Step 5:** Cleanup:

```powershell
git worktree remove .worktrees/library-phase2-demos
git branch -d library/phase2-demos
```

---

## Definition of done for Phase 2

- All 9 components have a `*Demo.razor` in `Library/Demos/Components/`.
- `LibraryRegistry.All` has 9 entries, all under `LibraryGroup.Components`.
- `dotnet build` clean, `dotnet format --verify-no-changes` clean, 48/48 tests pass.
- Sidebar lists all 9; each demo deep-links and renders without console errors.
- Master FF-merged and pushed; worktree + branch removed.

## What this plan deliberately does not do

- No primitive extraction (phase 3 atoms, phase 4 primitives).
- No new components, no behavior changes inside existing components.
- No Tokens group population (phase 5).
- No CSS migration to design tokens (phase C).
- No tests for the demos themselves — manual verification at task 10 is the gate per spec.
- No iteration on the LibraryFrame chrome unless a demo reveals a bug; if a demo cannot be expressed within the existing slots, file it as a follow-up rather than redesigning the frame mid-plan.
