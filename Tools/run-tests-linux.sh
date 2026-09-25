#!/usr/bin/env bash
# Linux/cloud equivalent of Tools/Test-NetworkCore.ps1 and Tools/Test-NetworkCompile.ps1,
# for AI sessions and machines without Unity.
#
#   Tools/run-tests-linux.sh             Core tests + simulated Steam session tests
#   Tools/run-tests-linux.sh --compile   ...and compile every runtime assembly against
#                                        Unity reference DLLs and the pinned Steamworks.NET source
#
# The compile step uses Roslyn (the compiler Unity uses, from the .NET 8 SDK) but
# Unity 2021.3 reference DLLs (NuGet "UnityEngine.Modules"), so Unity 6 API renames
# are mapped back in a temporary copy (see unity6_to_2021). It is a strong check,
# not Unity itself. Input/ (needs the Input System package) and editor code
# (needs UnityEditor.dll) are skipped. Tests still use Mono's mcs.
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

# The reference DLLs are Unity 2021.3, so Unity 6 renames are mapped back in a
# temporary copy. The real sources are never touched.
unity6_to_2021() {
  local dir="$1"
  grep -rlE 'linearVelocity|linearDamping|angularDamping|PhysicsMaterial' "$dir" | xargs -r sed -i \
    -e 's/\.linearVelocity/.velocity/g' -e 's/\.linearDamping/.drag/g' -e 's/\.angularDamping/.angularDrag/g' \
    -e 's/PhysicsMaterialCombine/PhysicMaterialCombine/g' -e 's/PhysicsMaterial/PhysicMaterial/g'
  # Physics.simulationMode is Unity 2022.1+; the 2021.3 equivalent is autoSimulation.
  grep -rl 'Physics.simulationMode' "$dir" | xargs -r sed -i \
    -e 's/Physics\.simulationMode = SimulationMode\.Script/Physics.autoSimulation = false/' \
    -e 's/Physics\.simulationMode = SimulationMode\.FixedUpdate/Physics.autoSimulation = true/'
}

ensure_roslyn() {
  CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1 || true)
  if [ -n "$CSC" ]; then return; fi
  echo "Installing the .NET 8 SDK for the Roslyn compiler Unity uses..."
  local sudo=""; [ "$(id -u)" -ne 0 ] && sudo="sudo"
  $sudo apt-get update -q >/dev/null; $sudo apt-get install -y -q dotnet-sdk-8.0 >/dev/null
  CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
}

compile_all() {
  fetch_references
  ensure_roslyn
  local build="$OUT/build" src="$OUT/src" refs sym m=/usr/lib/mono/4.5
  rm -rf "$build" "$src"; mkdir -p "$build" "$src"
  cp -r Assets/Scripts "$src/Scripts"; cp -r Assets/ChessFight/RagdollLab/Scripts "$src/RagdollLab"; cp -r Assets/ChessFight/RagdollLabSteam "$src/RagdollLabSteam"
  unity6_to_2021 "$src"
  refs=$(ls "$OUT"/unity/lib/net45/UnityEngine*.dll | sed 's/^/-r:/' | tr '\n' ' ')
  sym="-define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_2017_1_OR_NEWER;UNITY_2019_3_OR_NEWER"
  local csc="dotnet $CSC -nologo -noconfig -nostdlib+ -target:library -langversion:9 -nowarn:414,649,169,8632,0618 -r:$m/mscorlib.dll -r:$m/System.dll -r:$m/System.Core.dll"
  echo "Compiling Steamworks.NET $STEAMWORKS_COMMIT..."
  $csc -unsafe $sym $refs -out:"$build/Steamworks.NET.dll" $(find "$OUT/steamworks/com.rlabrecque.steamworks.net/Runtime" -name '*.cs') >/dev/null
  $csc -out:"$build/ChessFight.Network.Core.dll" "$src"/Scripts/Core/*.cs
  $csc $sym $refs -r:"$build/Steamworks.NET.dll" -r:"$build/ChessFight.Network.Core.dll" \
       -out:"$build/ChessFight.Network.Steam.dll" "$src"/Scripts/Network/*.cs
  $csc $sym $refs -r:"$build/ChessFight.Network.Core.dll" -out:"$build/ChessFight.Game.dll" "$src"/Scripts/Game/*.cs
  $csc $sym $refs -r:"$build/ChessFight.Game.dll" -out:"$build/ChessFight.Gameplay.dll" $(find "$src/Scripts/Gameplay" -name '*.cs')
  $csc $sym $refs -r:"$build/Steamworks.NET.dll" -r:"$build/ChessFight.Network.Core.dll" -r:"$build/ChessFight.Network.Steam.dll" \
       -r:"$build/ChessFight.Game.dll" -r:"$build/ChessFight.Gameplay.dll" \
       -out:"$build/ChessFight.Game.Steam.dll" "$src"/Scripts/Bootstrap/*.cs
  # Player defines: its UNITY_EDITOR blocks need UnityEditor.dll, which the reference package lacks.
  $csc "-define:UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_2017_1_OR_NEWER;UNITY_2019_3_OR_NEWER" $refs \
       -r:"$build/ChessFight.Game.dll" -r:"$build/ChessFight.Gameplay.dll" \
       -out:"$build/ChessFight.RagdollLab.dll" $(find "$src/RagdollLab" -name '*.cs')
  $csc $sym $refs -r:"$build/Steamworks.NET.dll" -r:"$build/ChessFight.Network.Core.dll" -r:"$build/ChessFight.Network.Steam.dll" \
       -r:"$build/ChessFight.RagdollLab.dll" -r:"$build/ChessFight.Game.dll" -r:"$build/ChessFight.Gameplay.dll" \
       -out:"$build/ChessFight.RagdollLab.Net.dll" $(find "$src/RagdollLabSteam" -name '*.cs')
  echo "PASS: Core, Network.Steam, Game, Gameplay, Bootstrap, RagdollLab and RagdollLabSteam compiled with Roslyn (Input/ and Editor code skipped)."
}

# Assembly boundaries that keep gameplay work from reaching into the network layer.
check_boundaries() {
  local bad=0 hits
  # code_refs PATTERN PATHS...: matches outside // comments.
  code_refs() { local pattern="$1"; shift
    grep -rnE "$pattern" "$@" --include='*.cs' --include='*.asmdef' | grep -vE '^[^:]+:[0-9]+:[[:space:]]*//' || true; }
  # Only Network/ and Bootstrap/ may know about Steam (and RagdollLabSteam/, the lab's own
  # Bootstrap-style bridge, which is why it sits outside RagdollLab/).
  hits=$(code_refs 'Steamworks|ChessFight\.Network\.Steam|SteamSession|SteamMotion' \
         Assets/Scripts/Core Assets/Scripts/Game Assets/Scripts/Gameplay Assets/Scripts/Input Assets/ChessFight/RagdollLab)
  if [ -n "$hits" ]; then echo "$hits"; echo "FAIL: Steam referenced outside Network/ and Bootstrap/"; bad=1; fi
  # The network layer and scene flow never depend on a character implementation.
  hits=$(code_refs 'RagdollLab|RagdollPawn|LabGame' Assets/Scripts)
  if [ -n "$hits" ]; then echo "$hits"; echo "FAIL: Assets/Scripts depends on the ragdoll lab"; bad=1; fi
  [ $bad -eq 0 ] && echo "PASS: assembly boundaries (Steam only in Network/Bootstrap, nothing in Assets/Scripts depends on the ragdoll)"
  return $bad
}

ensure_mono
check_boundaries
run_tests
if [ "${1:-}" = "--compile" ]; then compile_all; fi
