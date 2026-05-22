# Marker

A fast, lightweight local file editor for Windows — a friendlier replacement
for Notepad for daily note-taking and casual editing of text files. Built with
WPF + [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) on .NET 10.

![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)

Files on disk stay plain files — no proprietary format, no database, no lock-in.

## Features

- **Workspaces** — a named set of one or more folders, each with its own open
  tabs and scratchpad. Switch between them from the dropdown above the file tree.
- **File tree** — recursive, lazy-loaded, with single-click open, a right-click
  menu (new / rename / delete to Recycle Bin / reveal in Explorer) and live
  refresh when files change outside the app.
- **Tabbed editor** — find & replace, line numbers, word-wrap, undo/redo,
  auto-save, adjustable font size.
- **Markdown, three ways** — edit the source with syntax highlighting, edit
  WYSIWYG ([TOAST UI Editor](https://ui.toast.com/tui-editor)), or read the
  rendered HTML ([Markdig](https://github.com/xoofx/markdig)).
- **Scratchpad** — an always-available plain-text notepad, one per workspace.
- **Light & dark themes** — the whole UI follows the theme, including the
  title bar.
- **Drag & drop** from Windows Explorer — files open as tabs, folders are
  added to the active workspace.

## Requirements

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build
- The [WebView2 runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
  for the rich and read markdown modes (preinstalled on current Windows; the
  app degrades gracefully to source mode if it is missing)

## Build & run

```sh
git clone https://github.com/PaulSnijders/Marker.git
cd Marker
dotnet run --project Marker.App
```

To produce a standalone build:

```sh
dotnet publish Marker.App -c Release
```

The result is a plain `.exe` — no installer; copy the output folder and run.

## Project layout

| Path             | What it is                                                |
|------------------|-----------------------------------------------------------|
| `Marker.slnx`    | Solution file                                             |
| `Marker.App/`    | WPF application — views, view models, app composition     |
| `Marker.Core/`   | Core logic — file system, markdown, settings (no UI)      |
| `requirements.md`| Design notes / specification                              |

`Marker.Core` is UI-free and exposes a few small interfaces
(`IFileRepository`, `IMarkdownRenderer`, `ISettingsStore`, `IWorkspaceStore`,
`IFileTypeRegistry`) — the seams for adding features later.

## Configuration

All configuration lives under `%APPDATA%\Marker\`:

- `settings.json` — app-wide preferences (theme, font, window layout, …)
- `workspaces\*.json` — one file per workspace
- `scratch\*.txt` — per-workspace scratchpads

Every file is plain JSON/text and safe to edit by hand. Set the
`MARKER_SETTINGS_DIR` environment variable to redirect all of this elsewhere
(useful for a portable setup or for testing).

## Contributing

This is a small hobby project, but issues and pull requests are welcome. Keep
changes simple and in the spirit of the existing code — see `requirements.md`
for the design intent. Build with the .NET 10 SDK; there is no extra setup.

## License

[MIT](LICENSE) © 2026 Paul Snijders.

Bundled third-party components and their licenses are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
