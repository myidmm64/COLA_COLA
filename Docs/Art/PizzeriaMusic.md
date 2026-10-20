# Bleak Terminal — 현재 게임 BGM

2026-10-20 사용자 선택에 따라 **Ruskerdax의 Bleak Terminal 한 곡만** 재생한다. 다른 음악 레이어나 후보 곡을 섞지 않는다. 기존 효과음과 피자리아 환경음은 유지한다.

- 게임 에셋: `Assets/_COLA_COLA/Resources/Audio/BleakTerminal.mp3`. 사용자 요청으로 보관한 후보 원본을 그대로 복사했다. 속도·피치·편집 없음.
- [원작·공식 배포](https://opengameart.org/content/bleak-terminal) / 작곡가 Ruskerdax / [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). 상업적 사용과 편집이 허용되며 표기는 선택 사항이다.
- `ColaDuelMusic`가 단일 2D 반복 AudioSource를 사용한다. MP3는 스트리밍으로 가져오도록 설정했다. 새 콜라·재시작 때 음악을 처음부터 다시 틀지 않는다.
- 기본 `musicVolume=0.50`, 출력은 `musicVolume × 0.84 × Duck`. 압력에 따라 추가 음악이나 피치를 얹지 않는다.
- 이전 Last Order 두 WAV와 메타는 삭제했다. `Tools/Audio/compose_pizzeria_bgm.py`는 환경음 제작과 SFX 공용 함수 의존성이 있어 남겼다. 실행하면 이전 곡을 다시 만들 수 있지만 현재 게임은 그 파일을 읽지 않는다.

결과 대기에는 안전/폭발에 동일하게 음악을 58%로 낮춘다. 폭발 직전 50%, 폭발·복귀 중 10%, 암전 중 18%를 적용하고 페이드인에 맞춰 회복한다. 결과가 결정되기 전에 실제 폭발 여부로 음악을 다르게 재생하지 않는다.

환경음은 `ColaPizzeriaAmbience`가 대전 시작 시 자동 연결한다. 음악과 다른 AudioSource를 사용하며 별도의 `ambienceVolume`(기본 0.28)로 조절한다.

- `Pizzeria_Fridge_Room.wav`: 12초 모노 루프. 60Hz 모터·배음과 낮은 공기 질감. 기존 음료 기계 쪽에서 넓고 낮게 들리도록 배치한다.
- `Pizzeria_Vent_Air.wav`: 10초 모노 루프. 환풍기 바람과 약한 기계 진동. 기존 오븐 쪽에 공간음을 배치한다.
- `Pizzeria_Metal_Settle.wav`: 약 0.62초의 조용한 금속 수축/접촉음.
- `Pizzeria_Pipe_Drip.wav`: 약 0.45초의 짧은 배관 물방울 소리.

간헐음은 첫 6~10초 뒤, 이후 12~23초 간격으로 재생한다. 금속과 물방울의 선택·간격은 게임 난수와 독립적이며 플레이어 행동이나 폭발 결과를 암시하지 않는다. 폭발·암전에서 환경음도 낮추고 그 구간에는 새 간헐음을 시작하지 않는다. 재시작 시 소스를 중복 생성하거나 루프를 매번 처음부터 틀지 않는다. 모든 환경음은 22,050Hz·모노·16bit이며 실제 게임용 에셋이다.


이번 교체의 **Unity Play·청취·컴파일 검사는 실행하지 않았다.** 기존 음악 보고서는 이전 곡의 결과다. 요청 시 쓰는 음악 Play 검사 소스는 단일 스트리밍 MP3의 에셋·재생·루프·더킹 기준으로 수정했으나 실행하지 않았다. 압축 MP3를 이전 WAV와 PCM 비교하거나 루프 경계가 검증됐다고 주장하지 않는다.

## 외부 무료 후보

최초 6곡은 `References/BGM_Candidates/`에 보관한다. 추가 7곡은 `04_Faster_Industrial/`에 모았다. 모두 Kevin MacLeod의 무료 CC BY 4.0 곡으로, 게임에 채택하면 해당 곡의 크레딧을 표시한다. 새 후보는 공식 표기 135~165 BPM이며 Chase Pulse Faster·Club Diver·Harmful or Fatal·Ghostpocalypse - 7 Master·Nightmare Machine·Eternal Terminal·Burnt Spirit이다. 전자·산업·호러 질감은 제작자 설명·악기·태그 기준으로 선정했다. Bleak Terminal의 공식 BPM은 미표기이므로 속도 차이를 직접 측정했다고 표현하지 않는다.

각 폴더 README에 목록, LICENSES.md에 출처·크레딧, manifest.json에 원본 경로·용량·해시를 남겼다. 추가 후보는 게임에 연결하지 않는다. 재다운로드 소스는 `Tools/Audio/download_faster_bgm.py`다.
