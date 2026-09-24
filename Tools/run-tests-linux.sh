#!/usr/bin/env bash
# Linux/cloud equivalent of Tools/Test-NetworkCore.ps1 and Tools/Test-NetworkCompile.ps1,
# for AI sessions and machines without Unity.
#
#   Tools/run-tests-linux.sh             Core tests + simulated Steam session tests
#   Tools/run-tests-linux.sh --compile   ...and compile every runtime assembly against
#                                        Unity reference DLLs and the pinned Steamworks.NET source
#
# The compile step is a strong check, not Unity itself: the reference DLLs are
# Unity 2021.3 (NuGet "UnityEngine.Modules"), and mcs cannot parse a few newer
# constructs, which are patched in a temporary copy only (see patch_for_mcs).
# Input/ (needs the Input System package) and Editor/ (needs UnityEditor.dll) are skipped.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${CHESSFIGHT_TEST_OUT:-$ROOT/Temp/LinuxTests}"
STEAMWORKS_COMMIT=c21a8f0e31c56ae8707130967faf491f7dd7c0d8
UNITY_REFS_VERSION=2021.3.33
mkdir -p "$OUT"
cd "$ROOT"

ensure_mono() {
  if command -v mcs >/dev/null && command -v mono >/dev/null &&
     ls /usr/lib/mono/4.5/System.Web.Extensions.dll >/dev/null 2>&1; then return; fi
  echo "Installing Mono (mcs, runtime, System.Web.Extensions)..."
  local sudo=""; [ "$(id -u)" -ne 0 ] && sudo="sudo"
  $sudo apt-get install -y -q mono-mcs mono-runtime libmono-system-web-extensions4.0-cil >/dev/null 2>&1 ||
    { $sudo apt-get update -q >/dev/null && $sudo apt-get install -y -q mono-mcs mono-runtime libmono-system-web-extensions4.0-cil >/dev/null; }
}

run_tests() {
  mcs -nologo -langversion:experimental -out:"$OUT/NetworkCoreTests.exe" \
      Assets/Scripts/Core/*.cs Tests/Network/NetworkCoreTests.cs
  mono "$OUT/NetworkCoreTests.exe"
  mcs -nologo -langversion:experimental -nowarn:414 -r:System.Web.Extensions.dll -out:"$OUT/SessionFlowTests.exe" \
      Assets/Scripts/Core/*.cs Assets/Scripts/Network/SteamSession.cs Tests/Network/FakeSteam.cs Tests/Network/SessionFlowTests.cs
  mono "$OUT/SessionFlowTests.exe"
}

fetch_references() {
  if [ ! -d "$OUT/steamworks/.git" ]; then
    git clone -q --filter=blob:none https://github.com/rlabrecque/Steamworks.NET.git "$OUT/steamworks"
  fi
  git -C "$OUT/steamworks" checkout -q "$STEAMWORKS_COMMIT"
  if [ ! -f "$OUT/unity/lib/net45/UnityEngine.dll" ]; then
    mkdir -p "$OUT/unity"
    curl -sSL -o "$OUT/unity/refs.nupkg" \
      "https://api.nuget.org/v3-flatcontainer/unityengine.modules/$UNITY_REFS_VERSION/unityengine.modules.$UNITY_REFS_VERSION.nupkg"
    (cd "$OUT/unity" && unzip -qo refs.nupkg)
  fi
}

# Valid C# that mcs or the 2021.3 reference DLLs cannot take. Patched in a copy only.
patch_for_mcs() {
  local dir="$1"
  sed -i 's/(rotation \* Quaternion.Inverse(lastRotation)).ToAngleAxis/var delta = rotation * Quaternion.Inverse(lastRotation); delta.ToAngleAxis/' \
      "$dir/Obstacles/Obstacle.cs"
  # Unity 6 renamed Rigidbody.velocity to linearVelocity.
  grep -rl 'linearVelocity' "$dir" | xargs -r sed -i 's/\.linearVelocity/.velocity/g'
}

compile_all() {
  fetch_references
  local build="$OUT/build" unity refs sym
  mkdir -p "$build"
  refs=$(ls "$OUT"/unity/lib/net45/UnityEngine*.dll | sed 's/^/-r:/' | tr '\n' ' ')
  sym="-define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_2017_1_OR_NEWER;UNITY_2019_3_OR_NEWER"
  local mcsl="mcs -nologo -langversion:experimental -target:library -nowarn:414,649,169"
  echo "Compiling Steamworks.NET $STEAMWORKS_COMMIT..."
  $mcsl -unsafe $sym $refs -out:"$build/Steamworks.NET.dll" $(find "$OUT/steamworks/com.rlabrecque.steamworks.net/Runtime" -name '*.cs')
  $mcsl -out:"$build/ChessFight.Network.Core.dll" Assets/Scripts/Core/*.cs
  $mcsl $sym $refs -r:"$build/Steamworks.NET.dll" -r:"$build/ChessFight.Network.Core.dll" \
        -out:"$build/ChessFight.Network.Steam.dll" Assets/Scripts/Network/*.cs
  $mcsl $sym $refs -r:"$build/ChessFight.Network.Core.dll" -out:"$build/ChessFight.Game.dll" Assets/Scripts/Game/*.cs
  rm -rf "$OUT/gameplay" && cp -r Assets/Scripts/Gameplay "$OUT/gameplay" && patch_for_mcs "$OUT/gameplay"
  $mcsl $sym $refs -r:"$build/ChessFight.Game.dll" -out:"$build/ChessFight.Gameplay.dll" $(find "$OUT/gameplay" -name '*.cs')
  $mcsl $sym $refs -r:"$build/Steamworks.NET.dll" -r:"$build/ChessFight.Network.Core.dll" -r:"$build/ChessFight.Network.Steam.dll" \
        -r:"$build/ChessFight.Game.dll" -r:"$build/ChessFight.Gameplay.dll" \
        -out:"$build/ChessFight.Game.Steam.dll" Assets/Scripts/Bootstrap/*.cs
  echo "PASS: Core, Network.Steam, Game, Gameplay and Bootstrap compiled (Input/ and Editor/ skipped)."
}

ensure_mono
run_tests
if [ "${1:-}" = "--compile" ]; then compile_all; fi
