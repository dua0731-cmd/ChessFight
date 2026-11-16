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
   사용 데이터 전송(telemetry)은 기본 꺼짐.
4. **MCP 서버 등록**(이 PC의 Claude 설정에만 들어감), 이 폴더에서 Git Bash로:
   `claude mcp add -s local blender -- uvx --from blender-mcp==2.0.0 blender-mcp`
   (PowerShell에서는 `--`가 먹혀서 오류 — Git Bash로. `uvx`가 PATH에 없으면 winget이 깐 `uvx.exe`의 전체 경로를 쓴다.)
5. Blender를 열어 둔 채 이 폴더에서 Claude Code **새 세션** — `mcp__blender__*` 도구와 스킬이 잡힌다. 연결 시험: "블렌더 현재 장면 정보 가져와".

Blender를 열지 않고도 일괄 작업은 된다: `blender -b --factory-startup --python <스크립트>`(QA 도구 `ArtSource/PawnRush/CarriageYardModels/qa/`).
**Unity가 Play 중일 때 스크립트 · FBX를 쓰지 말 것**(Play 도중 재컴파일 → 래그돌 NullReferenceException, RCY_MODELS '다음 작업 메모').
