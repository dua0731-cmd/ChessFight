# Blender 연결 (BlenderMCP) · 블렌더 스킬 — PC마다 한 번 (seoungtest, 2026-11-16)

마차뜰 모델 작업(`ArtSource/PawnRush/CarriageYardModels`, [RCY_MODELS](../../Docs/PawnRush/RCY_MODELS.md))을 Claude Code가 Blender로 하기 위한 준비.

## 저장소에 들어 있는 것 (`git pull`이면 옴)

- **블렌더 스킬 94개:** `.claude/skills/` (github.com/arjun988/blender-skills, 커밋 904f095, MIT — `.claude/skills/LICENSE`). 프로젝트 스킬이라
  이 폴더에서 연 Claude Code 새 세션이 바로 읽는다(qa-review · blender-director · asset-optimization · lod-pipeline · rigging 등).
- **애드온 설치 스크립트:** `Tools/BlenderMCP/install_blendermcp.py`(Blender 백그라운드로 애드온을 설치 · 켜고 설정 저장).

## PC마다 할 것 (저장소에 넣을 수 없는 것)

1. **Blender 5.2**(마차뜰 스크립트가 5.2 기준): `winget install --id BlenderFoundation.Blender --version 5.2.3 --exact`
2. **uv**(MCP 서버를 돌림): `winget install --id astral-sh.uv --exact` → 새 터미널부터 `uvx`가 잡힘.
3. **BlenderMCP 애드온 2.0** — `addon.py`를 github.com/ahujasid/blender-mcp 에서 받아(라이선스가 확인되지 않아 저장소에 안 넣음) 설치:
   `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python Tools/BlenderMCP/install_blendermcp.py -- <받은 addon.py 경로>`
   (또는 Blender > Edit > Preferences > Add-ons > Install에서 `addon.py` 고르고 켜기). 켜 두면 Blender를 열 때 localhost:9876으로 자동 연결 대기,
   애드온 쪽 사용 데이터 전송(telemetry)은 기본 꺼짐. 받을 때 커밋을 고정하면 PC마다 같은 파일이 된다(11-16 세 번째 PC:
   `raw.githubusercontent.com/ahujasid/blender-mcp/b6a2ce30c14636a562b5549684d9db6fc01f29ab/addon.py`, 2.0, sha256 `35e5f317…5f39a05`).
   설치 스크립트를 `--factory-startup`과 같이 돌리면 그 PC의 기존 Blender 설정을 공장값 + 애드온으로 덮어쓴다 — 위 명령처럼 빼고 돌릴 것
   (11-16 세 번째 PC는 설정 폴더가 없어서 붙여도 잃은 것 없음).
4. **MCP 서버 등록**(이 PC의 Claude 설정에만 들어감), 이 폴더에서 Git Bash로:
   `claude mcp add blender -s local -e DISABLE_TELEMETRY=true -e BLENDER_MCP_DISABLE_TELEMETRY=true -- uvx --from blender-mcp==2.0.0 blender-mcp`
   - **`-e DISABLE_TELEMETRY=true`를 꼭 넣는다:** 애드온과 별개로 MCP 서버 패키지(blender-mcp 2.0.0)가 켜질 때마다 개발사 서버(supabase)로
     사용 기록을 보낸다(11-16 서버 로그에서 확인, 이 변수를 넣으면 "Telemetry disabled via environment variable"). 이미 등록한 PC(HOSEO)는
     `claude mcp remove blender -s local` 후 이 줄로 다시 등록.
   - `-e`는 서버 이름 **뒤에** 쓴다(앞에 쓰면 이름까지 환경 변수로 읽어 오류). PowerShell에서는 `--`가 먹혀서 오류 — Git Bash로.
   - `claude`가 PATH에 없으면(데스크톱 앱만 쓰는 PC) 앱이 쓰는 `claude.exe`의 전체 경로로: `%LOCALAPPDATA%\Claude-Data\claude_*\claude-code\<버전>\<해시>\claude.exe`.
     `uvx`가 PATH에 없으면 winget이 깐 `%LOCALAPPDATA%\Microsoft\WinGet\Packages\astral-sh.uv_*\uvx.exe`의 전체 경로.
   - uv가 처음 받은 파이썬에서 `Missing expected target directory for Python minor version link`로 서버가 안 뜨면(11-16 세 번째 PC, 파이썬
     3.15.2), uvx 인자에 받은 파이썬을 직접 준다: `--python %APPDATA%\uv\python\cpython-3.15.2-windows-x86_64-none\python.exe`.
   - 확인: `claude mcp get blender` → `✔ Connected`.
5. Blender를 열어 둔 채 이 폴더에서 Claude Code **새 세션** — `mcp__blender__*` 도구와 스킬이 잡힌다. 연결 시험: "블렌더 현재 장면 정보 가져와".

**세팅한 PC:** HOSEO PC(11-16, 처음), 세 번째 PC `C:\Users\Makebot2\Documents\GitHub\ChessFight`(11-16, uv 0.13.8 · 애드온 2.0 고정 커밋 ·
서버 등록에 사용 데이터 끔 · 파이썬 경로 직접 — `✔ Connected`, Blender 9876 대기 확인. `mcp__blender__*`는 새 세션부터).
**브랜치 주의:** 스킬 94개와 이 폴더는 seoungtest에만 있다. GitHub Desktop이 main으로 바꾼 채 새 세션을 열면 스킬이 안 잡힌다.

Blender를 열지 않고도 일괄 작업은 된다: `blender -b --factory-startup --python <스크립트>`(QA 도구 `ArtSource/PawnRush/CarriageYardModels/qa/`).
**Unity가 Play 중일 때 스크립트 · FBX를 쓰지 말 것**(Play 도중 재컴파일 → 래그돌 NullReferenceException, RCY_MODELS '다음 작업 메모').
