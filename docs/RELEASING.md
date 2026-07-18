# Release Process

This document describes how to create and publish a new release of Task Monitor.

## Overview

Releases are fully automated via GitHub Actions. When you push a git tag, the workflow:
1. Builds binaries for all platforms (macOS ARM64, Windows x64)
2. Creates a GitHub Release with all artifacts and SHA256 checksums
3. Automatically updates the Homebrew formula with the new version
4. Users can install/upgrade via Homebrew or direct download

## Prerequisites

### 1. Choose a Version Number

Use [Semantic Versioning](https://semver.org/):
- **MAJOR** version for incompatible API changes
- **MINOR** version for new functionality in a backward compatible manner
- **PATCH** version for backward compatible bug fixes

Examples: `1.3.0`, `2.0.0`, `1.3.1`

### 2. Create and Push the Tag

Create an annotated tag with a release message:

```bash
# Replace 1.3.0 with your version
git tag -a v1.3.0 -m "Release v1.3.0"

# Push the tag to GitHub
git push origin v1.3.0
```

**Important**: The tag must start with `v` followed by the version number (e.g., `v1.3.0`).

### 3. Monitor the Release Workflow

1. Go to the [Actions tab](https://github.com/jchas2/taskmon/actions)
2. Find the "Release" workflow run for your tag
3. Monitor the progress of all jobs:
   - `create-release` - Creates the GitHub Release
   - `build-macos` - Builds macOS ARM64 binary
   - `build-windows` - Builds Windows binary
   - `update-homebrew` - Updates Homebrew formula

The entire process takes approximately 1-3 minutes.

### 5. Verify the Release

After the workflow completes successfully:

1. **Check the GitHub Release**:
   - Go to [Releases](https://github.com/jchas2/taskmon/releases)
   - Verify all artifacts are present:
     - `taskmon-{version}-macos-arm64.tar.gz`
     - `taskmon-{version}-macos-arm64.tar.gz.sha256`
     - `taskmon-{version}-windows-x64.zip`
     - `taskmon-{version}-windows-x64.zip.sha256`

2. **Check Homebrew Formula**:
   - Go to [homebrew-taskmon](https://github.com/jchas2/homebrew-taskmon)
   - Verify the formula was updated with the new version
   - Check that SHA256 checksums were updated

3. **Test Installation**:

   macOS (Homebrew):
   ```bash
   brew update
   brew upgrade taskmon
   taskmon --version
   # Should show: taskmon version {version}
   ```

## Manual Release (Alternative)

If you need to trigger a release manually without creating a tag:

1. Go to [Actions](https://github.com/jchas2/taskmon-cli/actions)
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
   - Ensure the token has access to `jchas2/homebrew-taskmon`

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
cd src/taskmon/bin/Release/net10.0/osx-arm64/publish
tar -czf taskmon-1.3.0-rc1-macos-arm64.tar.gz taskmon
shasum -a 256 taskmon-1.3.0-rc1-macos-arm64.tar.gz
```

### Test Homebrew Formula Locally

```bash
# Edit homebrew-tap/Formula/taskmon.rb with test values
# Then test installation:
brew install --build-from-source homebrew-tap/Formula/taskmon.rb
```

## Version History

Releases are tracked in:
- [GitHub Releases](https://github.com/jchas2/taskmon/releases)
- [Homebrew Formula](https://github.com/jchas2/homebrew-taskmon/commits/main/Formula/taskmgr.rb)
