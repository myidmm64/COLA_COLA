# 버튼 콘솔과 캐릭터 Idle

## 적용 범위

MainScene의 압력계와 1·2·3 버튼 콘솔을 플레이어가 앉은 방향으로 돌리고, 플레이어 팔과 보스에 테이블을 지지하는 착석 Idle을 적용한다. 이후 실물 상태 단말기를 추가하면서 압력계·콘솔을 조립 상태로 재배치했다. 현재 화면 왼쪽부터 **단말기 → 압력계 → 중앙 콜라** 순서이며 카메라·캐릭터 배치와 기존 Idle은 유지한다.

1·2·3 버튼을 직접 클릭해 강도를 선택하면 해당 키를 12mm 누른 상태로 유지하고 예상 압력·폭발 확률만 바꾼다. 중앙 콜라를 클릭해야 플레이어 흔들기와 폭발 판정이 한 번 진행되고, 이후 보스의 자동 응답으로 이어진다. 강도 미선택과 양쪽 연출 중 추가 클릭은 거절한다. 입력 구현과 검증은 `Docs/Gameplay/PressureSelection.md`, `output/pressure-interaction`을 참고한다. 손으로 키를 누르는 행동·피해·토큰 연결은 후속 범위이며 기존 Idle은 유지한다.

플레이어·보스의 콜라 잡기·들기·흔들기·내려놓기는 강도별 4.4초 클립으로 추가했다. 플레이어는 원래 FPS 팔 모델을 사용하며 추가 소매 메시를 제거했다. 플레이어 클릭 다음 차례에 보스 원본 손이 자동으로 흔든다. 동작 전후 기존 Idle로 부드럽게 전환하며 자세한 구조·검증은 `Docs/Art/ColaShakeAnimations.md`를 따른다.

## 콘솔 에셋

| 구성 | 경로 또는 값 |
|---|---|
| 프리팹 | `Assets/_COLA_COLA/Art/Prefabs/ShakeConsole.prefab` |
| 메시 | `Assets/_COLA_COLA/Art/Models/ShakeConsole` |
| 재질 | `Assets/_COLA_COLA/Art/Materials/ShakeConsole` |
| 생성 소스 | `Assets/Editor/COLACOLA/ShakeConsoleModelFactory.cs` |
| 메인 씬 이름·그룹 | `ShakeConsole`, `08_Devices` |
| 메인 씬 위치 | `(-0.34, 0.90, -0.10)`, Y 회전 약 `345.34°`, 배율 `(1, 1, 1)` |
| 메시·삼각형·재질 | 18 MeshFilter, 1,808 triangles, 재질 7개 |

청록색 경사 몸체에 금속 테두리·후면 압력계 받침·나사·고무발을 조립한다. 상판에는 금속 소켓을 두고, 크림색 1·2 키와 붉은색 3 키를 배치한다. 각진 모서리와 돌출된 픽셀 숫자로 실물 저폴리 느낌을 유지한다. 숫자는 외부 폰트나 텍스처를 요구하지 않는 메시다. `현재 압력`, `흔들기 강도` 제목은 추가하지 않는다.

`Assets/Editor/COLACOLA/PlayerFacingDeviceLayout.cs`가 장치의 로컬 정면 **-Z**를 플레이어 카메라 방향으로 맞춘다. 콘솔의 중심을 회전 피벗으로 사용하고 압력계의 상대 위치도 같은 각도로 돌린다. 기존 상판 경사·장착 높이·압력계와 콘솔 사이 간격을 유지한다. 이미 정렬된 장치에 다시 실행해도 회전이 누적되지 않는다.

현재 압력계 위치는 `(-0.3602, 1.1522, -0.0226)`, 배율은 `0.82`다. 중앙 콜라와의 시선 균형을 위해 압력계만 축소하고 받침 접촉 위치를 유지하도록 낮췄으며, 콘솔과 키의 크기·위치는 유지했다. 정확한 저장 좌표·회전은 `output/scene-layout/MainScene-layout.txt`, 압력계 조정은 `Docs/Art/PressureGauge.md`를 따른다.

### 버튼 피벗과 눌림

프리팹 루트 바로 아래 `ShakeButton1`, `ShakeButton2`, `ShakeButton3`가 있다. 키캡과 돌출 숫자는 각각 해당 버튼의 자식이므로 버튼 Transform을 움직이면 함께 이동한다. 버튼은 상판의 경사에 맞춰 기울어져 있다.

각 버튼의 초기 `localPosition`을 저장하고, 눌릴 때 그 값에서 **Y를 0.012m 낮추면** 된다. 이는 부모인 콘솔의 로컬 Y 방향이며, 버튼 자체가 기울어진 축을 기준으로 `Translate`하는 것과 구분한다. 손을 떼면 저장한 초기 위치로 되돌린다. 상호작용을 연결할 때는 매 프레임 현재 위치에서 값을 계속 빼지 말고 초기 위치를 기준으로 계산한다.

콘솔 루트에는 몸체용 BoxCollider가 있고 각 버튼에는 키캡용 BoxCollider가 있다. 키의 Collider를 통해 선택 대상을 구분할 수 있다. 자동 누름이나 입력 처리 스크립트는 이 모델에 포함하지 않는다.

기존 `PLACEHOLDER_ShakeConsole`과 `PLACEHOLDER_ShakeButton1/2/3`를 제거하고 프리팹으로 교체한다. `05_CubePlaceholders`에는 밸브·냉각팩·플레이어 쿠폰·보스 쿠폰의 4개가 남는다.

## Idle 에셋과 재생

| 대상 | 클립 | Controller | 길이 |
|---|---|---|---|
| 보스 | `Assets/_COLA_COLA/Animations/Idle/Boss_SeatedIdle.anim` | 같은 폴더의 `Boss_SeatedIdle.controller` | 30초 반복 |
| 플레이어 팔 | `Assets/_COLA_COLA/Animations/Idle/PlayerArms_TableIdle.anim` | 같은 폴더의 `PlayerArms_TableIdle.controller` | 30초 반복 |

생성 코드는 `Assets/Editor/COLACOLA/CharacterIdleFactory.cs`다. 보스의 `Boss_ExistingRig_SeatedBlockPose`, 팔의 `PlayerArms_ExistingRig_TablePose` 루트에 활성 Animator를 두고 해당 Controller를 연결한다. 각 Controller의 기본 상태는 `Idle`이며, MainScene에서 **Play를 누르면 자동 재생**한다. 별도 입력이나 상태 전환 조건은 필요하지 않다.

두 리그는 기존 Generic 본 계층을 사용하며 원본 FBX와 원본 프리팹은 수정하지 않는다. 먼저 손바닥 방향과 손가락의 이완을 조정하고 실제 스킨 메시의 아래 면을 기준으로 테이블 높이에 손을 맞춘다. 보스는 양손 위치가 약간 다른 비대칭 자세로 앉는다. 캐릭터 루트의 배치 Transform은 애니메이션하지 않는다.

30초 안에 길이와 깊이가 서로 다른 호흡 6번을 배치한다. 각 호흡은 들숨 36%, 날숨 55%, 짧은 멈춤 9%로 나누고 부드럽게 이어진다. 보스는 호흡과 함께 간헐적으로 고개를 숙이거나 옆을 바라보고 몸의 무게를 조금 옮긴다. 플레이어는 호흡에 따라 위팔이 미세하게 움직이며, 정해진 구간에만 손가락을 가볍게 편다. 손목과 손가락을 계속 흔드는 움직임은 제거했다.

제작 중에는 팔의 IK 계산으로 손목의 지지 위치와 손바닥 방향을 유지한다. 이 결과를 12Hz 간격의 **361개 포즈**로 샘플링하여 각 본의 위치·회전·배율 곡선에 굽고, 보간되는 30fps Unity AnimationClip으로 저장한다. 재생 시에는 일반 Animator만 사용하므로 런타임 IK 스크립트나 추가 패키지가 필요하지 않다. 반복 시작·끝 값과 곡선의 기울기를 맞춰 30초 경계가 이어지도록 작성한다.

기준 자세는 같은 폴더의 `Boss_SeatedIdle_SupportedReference.json`, `PlayerArms_TableIdle_SupportedReference.json`에 저장한다. 재생성할 때 이 파일의 본 자세를 먼저 복원하므로 손가락 굽힘이 누적되지 않는다. 보스 기준 자세의 revision 2에는 엄지를 편안하게 내려놓도록 수정한 자세가 포함된다. 본 계층이나 기준 착석 자세를 의도적으로 바꿀 때는 해당 기준 파일도 새 자세에 맞춰 다시 작성해야 한다. 기존 `.anim`과 `.controller`의 경로와 GUID는 유지한다.

Animator는 Root Motion을 끄고 `Always Animate`로 설정한다. Idle 상태의 `Write Defaults`는 끈다. 이 클립은 현재 테이블 자세와 해당 리그의 본 경로를 기준으로 제작했으므로, 캐릭터의 본 이름·계층이나 기본 자세를 바꾸면 새 기준으로 재생성하거나 클립을 수정해야 한다.

## 적용 도구와 확인

현재 배치의 장치 방향과 Idle을 수정하는 도구는 `Assets/Editor/COLACOLA/ConsoleAndIdleSceneTools.cs`의 `ReviseNaturalIdle`다. 메뉴는 `COLA_COLA > Revise Player Facing Devices and Natural Idle`이며, 저장된 MainScene을 열어 부분 수정·저장한 뒤 Play 모드로 들어가 자동 재생을 검증한다. 적용 전 씬 백업은 `output/scene-layout/MainScene-before-natural-idle.unity`다.

기본 적용 도구 `Apply`와 메뉴 `COLA_COLA > Apply Console and Character Idles`, 전체 씬을 재생성하는 `MainSceneTools.Build`에도 플레이어 방향 정렬과 새 Idle 적용이 포함된다. 최초 콘솔 교체 전 백업은 `output/scene-layout/MainScene-before-console-idle.unity`에 있다. 이미 수동으로 배치를 조정한 씬에서는 전체 재생성 대신 해당 부분의 편집·적용 도구를 사용한다.

현재의 **단말기 포함 배치만** 적용할 때는 `COLA_COLA > Build and Place Status Terminal` 또는 `StatusTerminalSceneTools.BuildAndPlace`를 사용한다. 콘솔·압력계를 함께 옮기고 기존 카메라·캐릭터·Idle을 유지한다. 단말기 표시 API와 실제 Play 검증은 `Docs/Art/StatusTerminal.md` 및 `output/status-terminal`을 참고한다.

| 출력 | 경로 |
|---|---|
| 콘솔 구조·기본 포즈·클립 곡선 검사 | `output/scene-layout/ConsoleIdle-assets-validation.txt` |
| 장치 방향·조립 간격 검사 | `output/scene-layout/PlayerFacingDevices-validation.txt` |
| 접지된 손·새 포즈 제작 검사 | `output/scene-layout/NaturalIdle-authoring-validation.txt` |
| 실제 Play 모드 자동 재생 검사 | `output/scene-layout/Idle-playmode-validation.txt` |
| 저장본 씬 검사 | `output/scene-layout/MainScene-validation.txt` |
| 압력계와 콘솔 근접 렌더 | `output/scene-layout/ShakeConsole-closeup.png` |
| 메인 카메라 렌더 | `output/scene-layout/MainScene-preview.png` |
| Idle 재생 중 렌더 | `output/scene-layout/MainScene-idle-playing.png` |
| Idle 프레임 이미지 | `output/scene-layout/idle-frames` |
| 새 Idle의 실제 Play 프레임으로 만든 GIF | `output/scene-layout/MainScene-natural-idle-preview.gif` |
| 같은 미리보기의 기존 경로 | `output/scene-layout/MainScene-idle-preview.gif` |

위 Idle 전용 이미지·검증은 제작 당시 기록이다. 현재 장치 배치와 단말기 추가 후 전체 화면은 `output/status-terminal/MainScene-StatusTerminal.png`를 따른다. 정지 화면만으로는 애니메이션 재생을 확인할 수 없으므로 Play 상태의 Animator와 움직임 검증 기록도 함께 확인한다.

## 검증 기록

자연스러운 Idle 제작 단계에서 Unity 6000.6.4f1의 실제 Play 모드를 31.01초 동안 확인했다. 두 Controller는 별도 `Animator.Play` 호출 전에 기본 `Idle` 상태에 자동 진입했고, 각각 normalizedTime 1.024까지 진행해 30초 반복 경계를 통과했다. 시작 자세의 T 포즈 초기화와 캐릭터 배치 루트 이동은 없었다. 관측한 최대 관절 회전은 보스 3.597°, 팔 2.801°였다. 실제 재생 프레임 150장으로 30.00초 길이의 `MainScene-natural-idle-preview.gif`를 만들었으며 기존 미리보기 경로도 같은 파일로 갱신했다.

제작 시 361개 포즈의 손 지지 위치 오차는 보스 0.0004mm, 플레이어 0.0003mm였으며, 실제 재생 중 손목 지지 위치 오차는 0.0005mm 이하였다. 측정한 손 메시의 최저 높이는 월드 Y 0.83300m로 테이블 Y 0.83000m보다 3mm 높았다. 높이는 `bone.localToWorldMatrix × bindpose`로 월드 공간 CPU 스키닝을 계산해 확인한다. 원본 FBX의 100배 렌더러 배율이 이중 적용되지 않도록 이 검사에는 `BakeMesh`를 사용하지 않는다. 이 계산은 제작·검증 도구에만 있고 재생 시 IK 의존성은 없다.

당시 장치 Y 회전은 -31.44872°이며 정렬을 다시 실행했을 때 회전 변화는 0°였다. 압력계와 콘솔의 정면·카메라 방향 내적은 모두 1.0이고 조립 간격·높이를 유지했다. 당시 버튼 앞 인접 아이템의 윗면은 키 상단보다 약 4.8~6.8cm 낮았으며 접근 경로 경고는 0개였다. 전체 수정에서 장치·캐릭터 외 무대 로컬 Transform 94개를 보존했다. 이 위치·회전·접근 검사 수치는 단말기 추가 전 기록이다.

저장본 재열기 검사에서 Renderer 77개의 재질·셰이더 참조, 카메라·AudioListener 각 1개, 누락 스크립트 0개, 아이템 Cube 4개를 확인했다. `SampleScene.unity`, `URPProjectSettings.asset`, `QualitySettings.asset`, `EditorSettings.asset`의 파일 해시도 작업 전후 동일했다. 상세 결과는 위 검증 기록과 `output/scene-layout/preserved-files-natural-idle.json`에 있다.

단말기 최초 적용에서는 압력계·콘솔 배치 외 월드 Transform 235개와 설정·패키지·Volume·Idle 관련 파일 21개를 보존했다. 실제 Play에서도 두 캐릭터의 기존 Idle이 계속 재생됨을 확인했다. 최신 검증은 `output/status-terminal/Placement-preservation.txt`, `preserved-files-after.json`, `Display-playmode-validation.txt`를 따른다.
