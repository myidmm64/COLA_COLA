# 체력·토큰 상태 단말기

## 외형과 배치

피자리아의 오래된 주문 단말기를 개조한 탁상 3D 소품이다. 청록색 금속 몸체, 크림색 베젤, 나사·통풍구·고무발·짧은 전원선과 드문 도장 벗겨짐으로 기존 압력계와 재질을 맞춘다. 검은 CRT 화면을 좌우로 나누어 왼쪽 BOSS, 오른쪽 PLAYER의 체력바·현재/최대 체력·토큰을 표시한다.

폭 약 0.55m, 높이 약 0.30m이며 피벗은 고무발의 테이블 접지 위치다. 로컬 정면은 **-Z**, 위는 **+Y**다. 화면 조립부를 위로 8° 기울이고, 씬의 수평 회전은 앉은 플레이어 카메라를 향하도록 계산한다. 화면과 문자는 실제 메시이며 외부 폰트·화면 UI Canvas가 필요하지 않다. 녹색 체력, 호박색 토큰, 약한 발광과 얇은 주사선 메시로 저폴리 CRT 느낌을 낸다.

몸체는 URP Lit, CRT 내용은 URP Unlit 재질을 사용한다. 화면의 그림자 투사·수신은 꺼서 작은 글자가 주변 조명에 묻히지 않게 했다. 문자·체력 칸·주사선은 평면 픽셀 메시이며, 저장된 예시 값 기준 메시 인스턴스 56개와 삼각형 3,470개다. 재생성 시 기존 메시를 직접 갱신하고 GPU 버퍼도 갱신하여 에셋 GUID와 화면 표시를 유지한다.

플레이어 화면에서 왼쪽부터 **단말기 → 압력계 → 중앙 콜라** 순서로 배치했다. 저장된 단말기 위치는 `(-1.05, 0.833, 0.45)`, Y 회전은 `330.422°`다. 버튼 콘솔은 `(-0.34, 0.90, -0.10)`, 압력계는 `(-0.3602, 1.1522, -0.0226)`이며 둘의 Y 회전은 약 `345.34°`다. 압력계는 시선 균형 조정에서 배율 `0.82`로 줄이고 받침 접촉 위치를 유지하도록 낮췄다. 단말기·콘솔·카메라·캐릭터·기존 Idle은 보존했다. 정확한 저장값은 `output/scene-layout/MainScene-layout.txt`를 따른다.

## 저장 파일

| 구성 | 경로 |
|---|---|
| 프리팹 | `Assets/_COLA_COLA/Art/Prefabs/StatusTerminal.prefab` |
| 재사용 메시·숫자 0~9 | `Assets/_COLA_COLA/Art/Models/StatusTerminal` |
| 재질 | `Assets/_COLA_COLA/Art/Materials/StatusTerminal` |
| 모델 생성 | `Assets/Editor/COLACOLA/StatusTerminalModelFactory.cs` |
| 기존 씬 부분 적용·검증 | `Assets/Editor/COLACOLA/StatusTerminalSceneTools.cs` |
| 표시 컴포넌트 | `Assets/_COLA_COLA/Scripts/Presentation/StatusTerminalDisplay.cs` |
| 메인 씬 | `Assets/Scenes/MainScene.unity` |
| 씬 내 오브젝트 | `COLA_COLA_MainScene/08_Devices/StatusTerminal` |

`StatusTerminalModelFactory.BuildPrefab()`이 메시·재질·표시 바인딩을 조립해 프리팹을 저장한다. 기존 메시·재질 에셋은 같은 경로에서 갱신하며 숫자와 체력 셀은 공유 메시를 사용한다. 몸체와 화면 요소를 분리해 후속 외형 수정과 값 갱신이 가능하다.

## 표시값과 연결 API

컴포넌트는 `ColaCola.Presentation.StatusTerminalDisplay`다. 저장된 **보스 3/3·12토큰, 플레이어 3/3·15토큰은 배치 확인용 예시**이며 실제 경기 체력·잔액·보상 규칙을 뜻하지 않는다. 일반 Play 시작 시 예시 값이 자동으로 변하지 않는다.

```csharp
terminal.SetBossState(bossHealth, bossMaxHealth, bossTokens);
terminal.SetPlayerState(playerHealth, playerMaxHealth, playerTokens);
terminal.SetActiveParticipant(isPlayerTurn);
```

두 상태 메서드의 네 번째 인자는 `bool animate = true`다. 경기 상태가 확정될 때 호출하며, 초기 로드·리셋·화면 복원은 `false`로 설정한다. Play 중 실제 체력이 감소하면 약 0.42초 피해 점멸, 토큰이 바뀌면 약 0.22초 숫자 점멸을 재생한다. 비활성화하면 남은 연출을 초기화한다. 편집 모드에서도 저장된 값에 맞는 메시를 표시한다.

| 구성 | 표시 기준 |
|---|---|
| 체력바 | 각 패널 10칸. 실제 현재/최대 체력 비율을 올림해 표시: 3/3은 10칸, 2/3은 7칸 |
| 현재·최대 체력 | 각각 2자리, `00`~`99` |
| 토큰 | 4자리, `0000`~`9999` |
| 턴 표시등 | `SetActiveParticipant(true)`면 플레이어, `false`면 보스 |

요청받은 정수는 그대로 보존한다. 표시 범위보다 큰 HP는 `99`, 토큰은 `9999`, 음수는 `0`으로 보이며 실제 값 자체를 잘라 저장하지 않는다. 최대 체력이 0 이하이면 체력바는 빈 상태가 된다. 체력바 비율은 자리수가 부족해도 실제 값을 사용한다. `boss.HasNumericOverflow`와 `player.HasNumericOverflow`로 숫자 표시 범위 초과를 확인할 수 있다.

`BossHealth/BossMaxHealth/BossTokens`, `PlayerHealth/PlayerMaxHealth/PlayerTokens`와 각 `Panel.Health/MaxHealth/Tokens`는 요청받은 실제 값을 반환한다. 게임 규칙에서 값의 유효성을 보장하고, 표시된 자릿수를 잔액 계산의 원본으로 사용하지 않는다.

`boss`와 `player` 패널에는 체력 셀 Renderer 배열, 현재·최대 체력 MeshFilter 각 2개, 토큰 MeshFilter 4개와 선택적인 턴 표시등을 연결한다. `digitMeshes[0..9]`와 네 공유 재질도 바인딩한다. 수동으로 바인딩을 바꾼 뒤에는 `RefreshDisplay()`, 누락 확인에는 `TryValidateBindings(out string message)`를 사용한다. `IsAnimating`은 현재 피드백 재생 여부다. 재생 중 재질 복제나 지연 콜백을 만들지 않는다.

토큰 지급·아이템 구매·보스 AI는 경기 로직에서 처리한 결과를 위 API에 전달한다. 현재 임시 보상인 안전한 강도 1·2·3의 1·2·3토큰, 폭발 시 강도와 관계없이 총 5토큰도 표시 컴포넌트 외부에서 계산한다.

## 적용과 검증

기존 메인 씬에는 **`COLA_COLA > Build and Place Status Terminal`** 메뉴 또는 `ColaCola.Editor.StatusTerminalSceneTools.BuildAndPlace`를 사용한다. 저장된 씬에 단말기를 추가하고 압력계·콘솔 배치를 부분 수정한다. 방·카메라·캐릭터를 재생성하지 않으며 다른 오브젝트의 위치·회전을 검사한다. 전체 씬을 새로 만드는 `MainSceneTools.Build`에도 `ApplyToScene` 연결이 포함되어 있다.

**`COLA_COLA > Verify Status Terminal`**은 저장본을 다시 열어 실제 Play에서 값 변경·피해 피드백·숫자 범위·비활성화 초기화·기존 Idle 재생을 확인한다. 검증용 변경값은 Play 종료 후 저장된 예시 값으로 돌아간다. 실제 실행 결과와 최종 렌더 수치는 다음 출력에서 확인한다.

| 출력 | 경로 |
|---|---|
| 적용 전 씬 | `output/status-terminal/MainScene-before-status-terminal.unity` |
| 메인 카메라 미리보기 | `output/status-terminal/MainScene-StatusTerminal.png` |
| 단말기 근접 이미지 | `output/status-terminal/StatusTerminal-closeup.png` |
| 실제 값 변경 예시 | `output/status-terminal/MainScene-ReadoutChange.png` |
| 저장본·배치·바인딩 검사 | `output/status-terminal/StatusTerminal-validation.txt` |
| 다른 오브젝트 배치 보존 | `output/status-terminal/Placement-preservation.txt` |
| 실제 Play 검사 | `output/status-terminal/Display-playmode-validation.txt` |

공용 씬 미리보기 `output/scene-layout/MainScene-preview.png`도 적용 결과로 갱신된다.

최초 적용 검증에서 무관한 월드 Transform 235개와 설정·패키지·Volume·Idle 관련 파일 21개를 보존했다. 실제 Play에서 저장된 예시 값 유지, 양쪽 상태 갱신, 부분/빈 체력바, 음수와 숫자 범위 초과, 피드백 종료, 비활성화 초기화와 기존 Idle 지속을 확인했다. 파일 해시 비교는 `output/status-terminal/preserved-files-before.json` 및 `preserved-files-after.json`에 있다. 현재 메시·삼각형 수와 최신 검증 시간은 위 보고서를 기준으로 한다.

최종 모델 재생성 직후 같은 Unity 6000.6.4f1 프로세스에서 정상 렌더와 3.00초 실제 Play 검증을 통과했다. 이후 새 프로세스에서도 저장본을 다시 열어 Renderer 137개의 재질·셰이더 참조, 누락 스크립트 0개, 카메라·AudioListener 각 1개와 기존 반복 Idle을 확인했다. 검증용 상태 변경은 저장하지 않는다.
