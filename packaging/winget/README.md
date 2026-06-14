# winget packaging

Manifests for publishing `taskmon` to the [Windows Package Manager Community
Repository](https://github.com/microsoft/winget-pkgs) under the package identifier
`jchas2.taskmon`.

```
winget/
  build-winget.ps1          Stamps version + release date + per-arch SHA256 into the manifests
  templates/
    jchas2.taskmon.yaml                 Version manifest
    jchas2.taskmon.installer.yaml       Installer manifest (x64 + ARM64 entries)
    jchas2.taskmon.locale.en-US.yaml    Default locale manifest (metadata)
  dist/                     Generated, stamped manifests (git-ignored)
```

The manifests reference the official GitHub Release archives
(`taskmon-<version>-windows-<arch>.zip`) and their SHA256 checksums — the same artifacts
produced by the `build-windows` matrix in `.github/workflows/release.yml`. Each zip
contains a single portable `taskmon.exe`, so winget uses a `zip` installer with a
`portable` nested installer and shims `taskmon` onto PATH.

## How it ships

Unlike Chocolatey, winget has no per-project feed to push to — a new version is published
by opening a pull request against `microsoft/winget-pkgs`. On a `v*.*.*` tag, the
`publish-winget` job:

1. Downloads the x64 and ARM64 release archives (workflow artifacts).
2. Runs `build-winget.ps1` to compute checksums and stamp tokens into `dist/`.
3. Downloads `wingetcreate.exe` and runs `wingetcreate submit dist/`, which opens a PR
   to `microsoft/winget-pkgs` from a fork owned by the `WINGET_TOKEN` account.

> Each submission goes through winget's automated validation (and, for the **first**
> submission of a new package, human moderation). A successful CI run means the PR was
> *opened*, not that the version is live yet.

### Prerequisites

- A **fork** of `microsoft/winget-pkgs` under the account that owns `WINGET_TOKEN`
  (e.g. `jchas2/winget-pkgs`). wingetcreate pushes its branch there.
- A repository secret **`WINGET_TOKEN`**: a GitHub PAT with `public_repo` (classic) or
  fine-grained Contents + Pull requests write access to that fork.

### First-time bootstrap (one-off)

The very first submission should be created and validated locally before relying on CI:

```powershell
# Build the stamped manifests for an existing release:
./build-winget.ps1 -Version 1.3.0 -X64Zip .\x64.zip -Arm64Zip .\arm64.zip

# Validate and test the install from the generated manifests:
winget validate --manifest .\dist
winget install --manifest .\dist

# Open the first PR (requires the winget-pkgs fork + a PAT):
wingetcreate submit .\dist --token <PAT>
```

After the first version is merged, the `publish-winget` CI job handles every subsequent
release automatically.
