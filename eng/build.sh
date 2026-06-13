#!/bin/bash

# Exit immediately if any command fails, and propagate failures through pipes.
# Without this, a failing `dotnet test` is masked by the final `echo` (exit 0),
# causing CI to report a false-positive green build.
set -e
set -o pipefail

configuration="Debug"
runtime="osx-arm64"

usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -c, --config <config>   Specify the build configuration (e.g., Release). Default: Debug"
    echo "  -r, --runtime <rid>     Specify the runtime identifier. Default: osx-arm64 (Intel macOS not supported)"
    echo "  --clean                 Clean the solution."
    echo "  --restore               Restore NuGet packages."
    echo "  --build                 Build the solution."
    echo "  --test                  Run tests."
    echo "  --publish               Publish the project."
    echo "  --deploy                Publish the project with 'Release' configuration (overrides --config)."
    echo "  -h, --help              Display this help message."
    echo ""
    echo "Example: $0 --clean --restore --build --test --publish --config Release --runtime osx-arm64"
    exit 1
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        -c|--config)
            configuration="$2"
            shift # Skip argument name
            ;;
        -r|--runtime)
            runtime="$2"
            shift # Skip argument name
            ;;
        --clean)
            clean=true
            ;;
        --restore)
            restore=true
            ;;
        --build)
            build=true
            ;;
        --test)
            test=true
            ;;
        --publish)
            publish=true
            ;;
        --deploy)
            deploy=true
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo "Error: Unknown parameter $1"
            usage
            ;;
    esac
    shift # Move to next argument/value
done

# --- Validate Runtime ---
if [[ "$runtime" == "osx-x64" ]]; then
    echo "Error: Intel macOS (osx-x64) is not supported. Use osx-arm64 for Apple Silicon."
    exit 1
fi

# --- Define Paths ---
# In bash, $0 is the script name. PWD is the current working directory.
# The `dirname` command is used to get the directory of the script.
ScriptDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"

SolutionPath="$ScriptDir/../src/taskmon.sln"
ProjectPath="$ScriptDir/../src/taskmon/taskmon.csproj"

function publish_project() {
    local config=$1
    local rid=$2
    echo "🛠️ Publishing project with configuration: $config, runtime: $rid"
    dotnet publish "$ProjectPath" -c "$config" -r "$rid" --self-contained -p:PublishAot=true
}

if [[ "$clean" == true ]]; then
    echo "Cleaning solution..."
    dotnet clean "$SolutionPath"
fi

if [[ "$restore" == true ]]; then
    echo "Restoring NuGet packages..."
    dotnet restore "$SolutionPath"
fi

if [[ "$build" == true ]]; then
    echo "Building solution with configuration: $configuration"
    # Configuration is passed via -c/--configuration flag, not /p:configuration=
    dotnet build "$SolutionPath" --configuration "$configuration" /p:buildtests=true
fi

if [[ "$test" == true ]]; then
    echo "Running tests..."
    dotnet test "$SolutionPath"
fi

if [[ "$publish" == true ]]; then
    publish_project "$configuration" "$runtime"
fi

if [[ "$deploy" == true ]]; then
    # Overrides any passed configuration to force 'Release'
    publish_project "Release" "$runtime"
fi

echo "Script finished."
