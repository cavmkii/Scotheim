#!/usr/bin/env bash
# Compiles src/Scotheim/Content against the real Jotunn.dll (JotunnLib 2.30.2 from NuGet) and real Unity
# reference assemblies (UnityEngine.Modules from NuGet), with stubs for BepInEx and the game.
# This checks every Jötunn API call; game members are only as good as tests/Content/ValheimStub.cs.
#   CSC=/path/to/csc.exe JOTUNN_DLL=/path/to/Jotunn.dll UNITY_DIR=/path/to/UnityEngine.Modules/lib/net45 HARMONY_DLL=/path/to/0Harmony.dll tests/Content/run.sh
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="$(mktemp -d)"
csc() { mono "$CSC" -nologo -langversion:7.3 -nowarn:1701,1702 "$@"; }
csc -target:library -out:"$out/BepInEx.dll" -r:"$UNITY_DIR/UnityEngine.CoreModule.dll" "$here/BepInExStub.cs"
csc -target:library -out:"$out/assembly_valheim.dll" -r:"$UNITY_DIR/UnityEngine.CoreModule.dll" "$here/ValheimStub.cs"
csc -target:library -out:"$out/content.dll" -r:"$UNITY_DIR/UnityEngine.CoreModule.dll" -r:"$UNITY_DIR/UnityEngine.AssetBundleModule.dll" -r:"$out/BepInEx.dll" \
  -r:"$out/assembly_valheim.dll" -r:"$JOTUNN_DLL" -r:"$HARMONY_DLL" "$here/PluginShell.cs" "$here"/../../src/Scotheim/Content/*.cs
echo "Content compiles against Jötunn."
