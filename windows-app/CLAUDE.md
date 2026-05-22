# Marker — Windows app

WPF + AvalonEdit on .NET 10. Two projects: `Marker.App` (WPF UI) and
`Marker.Core` (no UI). No DI — a static `AppServices` service locator.

## Testing
- NEVER read, write, or delete `%APPDATA%\Marker` — it holds the real user's
  settings, workspaces and scratchpads. (A past test cleanup wiped it once.)
- Run and test against a throwaway directory via the `MARKER_SETTINGS_DIR`
  environment variable.

## Specs
Requirements live in `../docs/requirements.md`.
