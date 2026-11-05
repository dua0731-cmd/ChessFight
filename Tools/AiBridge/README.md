# GPT ↔ Claude API 연결

하나의 요청을 GPT와 클로드가 번갈아 검토하는 로컬 도구입니다. 기본값은 **GPT 초안 → Claude 검토 → GPT 최종 답변**, 총 API 호출 3회입니다. Claude부터 시작하거나 답변 횟수를 2~8회로 바꿀 수 있습니다.

## 처음 실행

1. [OpenAI API 키](https://platform.openai.com/api-keys)와 [Anthropic API 키](https://platform.claude.com/settings/keys)를 발급합니다. 각 계정에 API 사용 권한과 잔액이 필요합니다.
2. 이 폴더의 **`Setup.cmd`를 더블클릭**합니다.
3. 나타난 창에 각 키를 입력합니다. 입력은 가려지며, 현재 Windows 계정으로 암호화해 `.local/credentials.xml`에 저장합니다. 키를 채팅이나 코드에 붙여 넣을 필요가 없습니다.
4. 함께 다룰 내용, 먼저 답할 모델, 총 답변 횟수를 입력합니다.
5. 다음부터는 **`Start.cmd`를 더블클릭**합니다. 키를 바꾸려면 `Setup.cmd`를 다시 실행합니다.

Node.js 22 이상이 필요합니다. 이 PC에서는 Codex에 포함된 Node도 찾아 사용합니다. 별도 라이브러리 설치는 없습니다.

한 번 실행할 때는 그 창에 입력한 요청과 이번 실행의 모델 답변만 두 API에 전송합니다. 현재 ChatGPT·Claude 앱의 채팅, 이 저장소의 문서·코드를 자동으로 읽지 않습니다. 기존 대화를 합치거나 모델에게 파일 편집 권한을 주는 도구는 아닙니다.

## 결과와 사용 한도

- 답변마다 화면에 표시하고 `runs/날짜.json`에 저장합니다. 실제 API에서 보고한 입력·출력 토큰 수도 기록합니다.
- 기본 답변 횟수는 3회, 답변당 출력 한도는 4,096토큰, 각 호출의 대기 한도는 180초입니다. 이전 답변도 다음 호출의 입력에 포함됩니다. 호출마다 해당 제공자의 API 사용량에 따라 과금됩니다.
- `Ctrl+C`로 중단할 수 있습니다. 이미 보낸 요청의 과금 취소를 보장하지 않습니다.
- 오류가 나면 자동 재시도하지 않습니다. 앞서 받은 답변과 오류 상태를 기록하고 멈춥니다. 토큰 한도로 잘린 답변도 기록하되 다음 모델에 넘기지 않습니다.
- `.local/`과 `runs/`는 Git에서 제외합니다. 키 파일은 이 Windows 사용자용입니다. 다른 PC에 복사해 사용하는 대신 그 PC에서 다시 설정합니다.
- 키를 지우려면 `.local/credentials.xml`을 삭제합니다. 환경변수로 지정한 키는 별도로 관리합니다.

## 명령으로 사용

환경변수 `OPENAI_API_KEY`와 `ANTHROPIC_API_KEY`를 사용하는 실행도 지원합니다. 명령줄 인수로 키를 받지 않습니다.

```powershell
node Tools/AiBridge/bridge.mjs --prompt "폰 러쉬 미니게임 아이디어를 검토해줘"
node Tools/AiBridge/bridge.mjs --prompt "이 기획의 빠진 점을 찾아줘" --first claude --turns 4
node Tools/AiBridge/bridge.mjs --check
node --test Tools/AiBridge/bridge.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AiBridge/launcher.test.ps1
```

암호화한 Windows 키까지 읽어 설정 유무를 확인하려면 `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/AiBridge/Start.ps1 -Check`를 사용합니다. `--check`와 `-Check`는 API에 접속하지 않으며 키의 유효성이나 잔액을 확인하지 않습니다.

기본 모델은 `gpt-6.1-sol`, `claude-sonnet-5-5`입니다. 계정에서 사용할 수 있는 모델이 다르면 `OPENAI_MODEL`·`ANTHROPIC_MODEL` 환경변수 또는 `--gpt-model`·`--claude-model`로 지정합니다. 전체 옵션은 `--help`로 봅니다.

## 연결 상태와 근거

2026-11-05: 모의 API 시험 **13개**, 가짜 키를 사용한 Windows 암호화 저장·불러오기 시험 **1개**, 기존 프로젝트의 **Core 164개·모의 세션 55개**가 통과했습니다. 두 API 키가 이 PC의 프로세스·사용자·시스템 환경변수에 없어 **실제 API 연결은 아직 시험하지 않았습니다**. 키를 설정한 뒤 짧은 요청으로 두 서비스의 실제 응답을 확인해야 합니다. Unity·Steam 확인 대상은 없습니다.

공식 문서: [OpenAI 텍스트 생성](https://developers.openai.com/api/docs/guides/text), [GPT-6.1 Sol](https://developers.openai.com/api/docs/models/gpt-6.1-sol), [Claude Messages API](https://platform.claude.com/docs/en/api/messages/create), [Claude 인증](https://platform.claude.com/docs/en/manage-claude/authentication), [Claude 모델 목록](https://platform.claude.com/docs/en/models/overview).
