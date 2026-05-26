# winget manifests

These three YAML files are the **initial** winget manifest for Marker. They are
needed only once — to get `PaulSnijders.Marker` into the
[`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) catalog the
first time. After that, the `Release` GitHub Actions workflow updates the
catalog automatically on every tagged release (via `wingetcreate`).

## One-time setup (do this once)

### 1. Create a token for the workflow

`wingetcreate` in CI needs to fork and open a PR against `microsoft/winget-pkgs`,
which the default `GITHUB_TOKEN` cannot do. Create a **classic** Personal Access
Token with the `public_repo` scope, then add it to this repo as a secret named
`WINGET_TOKEN` (Settings → Secrets and variables → Actions → New secret).

### 2. Cut the first release

Push the first version tag so the workflow builds and publishes `v1.0.0`:

```sh
git tag v1.0.0
git push origin v1.0.0
```

### 3. Submit the first manifest

Once the GitHub Release exists:

1. Copy the real SHA256 from the release asset `Marker-1.0.0-win-x64.zip.sha256`
   into `PaulSnijders.Marker.installer.yaml` (the `InstallerSha256` field).
2. Validate locally:
   ```sh
   winget validate --manifest windows-app/winget
   winget install --manifest windows-app/winget   # optional smoke test
   ```
3. Fork `microsoft/winget-pkgs`, copy these three files to
   `manifests/p/PaulSnijders/Marker/1.0.0/`, and open a PR.

That PR is reviewed and merged by the winget team. From the **next** release
onward, the workflow handles updates with no manual steps.

## Notes

- `Microsoft.DotNet.DesktopRuntime.10` is declared as a dependency, so winget
  pulls in the .NET 10 Desktop Runtime automatically — that is what keeps the
  Marker download itself small (~3 MB).
- These files are kept here for reference and for the first submission only;
  they are **not** the live source of truth. The live manifest is whatever is
  merged into `winget-pkgs`.
