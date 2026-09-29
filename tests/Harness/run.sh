#!/usr/bin/env bash
# Runs the real Harmony patches against stub Valheim/Unity/BepInEx types under Mono.
# Checks patch wiring (names, positional args, reverse patch, vegetation), not in-game behaviour.
#
# Needs: mono, a Roslyn csc.exe (e.g. from the Microsoft.Net.Compilers NuGet package), and a
# directory holding HarmonyX's 0Harmony.dll plus its MonoMod/Mono.Cecil dependencies.
#   CSC=/path/to/csc.exe HARMONY_DIR=/path/to/dlls tests/Harness/run.sh
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
src="$here/../../src/Scotheim"
out="$(mktemp -d)"
mono_lib="${MONO_LIB:-/usr/lib/mono/4.5}"
mono "$CSC" -nologo -langversion:7.3 -nowarn:1701,1702 -out:"$out/harness.exe" \
  -r:"$HARMONY_DIR/0Harmony.dll" -r:"$mono_lib/Facades/netstandard.dll" -r:"$mono_lib/System.Core.dll" \
  -resource:"$src/Data/expand_biomes_scotheim.yaml",Scotheim.Data.expand_biomes_scotheim.yaml \
  -resource:"$src/Data/expand_vegetation_scotheim.yaml",Scotheim.Data.expand_vegetation_scotheim.yaml \
  "$here/Stubs.cs" "$here/Harness.cs" "$src/Plugin.cs" "$src"/Patches/*.cs "$src"/Terrain/*.cs
cp "$HARMONY_DIR"/*.dll "$out/"
mono "$out/harness.exe"
