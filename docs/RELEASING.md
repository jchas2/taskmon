# Release Process

This document describes how to create and publish a new release of taskmgr.

## Overview

Releases are fully automated via GitHub Actions. When you push a git tag, the workflow:
1. Builds binaries for all platforms (macOS ARM64, Windows x64)
2. Creates a GitHub Release with all artifacts and SHA256 checksums
3. Automatically updates the Homebrew formula with the new version
4. Users can install/upgrade via Homebrew or direct download

## Prerequisites

Before creating your first release, ensure:
- [ ] Homebrew tap repository (`jchas2/homebrew-taskmgr`) is set up
- [ ] `HOMEBREW_TAP_TOKEN` secret is configured in the taskmgr-cli repository
- [ ] All tests pass locally
- [ ] CHANGELOG is updated (if you maintain one)

See [Phase 3 Setup](../PHASE3_MANUAL_STEPS.md) for initial configuration.

## Creating a Release

### 1. Prepare the Release

Ensure your main branch is up to date and all changes are merged:

```bash
git checkout main
git pull origin main
```

Run tests to verify everything works:

```bash
# macOS
./eng/build.sh --restore --build --test --config Release

# Windows
.\eng\build.ps1 -restore -build -test -config Release
```

### 2. Choose a Version Number

Use [Semantic Versioning](https://semver.org/):
- **MAJOR** version for incompatible API changes
- **MINOR** version for new functionality in a backward compatible manner
- **PATCH** version for backward compatible bug fixes

Examples: `1.3.0`, `2.0.0`, `1.3.1`

### 3. Create and Push the Tag

Create an annotated tag with a release message:

```bash
# Replace 1.3.0 with your version
git tag -a v1.3.0 -m "Release v1.3.0"

# Push the tag to GitHub
git push origin v1.3.0
```

**Important**: The tag must start with `v` followed by the version number (e.g., `v1.3.0`).

### 4. Monitor the Release Workflow

1. Go to the [Actions tab](https://github.com/jchas2/taskmgr-cli/actions)
2. Find the "Release" workflow run for your tag
3. Monitor the progress of all jobs:
   - `create-release` - Creates the GitHub Release
   - `build-macos` - Builds macOS ARM64 binary
   - `build-windows` - Builds Windows binary
   - `update-homebrew` - Updates Homebrew formula

The entire process takes approximately 10-15 minutes.

### 5. Verify the Release

After the workflow completes successfully:

1. **Check the GitHub Release**:
   - Go to [Releases](https://github.com/jchas2/taskmgr-cli/releases)
   - Verify all artifacts are present:
     - `taskmgr-{version}-macos-arm64.tar.gz`
     - `taskmgr-{version}-macos-arm64.tar.gz.sha256`
     - `taskmgr-{version}-windows-x64.zip`
     - `taskmgr-{version}-windows-x64.zip.sha256`

2. **Check Homebrew Formula**:
   - Go to [homebrew-taskmgr](https://github.com/jchas2/homebrew-taskmgr)
   - Verify the formula was updated with the new version
   - Check that SHA256 checksums were updated

3. **Test Installation**:

   macOS (Homebrew):
   ```bash
   brew update
   brew upgrade taskmgr
   taskmgr --version
   # Should show: taskmgr version {version}
   ```

   Direct download:
   ```bash
   # Download and extract the binary for your platform
   # Then verify:
   ./taskmgr --version
   ```

## Manual Release (Alternative)

If you need to trigger a release manually without creating a tag:

1. Go to [Actions](https://github.com/jchas2/taskmgr-cli/actions)
2. Select the "Release" workflow
3. Click "Run workflow"
4. Enter the version (without the `v` prefix, e.g., `1.3.0`)
5. Click "Run workflow"

Note: This will create both the tag and the release.

## Troubleshooting

### Build Fails

**Symptom**: One of the build jobs fails

**Solutions**:
- Check the logs in GitHub Actions for specific error messages
- Verify the code builds locally on your machine
- Ensure .NET 10.0 SDK is properly referenced in the workflow

### Homebrew Update Fails

**Symptom**: The `update-homebrew` job fails

**Solutions**:
1. **Check token permissions**:
   - Go to GitHub Settings → Developer settings → Personal access tokens
   - Verify the token hasn't expired
   - Ensure it has "Contents: Read and write" permission

2. **Verify secret is configured**:
   - Go to repository Settings → Secrets and variables → Actions
   - Ensure `HOMEBREW_TAP_TOKEN` exists and is valid

3. **Check repository access**:
   - Ensure the token has access to `jchas2/homebrew-taskmgr`

### Wrong Version Number

**Symptom**: You pushed a tag with the wrong version

**Solutions**:
```bash
# Delete the tag locally
git tag -d v1.3.0

# Delete the tag from GitHub
git push origin :refs/tags/v1.3.0

# Delete the release from GitHub (manually through UI)
# Then recreate with correct version
```

### Release Artifacts Missing

**Symptom**: Some artifacts didn't upload

**Solutions**:
- Check that all build jobs completed successfully
- Verify the artifact paths in the workflow match the actual build output
- Re-run the workflow if needed

## Testing a Release Locally

Before creating an official release, you can test the build process locally:

### Test Build with Release Version

```bash
# macOS
RELEASE_VERSION=1.3.0-rc1 ./eng/build.sh --restore --publish --config Release --runtime osx-arm64

# Windows
$env:RELEASE_VERSION="1.3.0-rc1"
.\eng\build.ps1 -restore -publish -config Release -runtime win-x64
```

### Test Archive Creation

```bash
cd src/taskmgr/bin/Release/net10.0/osx-arm64/publish
tar -czf taskmgr-1.3.0-rc1-macos-arm64.tar.gz taskmgr
shasum -a 256 taskmgr-1.3.0-rc1-macos-arm64.tar.gz
```

### Test Homebrew Formula Locally

```bash
# Edit homebrew-tap/Formula/taskmgr.rb with test values
# Then test installation:
brew install --build-from-source homebrew-tap/Formula/taskmgr.rb
```

## Version History

Releases are tracked in:
- [GitHub Releases](https://github.com/jchas2/taskmgr-cli/releases)
- [Homebrew Formula](https://github.com/jchas2/homebrew-taskmgr/commits/main/Formula/taskmgr.rb)
