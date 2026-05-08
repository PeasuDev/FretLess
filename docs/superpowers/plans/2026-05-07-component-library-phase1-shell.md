# Component Library — Phase 1 Implementation Plan (Gallery Shell)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold an empty `/components` gallery page reachable via a "Library" link in the main nav, with sidebar navigation, URL hash deep-linking, and a registry-driven entry list ready to receive component demos in Phase 2.

**Architecture:** New `Library/` folder in `Fretless.Web.Client`. Five tiny, focused files: a `LibraryEntry` record, a static `LibraryRegistry` (initially empty), three Razor components (`LibraryFrame`, `LibrarySidebar`, `LibraryEntry`), and one Razor page (`ComponentLibraryPage`). The page renders the sidebar + a `<DynamicComponent>` for the selected entry. Selection is driven by URL hash (`/components#primitives/fretboard-view`) so links are shareable and survive reloads.

**Tech Stack:** .NET 10 Blazor WebAssembly, `<DynamicComponent>`, `NavigationManager.LocationChanged`, scoped `*.razor.css` files (matching the project's existing styling pattern).

**Spec reference:** `docs/superpowers/specs/2026-05-07-component-library-design.md`, sections "Gallery page" and execution step 1.

**Phase 1 scope:** Shell only. The registry is empty — no demos exist yet. Visiting `/components` shows the sidebar with empty groups and a "select a component" placeholder. Phase 2 (a separate plan) populates the registry with as-is demos for existing components.

---

## File map

**Create:**
- `Fretless.Web/Fretless.Web.Client/Library/LibraryEntry.cs` — record describing one gallery entry
- `Fretless.Web/Fretless.Web.Client/Library/LibraryGroup.cs` — enum for the five top-level groups
- `Fretless.Web/Fretless.Web.Client/Library/LibraryRegistry.cs` — static, empty list of entries
- `Fretless.Web/Fretless.Web.Client/Library/LibraryFrame.razor` + `.razor.css` — visual chrome wrapper used by future demo files
- `Fretless.Web/Fretless.Web.Client/Library/LibrarySidebar.razor` + `.razor.css` — left column
- `Fretless.Web/Fretless.Web.Client/Library/LibraryDetail.razor` + `.razor.css` — right pane (renders the selected entry's demo via `<DynamicComponent>`)
- `Fretless.Web/Fretless.Web.Client/Library/ComponentLibraryPage.razor` + `.razor.css` — the `/components` page

**Modify:**
- `Fretless.Web/Fretless.Web.Client/_Imports.razor` — add `@using Fretless.Web.Client.Library` so razor files don't need explicit imports
- `Fretless.Web/Fretless.Web.Client/Layout/NavMenu.razor` — add the "Library" `<NavLink>`

**No changes outside `Fretless.Web.Client`.** Server project, AppHost, ServiceDefaults are untouched.

---

## Conventions used by every task below

- All paths are repo-root relative.
- Run commands from the repo root (`C:/Users/tpeas/source/repos/PeasuDev/Nautilus`).
- After code changes, the verification step is: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`. (We target the web server csproj rather than `Fretless.slnx` because the solution includes `MauiApp1` which requires the `maui-android` workload, not installed in this environment. Building the web server transitively builds the client, which is the only project Phase 1 changes.) Expected: `Build succeeded`. There is no test project; manual verification at the end of the plan is the integration gate (per spec).
- Commit messages follow the existing repo style (lowercase, type prefix optional). Each task ends with one commit.
- Disable line-ending warnings: the spec doc commit already triggered `LF will be replaced by CRLF` warnings. They are benign and should be ignored.

---

## Task 1: Create the `LibraryGroup` enum

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryGroup.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace Fretless.Web.Client.Library;

public enum LibraryGroup
{
    Atoms,
    Primitives,
    Components,
    Layout,
    Tokens,
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/LibraryGroup.cs
git commit -m "library: add LibraryGroup enum"
```

---

## Task 2: Create the `LibraryEntry` record

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryEntry.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace Fretless.Web.Client.Library;

/// <summary>
/// One entry in the component library sidebar. <c>DemoType</c> is rendered via
/// <c>&lt;DynamicComponent&gt;</c> in the right pane.
/// </summary>
public sealed record LibraryEntry(
    string Slug,
    string Title,
    string Description,
    LibraryGroup Group,
    Type DemoType,
    string SourcePath);
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/LibraryEntry.cs
git commit -m "library: add LibraryEntry record"
```

---

## Task 3: Create the empty `LibraryRegistry`

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryRegistry.cs`

- [ ] **Step 1: Write the file**

```csharp
using System.Collections.Generic;

namespace Fretless.Web.Client.Library;

/// <summary>
/// Central registry of every demo shown in the /components gallery.
/// Phase 1 leaves this empty; Phase 2 populates it as as-is demos are added.
/// </summary>
public static class LibraryRegistry
{
    public static IReadOnlyList<LibraryEntry> All { get; } = new List<LibraryEntry>();

    public static LibraryEntry? FindBySlug(LibraryGroup group, string slug)
    {
        foreach (var entry in All)
        {
            if (entry.Group == group && string.Equals(entry.Slug, slug, System.StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }
        return null;
    }

    public static IReadOnlyList<LibraryEntry> ForGroup(LibraryGroup group)
    {
        var result = new List<LibraryEntry>();
        foreach (var entry in All)
        {
            if (entry.Group == group)
            {
                result.Add(entry);
            }
        }
        return result;
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/LibraryRegistry.cs
git commit -m "library: add empty LibraryRegistry"
```

---

## Task 4: Add the `Library` namespace to `_Imports.razor`

**Files:**
- Modify: `Fretless.Web/Fretless.Web.Client/_Imports.razor`

- [ ] **Step 1: Edit the file**

Append a new line after `@using Fretless.Web.Client.Models`:

```razor
@using Fretless.Web.Client.Library
```

The full file should read:

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using Fretless.Web.Client
@using Fretless.Web.Client.Layout
@using Fretless.Web.Client.Components
@using Fretless.Web.Client.Models
@using Fretless.Web.Client.Library
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/_Imports.razor
git commit -m "library: import Library namespace from _Imports"
```

---

## Task 5: Create `LibraryFrame` (visual chrome for demos)

**Why this exists in Phase 1:** Demo files in Phase 2 need a consistent wrapper (title, description, source path, slot for variants and controls). Ship it now so Phase 2 has nothing to invent.

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryFrame.razor`
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryFrame.razor.css`

- [ ] **Step 1: Write `LibraryFrame.razor`**

```razor
<div class="library-frame">
    <header class="library-frame__header">
        <h1 class="library-frame__title">@Title</h1>
        @if (!string.IsNullOrWhiteSpace(Description))
        {
            <p class="library-frame__description">@Description</p>
        }
        @if (!string.IsNullOrWhiteSpace(SourcePath))
        {
            <p class="library-frame__source">
                <span class="library-frame__source-label">Source:</span>
                <code>@SourcePath</code>
            </p>
        }
    </header>

    @if (Variants is not null)
    {
        <section class="library-frame__section" aria-label="Variants">
            <h2 class="library-frame__section-title">Variants</h2>
            @Variants
        </section>
    }

    @if (Controls is not null)
    {
        <section class="library-frame__section" aria-label="Controls">
            <h2 class="library-frame__section-title">Controls</h2>
            @Controls
        </section>
    }
</div>

@code {
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? SourcePath { get; set; }
    [Parameter] public RenderFragment? Variants { get; set; }
    [Parameter] public RenderFragment? Controls { get; set; }
}
```

- [ ] **Step 2: Write `LibraryFrame.razor.css`**

```css
.library-frame {
    color: #e8e8e8;
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
    padding: 1.25rem 1.5rem;
}

.library-frame__header {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    border-bottom: 1px solid rgba(255, 215, 0, 0.18);
    padding-bottom: 0.85rem;
}

.library-frame__title {
    margin: 0;
    font-size: 1.4rem;
    font-weight: 800;
    color: #ffd700;
    letter-spacing: 0.01em;
}

.library-frame__description {
    margin: 0;
    color: #c8c8c8;
    font-size: 0.95rem;
    line-height: 1.4;
}

.library-frame__source {
    margin: 0;
    color: #8a8a8a;
    font-size: 0.82rem;
}

.library-frame__source-label {
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin-right: 0.4rem;
}

.library-frame__source code {
    color: #c8c8c8;
    background: rgba(255, 255, 255, 0.04);
    padding: 0.1rem 0.4rem;
    border-radius: 4px;
    font-size: 0.82rem;
}

.library-frame__section {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
}

.library-frame__section-title {
    margin: 0;
    font-size: 0.78rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.12em;
    color: #888;
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/LibraryFrame.razor Fretless.Web/Fretless.Web.Client/Library/LibraryFrame.razor.css
git commit -m "library: add LibraryFrame chrome component"
```

---

## Task 6: Create `LibrarySidebar`

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibrarySidebar.razor`
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibrarySidebar.razor.css`

- [ ] **Step 1: Write `LibrarySidebar.razor`**

```razor
<aside class="library-sidebar" aria-label="Component library navigation">
    <header class="library-sidebar__header">
        <span class="library-sidebar__brand">FretLess</span>
        <span class="library-sidebar__title">Library</span>
    </header>

    @foreach (var group in GroupOrder)
    {
        var entries = LibraryRegistry.ForGroup(group);
        <section class="library-sidebar__group">
            <h2 class="library-sidebar__group-title">@GroupLabel(group)</h2>
            @if (entries.Count == 0)
            {
                <p class="library-sidebar__empty">No entries yet.</p>
            }
            else
            {
                <ul class="library-sidebar__list">
                    @foreach (var entry in entries)
                    {
                        var isActive = ActiveSlug is not null
                            && ActiveGroup == entry.Group
                            && string.Equals(ActiveSlug, entry.Slug, StringComparison.OrdinalIgnoreCase);
                        <li>
                            <button type="button"
                                    class="library-sidebar__link @(isActive ? "is-active" : null)"
                                    @onclick="() => OnSelect(entry)">
                                @entry.Title
                            </button>
                        </li>
                    }
                </ul>
            }
        </section>
    }
</aside>

@code {
    [Parameter] public LibraryGroup? ActiveGroup { get; set; }
    [Parameter] public string? ActiveSlug { get; set; }
    [Parameter] public EventCallback<LibraryEntry> EntrySelected { get; set; }

    private static readonly LibraryGroup[] GroupOrder =
    {
        LibraryGroup.Atoms,
        LibraryGroup.Primitives,
        LibraryGroup.Components,
        LibraryGroup.Layout,
        LibraryGroup.Tokens,
    };

    private static string GroupLabel(LibraryGroup group) => group switch
    {
        LibraryGroup.Atoms => "Atoms",
        LibraryGroup.Primitives => "Primitives",
        LibraryGroup.Components => "Components",
        LibraryGroup.Layout => "Layout",
        LibraryGroup.Tokens => "Tokens",
        _ => group.ToString(),
    };

    private Task OnSelect(LibraryEntry entry) => EntrySelected.InvokeAsync(entry);
}
```

- [ ] **Step 2: Write `LibrarySidebar.razor.css`**

```css
.library-sidebar {
    width: 240px;
    flex-shrink: 0;
    background: #0d0d0d;
    border-right: 1px solid rgba(255, 255, 255, 0.06);
    padding: 1rem 0.75rem;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
}

.library-sidebar__header {
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
    padding: 0.25rem 0.4rem 0.6rem;
    border-bottom: 1px solid rgba(255, 215, 0, 0.18);
}

.library-sidebar__brand {
    color: #ffd700;
    font-size: 0.7rem;
    font-weight: 800;
    letter-spacing: 0.12em;
    text-transform: uppercase;
}

.library-sidebar__title {
    color: #ffffff;
    font-size: 1.05rem;
    font-weight: 700;
}

.library-sidebar__group {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}

.library-sidebar__group-title {
    margin: 0;
    padding: 0 0.4rem;
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.12em;
    color: #ffd700;
}

.library-sidebar__list {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
}

.library-sidebar__link {
    display: block;
    width: 100%;
    text-align: left;
    background: transparent;
    color: #c8c8c8;
    border: none;
    padding: 0.4rem 0.55rem;
    border-radius: 6px;
    font-size: 0.88rem;
    cursor: pointer;
    font-family: inherit;
    transition: background 0.15s, color 0.15s;
}

.library-sidebar__link:hover {
    background: rgba(255, 215, 0, 0.08);
    color: #ffffff;
}

.library-sidebar__link.is-active {
    background: rgba(255, 215, 0, 0.18);
    color: #ffd700;
    font-weight: 600;
}

.library-sidebar__empty {
    margin: 0;
    padding: 0.25rem 0.55rem;
    color: #555;
    font-size: 0.78rem;
    font-style: italic;
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/LibrarySidebar.razor Fretless.Web/Fretless.Web.Client/Library/LibrarySidebar.razor.css
git commit -m "library: add LibrarySidebar"
```

---

## Task 7: Create `LibraryDetail` (right pane)

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryDetail.razor`
- Create: `Fretless.Web/Fretless.Web.Client/Library/LibraryDetail.razor.css`

- [ ] **Step 1: Write `LibraryDetail.razor`**

```razor
<section class="library-detail" aria-live="polite">
    @if (Entry is null)
    {
        <div class="library-detail__placeholder">
            <h1 class="library-detail__placeholder-title">Component Library</h1>
            <p class="library-detail__placeholder-text">
                Select an entry from the sidebar to view its variants and controls.
            </p>
            <p class="library-detail__placeholder-text library-detail__placeholder-hint">
                The library is empty until Phase 2 of the rollout adds component demos.
                See <code>docs/superpowers/specs/2026-05-07-component-library-design.md</code>.
            </p>
        </div>
    }
    else
    {
        <DynamicComponent Type="@Entry.DemoType" />
    }
</section>

@code {
    [Parameter] public LibraryEntry? Entry { get; set; }
}
```

- [ ] **Step 2: Write `LibraryDetail.razor.css`**

```css
.library-detail {
    flex: 1;
    min-width: 0;
    overflow-y: auto;
    background: #000000;
    color: #e8e8e8;
}

.library-detail__placeholder {
    padding: 4rem 2rem;
    max-width: 720px;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    color: #c8c8c8;
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
}

.library-detail__placeholder-title {
    margin: 0;
    font-size: 1.5rem;
    font-weight: 800;
    color: #ffd700;
}

.library-detail__placeholder-text {
    margin: 0;
    line-height: 1.5;
}

.library-detail__placeholder-hint {
    color: #888;
    font-size: 0.88rem;
}

.library-detail__placeholder-hint code {
    background: rgba(255, 255, 255, 0.04);
    padding: 0.1rem 0.4rem;
    border-radius: 4px;
    color: #c8c8c8;
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/LibraryDetail.razor Fretless.Web/Fretless.Web.Client/Library/LibraryDetail.razor.css
git commit -m "library: add LibraryDetail right pane"
```

---

## Task 8: Create `ComponentLibraryPage` (the `/components` route)

**Why this is one task and not split:** the page is the orchestrator — it owns the URL hash parsing, the selection state, and wires `LibrarySidebar` ↔ `LibraryDetail`. Splitting it would cause one half to be unbuildable.

**Files:**
- Create: `Fretless.Web/Fretless.Web.Client/Library/ComponentLibraryPage.razor`
- Create: `Fretless.Web/Fretless.Web.Client/Library/ComponentLibraryPage.razor.css`

- [ ] **Step 1: Write `ComponentLibraryPage.razor`**

```razor
@page "/components"
@implements IDisposable
@inject NavigationManager Navigation

<PageTitle>Library | Fretless</PageTitle>

<div class="component-library">
    <LibrarySidebar ActiveGroup="@_activeEntry?.Group"
                    ActiveSlug="@_activeEntry?.Slug"
                    EntrySelected="OnEntrySelected" />
    <LibraryDetail Entry="@_activeEntry" />
</div>

@code {
    private LibraryEntry? _activeEntry;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        ApplyHashFromUri(Navigation.Uri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        ApplyHashFromUri(e.Location);
        InvokeAsync(StateHasChanged);
    }

    private void ApplyHashFromUri(string uri)
    {
        var hashIndex = uri.IndexOf('#');
        if (hashIndex < 0 || hashIndex == uri.Length - 1)
        {
            _activeEntry = null;
            return;
        }

        var hash = uri[(hashIndex + 1)..];
        var slash = hash.IndexOf('/');
        if (slash <= 0 || slash == hash.Length - 1)
        {
            _activeEntry = null;
            return;
        }

        var groupSegment = hash[..slash];
        var slugSegment = hash[(slash + 1)..];

        if (!Enum.TryParse<LibraryGroup>(groupSegment, ignoreCase: true, out var group))
        {
            _activeEntry = null;
            return;
        }

        _activeEntry = LibraryRegistry.FindBySlug(group, slugSegment);
    }

    private Task OnEntrySelected(LibraryEntry entry)
    {
        _activeEntry = entry;
        var groupSlug = entry.Group.ToString().ToLowerInvariant();
        var hash = $"#{groupSlug}/{entry.Slug}";
        var newUri = Navigation.GetUriWithQueryParameters(Navigation.BaseUri + "components");
        Navigation.NavigateTo(newUri + hash, forceLoad: false, replace: true);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
```

- [ ] **Step 2: Write `ComponentLibraryPage.razor.css`**

```css
.component-library {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: stretch;
    background: #000000;
}
```

`position: fixed; inset: 0;` takes the page out of `main`'s flow, so it fills the viewport regardless of `main`'s padding. The existing `NavMenu` (also `position: fixed`) layers above the sidebar correctly.

- [ ] **Step 3: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Library/ComponentLibraryPage.razor Fretless.Web/Fretless.Web.Client/Library/ComponentLibraryPage.razor.css
git commit -m "library: add ComponentLibraryPage at /components"
```

---

## Task 9: Add the "Library" `<NavLink>` to `NavMenu`

**Files:**
- Modify: `Fretless.Web/Fretless.Web.Client/Layout/NavMenu.razor`

- [ ] **Step 1: Edit the file**

Replace the existing single-link panel (lines 7–13) so the panel reads:

```razor
    <div class="app-menu__panel" onclick="document.getElementById('app-menu-toggle')?.click()">
        <a class="app-menu__brand" href="">FretLess</a>

        <NavLink class="app-menu__link" href="" Match="NavLinkMatch.All">
            Explore Scales
        </NavLink>

        <NavLink class="app-menu__link" href="components" Match="NavLinkMatch.Prefix">
            Library
        </NavLink>
    </div>
```

The full file should now read:

```razor
<nav class="app-menu" aria-label="Main navigation">
    <input id="app-menu-toggle" type="checkbox" title="Navigation menu" class="app-menu__toggle" />
    <label class="app-menu__button" for="app-menu-toggle" aria-label="Open navigation menu">
        <span aria-hidden="true"></span>
    </label>

    <div class="app-menu__panel" onclick="document.getElementById('app-menu-toggle')?.click()">
        <a class="app-menu__brand" href="">FretLess</a>

        <NavLink class="app-menu__link" href="" Match="NavLinkMatch.All">
            Explore Scales
        </NavLink>

        <NavLink class="app-menu__link" href="components" Match="NavLinkMatch.Prefix">
            Library
        </NavLink>
    </div>
</nav>
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fretless.Web/Fretless.Web.Client/Layout/NavMenu.razor
git commit -m "nav: add Library link to main menu"
```

---

## Task 10: Format check

**Why this exists as its own task:** the project's verify script enforces `dotnet format --verify-no-changes`. Catching whitespace drift now is cheaper than chasing it after the manual smoke test.

- [ ] **Step 1: Run format verification**

Run: `dotnet format Fretless.Web/Fretless.Web/Fretless.Web.csproj --verify-no-changes`
Expected: exit code 0, no output beyond the standard headers.

If it reports formatting changes, run `dotnet format Fretless.Web/Fretless.Web/Fretless.Web.csproj` (without `--verify-no-changes`) to apply them, then re-run the verify command.

- [ ] **Step 2: If anything was reformatted, commit the formatting fix**

If `dotnet format` made changes:

```bash
git add -A Fretless.Web/Fretless.Web.Client/Library Fretless.Web/Fretless.Web.Client/Layout
git commit -m "library: dotnet format pass"
```

If nothing changed, skip this commit.

---

## Task 11: Manual smoke test

**Why manual:** there is no test project. The spec calls out manual verification as the integration gate.

- [ ] **Step 1: Launch the app**

Run: `./fretless.ps1`
Expected: dev server starts, browser opens to `http://localhost:<port>` showing the existing SpiralWheel page (Explore Scales).

- [ ] **Step 2: Verify the existing app still works**

Confirm in the browser:
- The Explore Scales page (`/`) loads with the spiral wheel visible.
- Clicking notes on the wheel still highlights them and plays audio (this is regression coverage — Phase 1 should have changed nothing on this page).

- [ ] **Step 3: Verify the new nav link**

Open the hamburger menu (top-left). Confirm two entries are visible:
- "Explore Scales"
- "Library" (newly added)

- [ ] **Step 4: Verify the gallery shell**

Click "Library". Confirm:
- URL becomes `/components`.
- The page shows a left sidebar with five group headings (Atoms, Primitives, Components, Layout, Tokens). Each group displays "No entries yet." underneath.
- The right pane shows the placeholder text: "Select an entry from the sidebar to view its variants and controls." plus the spec hint.
- The page fills the viewport without scrollbars. The hamburger menu is still reachable in the top-left.

- [ ] **Step 5: Verify URL hash deep-link is parseable**

In the browser address bar, navigate to `http://localhost:<port>/components#primitives/does-not-exist` and reload. Expected: the right pane shows the placeholder (the registry doesn't contain that slug, so no demo renders, but the page must not throw).

Open the browser dev console and confirm there are zero errors and zero unhandled exceptions during navigation.

- [ ] **Step 6: Stop the dev server**

Stop the `fretless.ps1` process (Ctrl+C in its terminal, or kill via Task Manager / `Stop-Process`).

- [ ] **Step 7: Commit nothing**

This task changes no files; it only validates the previous tasks. Do not create a commit.

---

## Task 12: Final build + format check

**Why this exists:** earlier tasks each ran a single build, but a fresh end-to-end pass catches drift across all the new files together. `./scripts/verify.ps1` is the equivalent CI script, but it isn't tracked in the worktree, so we run the underlying commands directly.

- [ ] **Step 1: Restore and build**

Run: `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj`
Expected: `Build succeeded` with 0 warnings and 0 errors.

- [ ] **Step 2: Format verification**

Run: `dotnet format Fretless.Web/Fretless.Web/Fretless.Web.csproj --verify-no-changes`
Expected: exit code 0, no output beyond the standard headers.

- [ ] **Step 3: Commit nothing**

This task validates only.

---

## Definition of done for Phase 1

All boxes above ticked. Specifically:

- `git log --oneline` shows commits from tasks 1–9 (and possibly 10 if formatting changed).
- `dotnet build Fretless.Web/Fretless.Web/Fretless.Web.csproj` is clean.
- `dotnet format Fretless.Web/Fretless.Web/Fretless.Web.csproj --verify-no-changes` is clean.
- The app runs, the existing SpiralWheel page is unchanged, and `/components` renders the empty shell with the deep-link routing wired but no demos.
- Phase 2 (a separate plan) can begin by adding entries to `LibraryRegistry.All` and creating `Library/Demos/<group>/<name>Demo.razor` files.

## What this plan deliberately does not do

- No demos. `LibraryRegistry.All` is empty. Phase 2 will populate it.
- No primitive or atom extraction. Phase 3 (atoms) and Phase 4 (primitives) handle that.
- No tokens page content. Phase 5 builds the read-only tokens reference.
- No CSS class renaming, no token migration. Those are phase C.
- No tests. There is no test project; spec says manual verification is the only gate.
