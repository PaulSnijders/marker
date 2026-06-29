# Marker — Requirements

A fast, lightweight local file editor for Windows. Replaces Notepad for daily
note-taking and casual editing of text files. Built with WPF + AvalonEdit on
.NET 10.

## 1. Goals

- Open instantly — cold start around a second.
- Stay simple — the whole app is understandable in an afternoon.
- Stay extendable — adding features later (sync, new file types) stays easy.
- Files on disk stay plain files. No proprietary format, no database, no lock-in.

## 2. Layout

- Two panes: a sidebar on the left, editor on the right. The sidebar has three
  tabs — **Tree**, **Find** (find in files) and **Replace** (replace in files) —
  all scoped to the active workspace via the switcher above them.
- Draggable splitter. Window size, position and pane width persist between runs.
- Light and dark themes (toggle in the View menu). The whole UI — menus,
  scrollbars, title bar — follows the theme.
- A black splash screen with the logo shows on launch (no white flash).

## 3. File tree (left pane)

- A **workspace** is a named set of one or more folders, with its own open
  tabs and scratchpad. A switcher above the tree (dropdown + a New button)
  selects the active workspace; Rename / Delete live in the Workspace menu.
  Switching swaps the whole tree and the open tabs. There is always at least
  one workspace.
- Folders are added to / removed from the active workspace (Workspace menu,
  or drag & drop). Deleting a workspace never touches files on disk.
- Recursive, lazy-loaded tree. Folders and files are visually distinct, with
  lightweight extension icons. Which folders are expanded persists per
  workspace, so the tree restores to the same shape between runs.
- Single click opens a file, or expands/collapses a folder.
- Right-click menu: New file, New folder, Rename, Delete (to Recycle Bin),
  Reveal in Explorer, Copy Full Path, Remove folder from workspace.
- Keyboard: Ctrl+T focuses the tree; arrow keys navigate; Enter/Space opens a
  file or expands a folder; F2 renames; Del moves to the Recycle Bin (with
  confirmation). Right-arrow on a file opens a transient *sneak-peek* tab,
  dismissed again as the selection moves.
- Shift+Alt+R reveals the selected node in Explorer; Shift+Alt+C copies its
  full path (both are context-aware — they act on the focused tree node, or on
  the current tab when the editor has focus).
- Auto-refreshes when files change outside the app.
- Configurable ignore patterns (default: `.git`, `node_modules`, `bin`, `obj`).
- Drag & drop from Windows Explorer: files open as tabs, folders are added to
  the active workspace.

## 4. Editor (right pane)

- Tabs for open files. Unsaved changes are marked with a dot. Close button
  per tab, middle-click to close, Ctrl+Tab to cycle, Ctrl+1…9 to jump to a
  tab. Right-click a tab: close / close others / close all, reveal in
  Explorer, copy full path.
- Open File (Ctrl+O): the standard OS file picker (multi-select), for opening
  one-off files that live outside any workspace folder.
- Quick Open (Ctrl+P): a type-to-find file picker over the active workspace,
  fuzzy-ranked, with arrow keys + Enter to open.
- Undo/redo, find (Ctrl+F), replace (Ctrl+H) — both follow the theme. Find
  works in all three markdown modes (source, rich and read), not just source.
- Find in files (Ctrl+Shift+F) and Replace in files (Ctrl+Shift+R): a
  workspace-wide search in the sidebar, with match-case / whole-word / regex
  toggles. Find shows a results tree grouped by file; click a hit to jump to it
  (the matched line is centered in the view). Replace previews every change and
  commits on Ctrl+Enter after a confirmation. The three toggle states are
  remembered between sessions.
- Line numbers; word-wrap toggle (View menu; applies to source mode).
- Per-file caret position and scroll offset are remembered, so reopening a
  workspace puts each tab back where it was left, on the file that was last
  active.
- Auto-save on focus loss (configurable, on by default) plus manual Ctrl+S;
  Save All is Ctrl+Shift+S.
- Adjustable editor font size (View menu).
- Status bar: file path, type, encoding, line ending, line/column.
- Scratchpad: an always-available notepad, opened from a small button at the
  right of the tab strip. It is a markdown file — so it gets highlighting and
  the three view modes — and each workspace keeps its own.
- Help menu: opens a bundled guide as a rendered markdown tab.
- Cheat sheet: pressing Esc when nothing else needs the key (no search bar,
  dialog, etc.) shows a popup of the main keyboard shortcuts; Esc again hides it.

## 5. Supported file types

| Extension          | Highlighting | Modes                  |
|--------------------|--------------|------------------------|
| `.md`, `.markdown` | Markdown     | Source / Rich / Read   |
| `.txt`, `.csv`, `.log` | None     | Edit                   |
| `.json`            | JSON         | Edit                   |
| `.xml`, `.html`    | XML / HTML   | Edit                   |
| `.yaml`, `.yml`    | YAML         | Edit                   |
| `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.webp`, `.ico` | — | Image preview |
| Other text         | None         | Edit                   |
| Binary             | —            | "Binary file" placeholder |

Images are shown inline in a dedicated image host (no file lock, so they can be
edited elsewhere while open). Clicking the image opens the Windows "Open with…"
dialog to hand it to another program. (`.svg` is treated as text/XML, not an
image.)

Encoding: UTF-8 by default. Detects BOM, preserves CRLF/LF line endings on save.

## 6. Markdown — three modes

Ctrl+M cycles them:

- **Source** — plain markdown text with syntax highlighting.
- **Rich** — WYSIWYG editing (TOAST UI editor in WebView2); saves valid markdown.
  Pasting or dropping an image saves it to a sibling `images/` folder next to the
  file and embeds a relative link (falling back to an inline data URL if the file
  isn't on disk yet).
- **Read** — rendered HTML (Markdig) with code-block highlighting.

The mode is app-wide: switching it on one markdown tab applies to every open
markdown tab and is remembered for newly opened files. The scratchpad always
opens in Source.

## 7. Settings

- App-wide preferences: `%APPDATA%\Marker\settings.json` — theme, font,
  auto-save, ignore patterns, recent files, window layout, the app-wide markdown
  mode, the find-in-files toggle state, and the active workspace name. Editing it
  by hand is fine.
- Each workspace is its own JSON file in `%APPDATA%\Marker\workspaces\`,
  holding its name, folders, open files, expanded tree folders, the last active
  file, and a per-file caret/scroll snapshot. Scratchpads live in
  `%APPDATA%\Marker\scratch\`.
- Everything is written immediately on every change, atomically — nothing is
  lost on a crash.

## 8. Architecture

- **Marker.App** — WPF executable: views, view models, app composition.
- **Marker.Core** — file system, markdown rendering, settings, models. No UI.

Key abstractions in `Marker.Core` (the seams for future features like sync):

- `IFileRepository` — read/write/list files (local file system in v1).
- `IMarkdownRenderer` — markdown → HTML (Markdig).
- `ISettingsStore` — load/save app settings (JSON file).
- `IWorkspaceStore` — load/save each workspace's own JSON file.
- `IFileTypeRegistry` — maps file extensions to highlighting and modes.

No DI container — a simple static service locator.

## 9. Dependencies

- **AvalonEdit** — the core editor control.
- **Markdig** — markdown parser, for read mode.
- **Microsoft.Web.WebView2** — for rich and read modes.
- **CommunityToolkit.Mvvm** — lightweight MVVM helpers.

## 10. Build & distribution

- `dotnet build` / `dotnet publish`. A single `.exe` — no installer, copy and run.
- A GitHub Actions workflow builds and publishes a release on each tagged build.
- Distributed via winget (manifests under `windows-app/winget/`).

## 11. Out of scope

- Sync, cloud, server; mobile or web versions.
- Plugin system, bidirectional linking, graph view.
- Git integration, spell check, PDF viewer. (Images are previewed but not edited.)
- Cross-platform (Windows only).
- Automated tests (skipped for v1).
