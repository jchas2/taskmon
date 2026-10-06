#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the taskmon solution with the Windows (__WIN32__) code paths compiled in,
    from a non-Windows host (e.g. macOS).

.DESCRIPTION
    eng/Preprocessor.props selects the platform preprocessor constant from the BUILD HOST
    OS: on macOS it defines __APPLE__, on Windows __WIN32__. A normal `dotnet build` on a
    Mac therefore only ever compiles the __APPLE__ partials and never catches regressions
    inside `#if __WIN32__` blocks (missing usings, renamed members, etc.).

    This script overrides DefineConstants with __WIN32__ (a global MSBuild property, which
    replaces the host-selected value) so the Windows surface of every project is actually
    compiled. __APPLE__ is left undefined, so the macOS partials are excluded.

    This is a COMPILE CHECK only. Windows-only BCL types (Registry, SecurityIdentifier,
    PDH/DXGI interop, ...) resolve at compile time on net10.0 but are runtime-guarded for
    Windows, so the produced binaries are not meant to run on macOS.

    Any additional arguments are forwarded verbatim to `dotnet build`
    (e.g. -c Release, -v q, --no-restore).

.EXAMPLE
    ./build-win32.ps1
    ./build-win32.ps1 -c Release
    ./build-win32.ps1 -v q
#>

$ErrorActionPreference = 'Stop'

$solution = Join-Path $PSScriptRoot 'src' 'taskmon.sln'

Write-Host "Building $solution with __WIN32__ preprocessor directives..." -ForegroundColor Cyan

# $args (not a declared parameter) forwards flags like -v/-c verbatim, without PowerShell
# mistaking them for advanced-function common parameters.
dotnet build $solution -p:DefineConstants=__WIN32__ @args

exit $LASTEXITCODE
