# Contributing to taskmon

Thanks for your interest in improving taskmon. This page covers what you need to know before opening
a pull request.

## Before you start

- For anything bigger than a small fix, open an issue first so the approach can be agreed before you
  put time into it.
- Pull requests target the `main` branch.

## Building and testing

Setup, build, test, publish and debugging instructions are in the README's
[Development Guide](../README.md#development-guide). In short:

| | macOS | Windows |
|---|---|---|
| Build | `./build.sh` | `.\build.ps1` |
| Test | `./test.sh` | `.\test.ps1` |
| Check style | `./style.sh` | `.\style.ps1` |

taskmon runs on **macOS (Apple Silicon)** and **Windows**. A normal build only compiles your own
platform's `#if` code. On macOS, `./build-win32.ps1` compiles the Windows code paths as a check. CI
builds and tests both platforms, so make sure the `build-macos` and `build-windows` jobs pass on your
pull request.

## Code style

Follow [docs/CODING_STYLE.md](../docs/CODING_STYLE.md). The short version:

- Your IDE picks up [`.editorconfig`](../.editorconfig) for line endings, indentation, braces and
  naming.
- Class members go in a fixed order: fields, events, constructors, then properties and methods in
  one A–Z list.
- Related declarations and assignments are aligned in columns. Don't run a whole-file reformat
  (`dotnet format whitespace`, IDE "Reformat Code"), because it removes the alignment.
- Native calls belong in the `Task.Monitor.Interop.Mach` and `Task.Monitor.Interop.Win32` projects,
  and platform code goes in `.MacOS.cs` / `.Windows.cs` partial files.

## Dependencies

taskmon takes **no third-party runtime dependencies**. A small CPU and memory footprint is a core
design goal, so please don't add NuGet packages to application projects. Test projects use xUnit
and Moq.

## Pull request checklist

- [ ] Builds and tests pass on macOS and Windows (CI).
- [ ] New behaviour has tests in the matching project under `tests/`.
- [ ] Code follows [docs/CODING_STYLE.md](../docs/CODING_STYLE.md), and `./style.sh` (or `.\style.ps1`) passes.
- [ ] No new third-party runtime dependencies.
- [ ] The README is updated if you changed command-line options, configuration, themes or layouts.

## Releases

Releases are cut by the maintainer from git tags. See [docs/RELEASING.md](../docs/RELEASING.md).
