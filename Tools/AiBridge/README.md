# GPT ↔ Claude 계정 로그인 연결

**API 키 없이**, 로그인된 Codex와 Claude Code가 답변을 주고받습니다. 기본 흐름은 **GPT 초안 → Claude 검토 → GPT 최종 답변**입니다.

## 이 PC에서 실행

1. **`Start.cmd`를 더블클릭**합니다.
2. 함께 다룰 내용을 입력합니다.
3. 먼저 답할 모델과 총 답변 횟수를 고릅니다. Enter를 누르면 GPT부터 3회입니다.
4. 화면에서 답변을 보고, 표시된 폴더의 **`final.md`**에서 최종 답변을 엽니다.

2026-11-05 이 PC에서는 사용자가 Claude 로그인을 마쳤고, Codex의 ChatGPT 로그인과 Claude Code의 Claude 계정 로그인이 모두 인식됩니다. 짧은 요청으로 GPT → Claude → GPT의 실제 전달 시험도 통과했습니다.

## 처음 사용하는 PC 또는 로그인 만료

**`Setup.cmd`를 실행**합니다. Claude Code가 없으면 공식 배포처에서 받아 SHA256 값을 대조한 뒤 이 도구의 `.local/bin`에만 설치합니다. 필요한 계정 로그인을 마치면 요청을 입력할 수 있습니다. 브라우저에 로그인 코드가 표시되면 함께 열린 터미널 창에 직접 붙여 넣습니다.

Node.js 22 이상과 Codex가 필요합니다. 이 PC에서는 Codex 앱에 포함된 실행 파일도 찾습니다. 로그인 정보의 저장·갱신은 각 공식 프로그램이 담당하며 연결 도구는 토큰을 읽거나 복사하지 않습니다.

각 계정의 이용 권한과 사용량 한도가 적용됩니다. 계정 모드는 API 키 환경변수를 자식 프로그램에 전달하지 않으며, API 키로 로그인된 경우에도 실행을 멈추고 계정 로그인을 안내합니다. API 방식이 필요하면 별도 [API 사용 안내](API.md)를 따릅니다.

## 전달되는 내용과 기록

새로 입력한 요청, 이번 실행의 앞선 답변, 사용자가 명시적으로 지정한 참고 파일을 주고받습니다. 기존 ChatGPT·Claude 앱의 대화는 자동으로 불러오지 않습니다.

`runs/account-날짜/`에 다음 파일을 저장합니다.

- `01-gpt.request.txt`·`02-claude.request.txt` 등: 각 프로그램에 보낸 요청
- `01-gpt.response.md`·`02-claude.response.md` 등: 받은 답변
- `conversation.json`: 전체 대화와 완료·실패 상태
- `final.md`: 모든 단계가 끝난 뒤의 최종 답변

**Ctrl+C**로 중단할 수 있습니다. 오류·로그인 만료·사용량 제한이 생기면 받은 답변을 남기고 멈추며 자동 재시도하지 않습니다. `.local/`과 기본 기록 폴더 `runs/`는 Git에서 제외됩니다.

## 명령으로 사용

```powershell
node Tools/AiBridge/cli-bridge.mjs --prompt "폰 러쉬 미니게임 아이디어를 검토해줘"
node Tools/AiBridge/cli-bridge.mjs --prompt "이 기획의 빠진 점을 찾아줘" --first claude --turns 4
node Tools/AiBridge/cli-bridge.mjs --prompt "이 문서를 검토해줘" --context "기획서.md"
node Tools/AiBridge/cli-bridge.mjs --check
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AiBridge/Start.ps1 -Check
```

답변 횟수는 2~8회, 각 프로그램의 응답 대기 한도는 180초입니다. 참고 파일은 최대 4개·합계 128,000자입니다. `--gpt-model`·`--claude-model`을 생략하면 각 공식 프로그램의 기본 모델을 사용합니다. 경로를 직접 지정해야 하는 PC에서는 `AI_BRIDGE_CODEX_PATH`·`AI_BRIDGE_CLAUDE_PATH`에 실제 실행 파일 경로를 넣습니다. 전체 옵션은 `--help`로 봅니다.

## 개발·시험 기록

- 계정 연결 시험 13개와 기존 API 시험 13개, 합계 **26개** 통과.
- 기존 Windows API 키 암호화 저장·불러오기 시험 **1개** 통과.
- 기존 프로젝트 **Core 164개·모의 세션 55개** 통과.
- **실제 계정 전달 시험:** Codex(ChatGPT) → Claude Code(Claude 계정) → Codex(ChatGPT), 짧은 문장 3회. API 키 환경변수를 제외한 실행에서 양쪽 답변 수신·전달·최종 파일 생성을 AI가 확인했습니다. 사용자의 확인은 Claude 로그인 완료 응답까지이며 게임의 Unity·Steam 확인과는 관계없습니다.
- Claude Code **2.1.318**, Codex CLI **0.166.0-alpha.6**에서 실행했습니다. 바이너리·계정 정보·시험 대화는 커밋하지 않습니다.
- 계정 모드는 저장소 밖의 고유한 임시 폴더에서 실행합니다. Codex는 읽기 전용, Claude는 추가 도구 없이 답변합니다. 기존 프로젝트의 자동 지시·플러그인·MCP를 새 답변 실행에 불러오지 않습니다. 요청·답변 파일만 도구가 저장하며 게임 코드·프리팹을 모델이 수정하지 않습니다.

```powershell
node --test Tools/AiBridge/cli-bridge.test.mjs Tools/AiBridge/bridge.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AiBridge/launcher.test.ps1
```

공식 근거: [Codex 계정 로그인](https://learn.chatgpt.com/docs/auth), [Codex 비대화형 실행](https://learn.chatgpt.com/docs/non-interactive-mode), [Claude Code 계정 로그인](https://code.claude.com/docs/en/authentication), [Claude Code 명령 실행](https://code.claude.com/docs/en/headless), [Claude Code 설치](https://code.claude.com/docs/en/setup).
