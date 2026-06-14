# Packaging

Distribution packaging for `taskmon`. Each package manager lives in its own subfolder.

| Folder   | Manager                | Status      |
|----------|------------------------|-------------|
| `choco`  | Chocolatey (Windows)   | Implemented |
| `winget` | winget (Windows)       | Implemented |

The macOS Homebrew formula is published to a separate tap repository
(`jchas2/homebrew-taskmon`) and is updated directly by the release workflow.

## Chocolatey (`choco/`)

```
choco/
  taskmon.nuspec              Package metadata (version filled at pack time)
  build-choco.ps1             Stamps version + checksums and runs `choco pack`
  tools/
    chocolateyinstall.ps1     Downloads the arch-appropriate release zip and shims taskmon.exe
    chocolateyuninstall.ps1   Uninstall intent (Chocolatey auto-removes shim/files)
    VERIFICATION.txt          Required by community moderators (downloaded binary)
    LICENSE.txt               License text shipped in the package
```

The package downloads the official release archive from GitHub Releases at install time
(rather than embedding the binary) and selects the **x64** or **ARM64** build to match the
machine. Both architectures are produced by the `build-windows` matrix in
`.github/workflows/release.yml`.

### How it ships

On a `v*.*.*` tag, the `publish-chocolatey` job:

1. Downloads the x64 and ARM64 release archives (workflow artifacts).
2. Runs `build-choco.ps1` to compute checksums, stamp tokens, and `choco pack`.
3. `choco push`es the `.nupkg` to the Chocolatey Community Repository
   (`https://push.chocolatey.org/`) using the `CHOCO_API_KEY` secret.

> The **first** community submission goes through moderation (automated + human) and may
> take days to weeks. Subsequent versions are typically auto-approved.

### Local testing

From this folder (requires `choco` installed):

```powershell
# Build a package against existing release archives (downloads or local zips):
./build-choco.ps1 -Version 1.3.0 -X64Zip .\x64.zip -Arm64Zip .\arm64.zip

# Install the local package and verify the shim resolves:
choco install taskmon -s ".\dist" -y
taskmon --version

# Clean up:
choco uninstall taskmon -y
```

## winget (`winget/`)

```
winget/
  taskmon.* manifests        Version, installer, and locale YAML templates (in templates/)
  build-winget.ps1           Stamps version + checksums into dist/ for submission
```

The package is published to the [Windows Package Manager Community
Repository](https://github.com/microsoft/winget-pkgs) under the identifier
`jchas2.taskmon`, reusing the same GitHub Release archives and SHA256 checksums as the
Chocolatey package. Because winget has no per-project feed, releases are shipped by
opening a PR to `microsoft/winget-pkgs` via `wingetcreate`.

On a `v*.*.*` tag, the `publish-winget` job stamps the manifests and submits the PR using
the `WINGET_TOKEN` secret. The **first** version of a new package must be bootstrapped
manually. See [`winget/README.md`](winget/README.md) for setup, secrets, and local
testing.
