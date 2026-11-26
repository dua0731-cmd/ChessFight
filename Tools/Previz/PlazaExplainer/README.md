# 광장과 꾸미기 설명 영상 (프리비즈)

[기획서](../../../Docs/Plaza/DESIGN.md)를 약 5분짜리 영상으로 만드는 도구다. 그림은 개념 프리비즈이고 실제 그래픽이 아니다.

## 다시 만드는 법

Node, Edge, Krita에 든 ffmpeg가 필요하다. 이 폴더에서:

```powershell
node tts.mjs --rate 2
node record.mjs --from 0 --to 161 --rate 2600k --out out/part1.mp4
node record.mjs --from 161 --to 400 --rate 2600k --out out/part2.mp4
```

두 조각을 ffmpeg concat으로 `out/video.mp4`로 이은 뒤:

```powershell
node audio.mjs --video out/video.mp4 --out out/plaza-explainer.mp4
```

- 내레이션은 이 PC의 한국어 음성(Windows SAPI)이다. 빼려면 `--no-voice`, 음악을 빼려면 `--no-music`.
- 장면을 고친 뒤에는 `node record.mjs --sheets <장면 id>`로 정지 화면 모음을 먼저 본다.

## 파일

| 파일 | 내용 |
|---|---|
| `script.js` | 장면별 내레이션(`say`)과 자막(`cap`), 타임라인 계산 |
| `world.js` | 3D: 기물, 액세서리, 왕국 홀 모형 |
| `scenes.js`, `scenes2.js` | 장면 12개 |
| `ui.js`, `kit.js` | 공용 도우미 |
| `tts.mjs`, `record.mjs`, `audio.mjs`, `wav.mjs` | 음성, 녹화, 소리 합치기 |

대본을 고치면 `tts.mjs`부터 다시 돌린다(줄 길이가 타임라인을 정한다).
