# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project shape

Fretless is a music-theory / chord / fretboard visualizer built as a **Blazor United** app on .NET 10, orchestrated by .NET Aspire. The solution file is `Fretless.slnx` (the new XML solution format) — `Fretless.Web/Fretless.Web.sln` is a legacy/secondary file; prefer `Fretless.slnx` for tooling.

Solution projects:
- `Fretless.Web/Fretless.Web` — ASP.NET Core host. Serves the Blazor app, exposes JS files, uses `AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()`.
- `Fretless.Web/Fretless.Web.Client` — Blazor WebAssembly project. **This is where almost all UI and music-theory logic lives.** Components, pages, layouts, models, and services (`MusicTheoryService`, `TuningService`, `MidiService`) are here.
- `Fretless.AppHost` — Aspire AppHost; only registers `fretless-web` as a project resource.
- `Fretless.ServiceDefaults` — shared Aspire defaults (OpenTelemetry, health checks, service discovery, resilience). Wired via `builder.AddServiceDefaults()` in `Fretless.Web/Program.cs`.
- `Fretless.Tests` — xUnit test project covering `Fretless.Web.Client` (currently `MusicTheoryServiceTests.cs`). Targets `net10.0`, references the client project. Run with `dotnet test Fretless.Tests/Fretless.Tests.csproj`.

`MauiApp1/` is a separate MAUI project **not in `Fretless.slnx`** and unrelated to the web app's build — ignore it unless explicitly working on MAUI.

## Render mode and where logic runs

`App.razor` mounts `<Routes @rendermode="InteractiveWebAssembly" />`, so pages and components execute in the browser via WASM. The server project is essentially a static host plus JS interop endpoints. When adding components, put them under `Fretless.Web.Client/Components/` (or `Pages/` / `Layout/`), not under `Fretless.Web/Components/`.

`MidiService` is registered as a singleton in *both* `Program.cs` files — it must be available on both server (prerender) and client (interactive) sides. Keep that pattern when adding shared services.

## JS interop layout

- `Fretless.Web/wwwroot/js/fretlessAudio.js` — Web Audio synthesizer, loaded by `App.razor` for all pages.
- `Fretless.Web.Client/wwwroot/js/motionGraphics.js` — canvas/animation helpers loaded from client components.
- MIDI input bridges through `MidiService.cs` ↔ `window.fretlessMidi` (Web MIDI API).

User-facing settings (e.g. spiral wheel state) persist in localStorage under `fretless.spiralWheel.settings.v1`. The Reset button in `MainLayout.razor` removes that key and force-reloads.

## Music-theory domain (read before editing components)

`Fretless.Web.Client/MusicTheoryService.cs` is the canonical source for:
- Circle-of-fifths ordering and note-hue mapping (`NoteHue` = `cofIndex * 30°`).
- Chord types, moods, mood→color, mood→mode label.
- Diatonic chord generation (`GetDiatonicChords`, `GetKeyRelationships`) and chord pairing (`GetPairedChords`).
- Just-intonation interval ratios + arc weights (used by SpiralWheel ratio mode).
- Harmonic stability scoring (pairwise consonance averaging).

`TuningService.cs` defines tuning presets as MIDI-number arrays and the audible MIDI range (16–135). Add new tunings here, not in components.

When components need note↔color, note↔degree, or chord-quality data, **call into these services** rather than recomputing — multiple components share the same mappings and will drift if duplicated.

## Common commands

The repo expects PowerShell on Windows. Bash is also available, but the helper scripts are `.ps1`. Run from repo root unless noted.

Build / verify / test:
```powershell
./scripts/verify.ps1                  # restore, format-verify, build, test
./scripts/verify.ps1 -SkipTests       # skip tests
./scripts/verify.ps1 -Clean           # also wipe bin/obj first
dotnet build Fretless.slnx
dotnet test  Fretless.slnx
dotnet format Fretless.slnx --verify-no-changes
```

Run the app:
```powershell
./fretless.ps1                        # dotnet run on Fretless.Web (http profile)
./fretless.ps1 -Profile https
./fretless.ps1 -Aspire                # run via AppHost (Aspire dashboard)
./fretless.ps1 -NoBrowser
./scripts/review.ps1                  # verify, then launch for human testing
```

To get the `fret` / `fretless` shell aliases globally, run `Install-FretlessCommand.ps1` once (registers functions in `$PROFILE`).

Start a new feature branch (kills running Fretless.Web processes, switches/pulls base, restores):
```powershell
./scripts/start-work.ps1 <branch-name>            # base = master
./scripts/start-work.ps1 <branch> -BaseBranch X
```

Aspire watch mode is available via the `https (watch)` launch profile in `Fretless.AppHost/Properties/launchSettings.json` (sets `DOTNET_WATCH=1`, `--watch`).

`Fretless.Tests` (xUnit, in the solution) covers `MusicTheoryService` today. Run via `dotnet test Fretless.Tests/Fretless.Tests.csproj` or `./scripts/verify.ps1` (which runs them as part of the verify pipeline). UI/component behavior still relies on manual verification per `scripts/review.ps1` — passing tests confirm the music-theory math, not feature correctness in the browser.

## Branching and deploy

- Local default branch is `master`.
- `.github/workflows/deploy.yml` deploys to Azure Web App on push to **`main`** (note the name mismatch with `master`). The deployed project is `Fretless.Web/Fretless.Web/Fretless.Web.csproj`. Don't push the AppHost or ServiceDefaults expecting them to deploy — only the web project is published.

## Working preferences (from `.github/copilot-instructions.md`)

- Implement incrementally and confirm intent of each feature before moving to the next.
- SpiralWheel dropdowns should be plain white with black text.
- Keep current progression branch labels unchanged unless asked.
- For new spiral instrument overlays (guitar/piano graphics), keep them display-only — defer custom scale editing to a later phase.

## Gotchas

- `Fretless.Web.csproj` has a hard-coded `Content Include` to `C:\Users\tpeas\OneDrive\Documents\Nautilus\Nautilus App.css`. This is a machine-specific path; build will warn/fail on other machines until that line is removed or the file exists. Don't reintroduce it elsewhere.
- The Razor `_Imports.razor` already brings in `Fretless.Web.Client.Components`, `Layout`, and `Models`, so new components in those namespaces don't need `@using` lines in callers.
- `BlazorDisableThrowNavigationException` is set on both web projects — don't rely on `NavigateTo` throwing.
