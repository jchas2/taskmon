[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('config')]$configuration = "Debug",
    [string][Alias('r')]$runtime = "win-x64",
    [switch] $clean,
    [switch] $restore,
    [switch] $build,
    [switch] $test,
    [switch] $style,
    [switch] $publish,
    [switch] $deploy
)

$SolutionPath = $PSScriptRoot + "\..\src\taskmon.sln"
$ProjectPath = $PSScriptRoot + "\..\src\taskmon\taskmon.csproj"

# Validate runtime - Intel macOS not supported
if ($runtime -eq "osx-x64") {
    Write-Error "Error: Intel macOS (osx-x64) is not supported. Use osx-arm64 for Apple Silicon."
    exit 1
}

# Stops at the first failing dotnet command, like `set -e` in eng/build.sh, so a failed build or
# test run exits non-zero instead of being masked by the steps after it.
function Invoke-Dotnet {
    & "dotnet" @args
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Publish([string] $config, [string] $rid) {
    Write-Host "🛠️ Publishing project with configuration: $config, runtime: $rid"
    Invoke-Dotnet publish $ProjectPath -c $config -r $rid --self-contained -p:PublishAot=true
}

# Runs every check before failing, so one run reports all style problems. See docs/CODING_STYLE.md.
function Test-Style {
    $failed = $false

    Push-Location (Join-Path $PSScriptRoot "..")
    try {
        Write-Host "Checking code style (dotnet format style)..."
        & "dotnet" format style src/taskmon.sln --verify-no-changes --severity warn
        if ($LASTEXITCODE -ne 0) { $failed = $true }

        Write-Host "Checking member ordering (eng/tools/reorder-members.cs)..."
        & "dotnet" run eng/tools/reorder-members.cs '--' --check src tests eng/tools
        if ($LASTEXITCODE -ne 0) { $failed = $true }
    }
    finally {
        Pop-Location
    }

    if ($failed) {
        Write-Host "Style check failed. See docs/CODING_STYLE.md." -ForegroundColor Red
        exit 1
    }

    Write-Host "Style check passed."
}

if ($clean) {
    Invoke-Dotnet clean $SolutionPath
}

if ($build) {
    Invoke-Dotnet build $SolutionPath /p:configuration=$configuration /p:buildtests=true
}

if ($restore) {
    Invoke-Dotnet restore $SolutionPath
}

if ($test) {
    Invoke-Dotnet test $SolutionPath
}

if ($style) {
    Test-Style
}

if ($publish) {
    Publish -config $configuration -rid $runtime
}

if ($deploy) {
    Publish -config Release -rid $runtime
}
