#!/usr/bin/env bash
# Packs the console app as two NuGet tool packages and checks what each one contains:
#   OutroKit                  the app, with the command "outrokit"
#   PodcastMetadataGenerator  the same app under its original ID and command, which tells
#                             people to move to OutroKit
#
# Usage: pack-tool.sh <output-directory> [version]
set -euo pipefail

output="${1:?Usage: pack-tool.sh <output-directory> [version]}"
version="${2:-}"
project="src/Console/PodcastMetadataGenerator.Console.csproj"
marker="renamed-package.txt"

version_arguments=()
if [ -n "$version" ]; then
  version_arguments=("-p:Version=$version" "-p:PackageVersion=$version")
fi

mkdir -p "$output"
output="$(cd "$output" && pwd)"

dotnet pack "$project" --configuration Release --output "$output" ${version_arguments[@]+"${version_arguments[@]}"}
dotnet pack "$project" --configuration Release --output "$output" -p:LegacyPackage=true ${version_arguments[@]+"${version_arguments[@]}"}

# check <package ID> <command> <yes|no: has the renamed-package marker>
check() {
  local id="$1" command="$2" expect_marker="$3"
  local package
  package="$(ls "$output/$id".*.nupkg 2>/dev/null | head -n 1 || true)"
  if [ -z "$package" ]; then
    echo "error: no $id package in $output" >&2
    exit 1
  fi

  # Read each piece before searching it: grep -q exits at the first match, which under pipefail
  # would turn a successful search of a long listing into a failure.
  local nuspec settings listing
  nuspec="$(unzip -p "$package" '*.nuspec')"
  settings="$(unzip -p "$package" '*/DotnetToolSettings.xml')"
  listing="$(unzip -Z1 "$package")"

  grep -q "<id>$id</id>" <<<"$nuspec" \
    || { echo "error: $package does not have the ID $id" >&2; exit 1; }
  grep -q "Command Name=\"$command\"" <<<"$settings" \
    || { echo "error: $package does not install the command $command" >&2; exit 1; }
  if [ -n "$version" ]; then
    grep -q "<version>$version</version>" <<<"$nuspec" \
      || { echo "error: $package is not version $version" >&2; exit 1; }
  fi

  # The app looks for the marker in its own folder, so that is where it has to be.
  local app_path app_folder has_marker=no
  app_path="$(grep -m 1 '/outrokit\.dll$' <<<"$listing" || true)"
  app_folder="${app_path%/*}"
  [ -n "$app_folder" ] || { echo "error: $package does not contain outrokit.dll" >&2; exit 1; }
  if grep -qx "$app_folder/$marker" <<<"$listing"; then
    has_marker=yes
  fi
  if [ "$has_marker" != "$expect_marker" ]; then
    echo "error: $package: $marker beside the app=$has_marker, expected $expect_marker" >&2
    exit 1
  fi

  echo "ok: $(basename "$package") installs the command \"$command\""
}

check OutroKit outrokit no
check PodcastMetadataGenerator podcast-metadata-generator yes
