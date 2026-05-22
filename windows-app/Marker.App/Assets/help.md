# Welcome to Marker 👋

Marker is a fast, lightweight editor for your everyday text files — think of it
as Notepad with super-powers. Your files stay plain files on disk: no lock-in,
no surprises. Here's everything you need to get going.

## Workspaces

A **workspace** is a named set of folders you work on together — each with its
own tabs and scratchpad.

- Pick a workspace from the dropdown at the top of the file tree.
- Hit the **+** button to create a new one.
- **Rename** or **delete** workspaces from the **Workspace** menu.
- Drag a folder in from Windows Explorer to add it to the current workspace.

Switching workspaces swaps the whole tree and reopens that workspace's tabs —
so you can jump between projects in a click.

## The file tree

- **Single-click** a file to open it, or a folder to expand it.
- **Right-click** for new file, new folder, rename, delete (to the Recycle
  Bin) and reveal in Explorer.
- The tree refreshes itself when files change outside the app.

**Keyboard, hands-free:** press **Ctrl+T** to jump into the tree, then use the
**arrow keys** to move around — **Enter** or **Space** opens a file or expands
a folder. **Right arrow** on a file is a *sneak peek*: it opens the file in a
temporary tab so you can skim it. Move up or down and the peek closes again —
no pile of tabs. Press **Enter** when you want to keep it.

## Tabs

Every open file gets a tab. A dot means unsaved changes.

- **Middle-click** a tab to close it.
- **Right-click** a tab for close others, close all, reveal in Explorer and
  copy full path.

## Markdown, three ways

Open a `.md` file and switch how you see it — per tab, any time:

| Mode       | What it does                                    |
|------------|-------------------------------------------------|
| **Source** | Plain markdown text with syntax highlighting    |
| **Rich**   | WYSIWYG editing — format without typing symbols |
| **Read**   | The finished, rendered page                     |

Use the **View** menu, or press **Ctrl+M** to cycle through them.

## The scratchpad

Need to jot something down? Click the notepad icon on the right of the tab
strip. It's a markdown file — so you get highlighting and the Source / Rich /
Read modes — always one keystroke away, and every workspace keeps its own.

## Make it yours

- Toggle **light / dark** theme from the **View** menu — the whole app
  follows along.
- Set a comfortable **editor font size** under **View ▸ Editor Font Size**.
- **Auto-save** keeps your work safe; turn it off in settings if you'd rather
  save by hand.

## Handy shortcuts

| Shortcut         | Action                       |
|------------------|------------------------------|
| `Ctrl+P`         | Quick-open a file by name    |
| `Ctrl+S`         | Save                         |
| `Ctrl+Shift+S`   | Save all                     |
| `Ctrl+N`         | New file                     |
| `Ctrl+W`         | Close tab                    |
| `Ctrl+Tab`       | Next tab                     |
| `Ctrl+1`…`9`     | Jump to tab 1–9              |
| `Ctrl+F`         | Find                         |
| `Ctrl+H`         | Replace                      |
| `Ctrl+M`         | Cycle markdown mode          |
| `Ctrl+T`         | Focus the file tree          |
| `Ctrl+Shift+W`   | Switch workspace             |
| `Alt`            | Open the menu bar            |
| `Esc`            | Show / hide the cheat sheet  |

Forgotten a shortcut? Press **Esc** anytime nothing else needs it and a little
cheat sheet pops up. **Esc** again sends it away.

## Where your settings live

Everything sits under `%APPDATA%\Marker\` — your preferences, your workspaces
and your scratchpads — as plain JSON and text files. Back them up, sync them,
or edit them by hand. They're yours.

---

That's it — happy writing! ✍️
