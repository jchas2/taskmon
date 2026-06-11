[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('config')]$configuration = "Debug",
    [string][Alias('r')]$runtime = "win-x64",
    [switch] $clean,
    [switch] $restore,
    [switch] $build,
    [switch] $test,
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

function Publish([string] $config, [string] $rid) {
    Write-Host "🛠️ Publishing project with configuration: $config, runtime: $rid"
    & "dotnet" publish $ProjectPath -c $config -r $rid --self-contained -p:PublishAot=true
}

if ($clean) {
    & "dotnet" clean $SolutionPath
}

if ($build) {
    & "dotnet" build $SolutionPath /p:configuration=$configuration /p:buildtests=true
}

if ($restore) {
    & "dotnet" restore $SolutionPath
}

if ($test) {
    & "dotnet" test $SolutionPath
}

if ($publish) {
    Publish -config $configuration -rid $runtime
}

if ($deploy) {
    Publish -config Release -rid $runtime
}
