# COLA_COLA 메인 씬 배치

## 씬과 기준 이미지

- 씬: `Assets/Scenes/MainScene.unity`
- 확정 컨셉: `output/concept-art/pizzeria-boss-v4.png`
- 실제 Unity 카메라 렌더: `output/scene-layout/MainScene-preview.png`
- 압력계 축소·중앙 콜라 시선 조정: `output/gauge-focus/MainScene-Focus.png`
- 플레이어·보스 콜라 흔들기: `Docs/Art/ColaShakeAnimations.md`, `output/shake-animation`
- 상태 단말기 적용 렌더: `output/status-terminal/MainScene-StatusTerminal.png`
- 상태 단말기 모델·표시 API: `Docs/Art/StatusTerminal.md`
- 압력 미리보기·콜라 클릭 확정: `Docs/Gameplay/PressureSelection.md`
- 강도 3 선택 화면: `output/pressure-interaction/Preview-Strength3.png`
- 양쪽 테스트 아이템 6개씩 배치한 렌더: `output/item-layout/MainScene-Items.png`
- 테스트 아이템 모델·공간 확인: `Docs/Art/TestItemLayout.md`
- 호러 조명 적용 후 렌더: `output/horror-atmosphere/MainScene-Horror.png`
- Cinemachine·호러 조명·후처리 구성: `Docs/Art/HorrorAtmosphere.md`
- 누락 모델과 Cube 대체 목록: `Docs/Art/MainScene-MissingModels.md`
- 실제 배치값: `output/verification/latest/scene-layout/MainScene-layout.txt`
- 엔진 재열기 검증 기록: `output/verification/latest/scene-layout/MainScene-validation.txt`

이 씬은 보스와의 1대1 경기, 실제 체력·토큰을 표시하는 3D 단말기, 강도 선택·압력/폭발 확률 미리보기·중앙 콜라 클릭 확정을 포함한다. 양쪽 체력 3·토큰 0으로 시작하며 승패·재시작·다음 난이도를 연결했다. 아이템 그룹과 해당 보조광은 숨겼고 폭발 연출·상점은 제외한다. 현재 규칙은 `Docs/Gameplay/PlayableDuel.md`를 따른다.

## 열어서 확인하기

Unity의 `COLA_COLA > Open Main Scene` 메뉴로 열 수 있다. Game 뷰는 **16:9**로 확인한다. Play를 누르면 경기가 시작되며 자동 손 미리보기는 꺼져 있다. 1·2·3 버튼 또는 숫자키로 선택하고 콜라 클릭 또는 Space로 확정한다. 빌드 설정의 첫 번째 활성 씬은 MainScene이며 기존 SampleScene도 보존한다.

플레이어는 강도 1·2·3 선택 후 콜라를 클릭하면 기존 FPS 모델의 오른손으로 잡아 흔들고 내려놓는다. 이후 0.65초 뒤 보스가 자기 손으로 자동 응답하며, 완료 후 다시 플레이어가 선택한다. 보스 강도는 임시 무작위 1~3이며 압력 판정은 각자 한 번 수행한다. 동작만 볼 때는 Play 모드의 `COLA_COLA > Preview Boss Cola Shake (Play Mode)`를 사용할 수 있다. 이 별도 미리보기는 압력·턴 규칙을 진행하지 않는다. 구조는 `Docs/Art/ColaShakeAnimations.md`를 따른다.

카메라는 테이블 앞에 앉은 1인칭 구도다. 위치 `(0, 1.32, -1.40)`, 회전 `(11, 0, 0)`, 수직 FOV `50`, Near Clip `0.035`를 사용한다. Cinemachine `CM_SeatedDuel`과 Main Camera의 Brain이 이 구도를 유지한다. Main Camera에 HDR와 URP 후처리를 켜고, 씬 전용 Global Volume으로 화면 효과를 적용한다.

## 계층

| 그룹 | 구성 |
|---|---|
| `01_Pizzeria` | 벽·바닥·천장 배치용 Cube, 메뉴 텍스처 보드, 기존 카운터·오븐·음료 기계·피자 박스·옆 테이블·벤치·천장등 |
| `02_DuelTable` | 기존 식탁·보스 벤치·중앙 콜라 |
| `03_Characters` | 기존 PSX 보스와 1인칭 팔, 테이블 자세를 유지하는 Idle Animator |
| `04_TabletopItems` | `PlayerInventory`·`BossInventory`, 각각 테스트 소품 6개와 실물 서빙 쟁반 |
| `05_CubePlaceholders` | 아이템 Cube를 모델로 교체해 현재 비어 있음 |
| `06_Lighting` | 일정한 테이블 키 라이트, 얼굴·콜라 라벨 보조광, 보스의 붉은 역광, 압력계·테스트 아이템 보조광, 녹색 주방·음료 기계 조명, 약하게 밝기가 변하는 형광등, 붉은 벽등 주변광 등 조명 11개 |
| `07_Cameras` | Main Camera·AudioListener·CinemachineBrain, 고정 구도의 `CM_SeatedDuel` CinemachineCamera |
| `08_Devices` | 실물 체력/토큰 단말기, 압력계, 1·2·3 버튼 콘솔, 압력 선택과 캐릭터·병 흔들기 재생 컴포넌트 |
| `09_Atmosphere` | 씬 전용 Global Volume, 녹색 주방 스트립과 붉은 벽 비상등 메시 |

아이템은 테이블 위 월드 오브젝트다. 좌측 장치에 `현재 압력`, `흔들기 강도` 문구를 넣지 않았다. 상태 표시는 청록색 몸체·크림색 베젤·검은 CRT를 가진 실물 단말기로 만들고, 화면 왼쪽 BOSS·오른쪽 PLAYER에 녹색 분절 체력바와 호박색 토큰 숫자를 표시한다.

플레이어 시점에서 **단말기 → 압력계 → 중앙 콜라** 순서다. 압력계·콘솔은 조립 간격·높이·상판 경사를 유지한 채 함께 이동하고 플레이어 방향으로 다시 정렬했다. 카메라·캐릭터·기존 Idle은 보존했다. 오른쪽 앞·뒤의 실물 쟁반에 플레이어·보스 소품을 6개씩 배치했다. 기존 밸브·냉각팩·쿠폰 Cube와 보스 캔·집게는 이 공간 확인용 모델로 교체했다.

| 오브젝트 | 현재 월드 위치 | Y 회전 |
|---|---|---|
| `StatusTerminal` | `(-1.05, 0.833, 0.45)` | `330.422°` |
| `PressureGauge` | `(-0.3602, 1.1522, -0.0226)`, 배율 `0.82` | 약 `345.34°` |
| `ShakeConsole` | `(-0.34, 0.90, -0.10)` | 약 `345.34°` |
| `HeroCola_ExistingModel` | `(0, 0.835, -0.07)` | `180°` |

실제 저장값은 `output/verification/latest/scene-layout/MainScene-layout.txt`, 카메라에서 본 좌우 순서와 단말기 방향은 `output/verification/latest/status-terminal/StatusTerminal-validation.txt`를 기준으로 한다.

압력계는 중앙 콜라에 시선을 모으기 위해 18% 축소하고, 다이얼·금속 테두리의 밝기와 반사를 낮췄다. 장착 받침 위치를 유지하며 약 3.8cm 낮췄고 버튼 콘솔은 유지한다. 압력계 보조광은 줄이고 콜라 라벨 보조광은 보강했다. 적용 메뉴는 `COLA_COLA > Balance Gauge and Cola Focus`, 세부 값은 `Docs/Art/PressureGauge.md`를 따른다.

## 기존 에셋을 사용하는 방식

- 식탁은 `Pizzeria_Props_fbx/Chair_Table`, 벤치는 `Chair_Table.001`을 사용한다.
- 카운터·오븐·음료 기계는 Pizzeria Props의 자식 모델을 분리 복제한다. 피자 박스와 천장등은 Pizzeria Scene에서 가져온다. 이전 배치의 캔·집게는 테스트 아이템 6종을 양쪽에 놓으면서 교체했다.
- FBX의 축·단위 변환을 보존한 뒤 경계 크기로 정규화한다. 각 소품의 최상위 Transform은 배치 위치를 가리키므로 Inspector에서 옮기기 쉽다.
- 원본 건물은 벽·실내 바닥·천장이 결합된 메시라 이번 실내는 Cube 구조블록과 기존 피자리아 텍스처로 구성한다. 메뉴도 기존 `Menu.png`를 입힌 얇은 구조블록이다.
- 보스와 팔은 기존 Generic 리그를 사용한다. 양손을 테이블에 지지하는 자세로 다시 잡고, 각각 30초의 Idle 클립과 Animator Controller를 적용한다. 길이와 깊이가 다른 호흡 6번, 보스의 간헐적인 고개·시선·체중 이동, 플레이어의 드문 손가락 이완을 배치한다. 제작 중 팔 IK로 손의 위치와 방향을 유지한 결과를 일반 AnimationClip으로 굽기 때문에 런타임 IK 의존성은 없다. 원본 FBX는 수정하지 않는다.
- 캐릭터와 콜라의 재질 보정은 `Assets/_COLA_COLA/Art/Materials/Blockout`의 복사본에 적용했다. 임포트한 원본 재질은 보존한다.

## 재생성과 검증

에디터 전용 도구는 `Assets/Editor/COLACOLA/MainSceneTools.cs`에 있다.

- `Build`: 컨셉 기준 씬을 재생성한다. 기존 MainScene이 있으면 `output/scene-layout/MainScene-before-rebuild.unity`에 백업하고 생성본으로 저장한다. 수동 배치 수정 후에는 씬 재생성 대신 일반 편집을 사용한다.
- `Verify`: 저장된 씬을 다시 열어 카메라·AudioListener·누락 스크립트·재질 참조·양쪽 아이템 6개씩의 배치·시작 씬·중앙 콜라 구도를 확인하고 실제 렌더를 출력한다.
- `OpenMainScene`: 일반 편집기에서 메인 씬과 Game 뷰를 연다.

`Build`에는 압력계·콘솔 생성, 플레이어 방향 정렬과 새 Idle 적용도 포함한다. 현재 배치의 장치 방향과 Idle만 수정할 때는 `COLA_COLA > Revise Player Facing Devices and Natural Idle` 메뉴 또는 `Assets/Editor/COLACOLA/ConsoleAndIdleSceneTools.cs`의 `ReviseNaturalIdle`을 사용한다. 적용 전 백업은 `output/scene-layout/MainScene-before-natural-idle.unity`에 저장한다. 방향 정렬 코드는 `PlayerFacingDeviceLayout.cs`다. 에셋·버튼 피벗·애니메이션과 기준 자세 JSON의 사용법은 `Docs/Art/ShakeConsole-And-Idle.md`를 참고한다.

단말기와 현재 장치 배치만 적용할 때는 **`COLA_COLA > Build and Place Status Terminal`**을 사용한다. `StatusTerminalSceneTools.BuildAndPlace`는 저장된 씬을 부분 수정하고 `output/status-terminal/MainScene-before-status-terminal.unity`에 최초 백업을 남긴다. `Verify Status Terminal` 메뉴는 저장본의 실제 Play에서 숫자 변경·피해 점멸·범위 초과·비활성화 초기화를 확인한다. 전체 `MainSceneTools.Build`에도 단말기 적용이 연결되어 있다.

`Build`에는 Cinemachine과 호러 조명·후처리 적용도 포함한다. 현재 배치를 보존하면서 해당 연출만 적용할 때는 `COLA_COLA > Apply Horror Lighting and Cinemachine`을 사용한다. 조정 값과 재질·Volume 경로는 `Docs/Art/HorrorAtmosphere.md`를 참고한다.

컴파일과 엔진 검증 결과는 `output/scene-layout`, `output/horror-atmosphere`, `output/status-terminal`의 로그와 validation 기록을 기준으로 확인한다. 호러 조명, 안개, ACES·색 보정·그레인·비네트·Bloom·약한 색 분리를 적용한다. 카메라 출력 Dithering을 사용하며, 컨셉아트와 동일한 전체 화면 픽셀 셰이더는 별도 구현 범위다.

아이템 보유 공간만 다시 배치할 때는 `COLA_COLA > Place Six Test Items Per Side`를 사용한다. 밸브·냉각팩·쿠폰·사탕·뚜껑·스포이트 병의 프리팹을 양쪽에 재사용한다. 개수·크기·쟁반·검증 경로는 `Docs/Art/TestItemLayout.md`를 따른다. 현재 아이템은 외형과 공간 확인용이며 효과·상점과 연결하지 않았다.

## 검증 기록

Unity 6000.6.4f1을 사용한다. 저장본의 카메라·AudioListener·재질·누락 스크립트·남은 아이템 Cube는 `MainScene-validation.txt`, 클립 곡선과 Controller는 `ConsoleIdle-assets-validation.txt`를 기준으로 확인한다. 장치 정렬과 관련 없는 배치의 보존은 `PlayerFacingDevices-validation.txt`, 손의 지지 자세와 베이크 오차는 `NaturalIdle-authoring-validation.txt`, 실제 자동 재생과 30초 반복은 `Idle-playmode-validation.txt`에 기록한다.

자연스러운 Idle 제작 단계의 실제 Play 검증은 31.01초이며 두 Animator가 기본 `Idle`에 자동 진입해 normalizedTime 1.024까지 반복했다. 캐릭터 루트는 고정되고 손목 지지 위치 오차는 0.0005mm 이하였다. 손 메시의 최저 높이는 월드 Y 0.83300m로 테이블보다 3mm 높았다. 당시 장치 Y 회전 -31.44872°와 재적용 시 추가 회전 0°, 무대 로컬 Transform 94개 보존을 확인했다. 이 회전값은 단말기 추가 전 기록이며 현재 장치 위치·회전은 위 표를 따른다.

자연스러운 Idle 작업 시 저장본의 Renderer 77개, 카메라·AudioListener 각 1개, 누락 스크립트 0개, 아이템 Cube 4개를 확인했다. 이 수치는 호러 연출용 조명 메시 추가 전 기록이다. 해당 작업에서 `SampleScene.unity`, `URPProjectSettings.asset`, `QualitySettings.asset`, `EditorSettings.asset`은 작업 전후 동일했다. 파일별 기록은 `output/scene-layout/preserved-files-natural-idle.json`에 있다.

실제 Play 프레임 150장으로 만든 30초 Idle 제작 당시 미리보기는 `output/scene-layout/MainScene-natural-idle-preview.gif`이며, `MainScene-idle-preview.gif`도 같은 기록이다. 당시 장치 근접 렌더는 `ShakeConsole-closeup.png`다. 실제 경기 규칙·독립 실행 빌드는 이 아트 작업의 검증 범위에 포함하지 않는다.

호러 연출 작업의 배치 보존은 `output/verification/latest/horror-atmosphere/Placement-validation.txt`, Cinemachine 활성 상태·기존 구도·URP Volume 평가·형광등 밝기 변화의 실제 Play 검증은 `Horror-validation.txt`에 기록한다. 적용 후 렌더는 `MainScene-Horror.png`이며, `output/scene-layout/MainScene-preview.png`도 최신 화면으로 갱신한다.

호러 연출 검증에서는 게임 시간 18.00초와 연속 렌더 50장을 기록했고, Cinemachine `6.6.0 (BuiltIn)`의 `CM_SeatedDuel`이 기존 구도를 유지했다. HDR·카메라 후처리·Global Volume이 활성 상태이며, 배경 형광등 강도 `0.6092~0.8726`과 일정한 테이블 키 라이트를 확인했다. 기존 카메라·모델·본의 월드 자세 269개를 보존했고, 공용 파이프라인·Volume과 기존 설정 파일 8개의 해시는 작업 전과 동일했다. 파일 목록은 `output/horror-atmosphere/original-file-hashes.json`에 있다.

호러 연출 적용 당시 저장본을 다시 열어 조명 메시 포함 Renderer 81개, 재질·셰이더 참조, 카메라·AudioListener 각 1개, 누락 스크립트 0개, 아이템 Cube 4개와 두 캐릭터의 반복 Idle Controller를 확인했다. 이 Renderer 수는 단말기 추가 전 기록이다. 현재 저장본의 수치는 `output/verification/latest/scene-layout/MainScene-validation.txt`를 기준으로 한다.

이후 인디 화면 질감으로 Film Grain을 Thin2 / 0.26 / Response 0.60으로 조정하고, 약한 검정 레벨 상승·출력 Dithering·완화한 대비와 비네트를 적용했다. 해당 작업 당시 화면은 `output/indie-look/MainScene-Indie.png`, 움직이는 질감은 `Indie-grain-preview.webp`에서 볼 수 있다. 상세 값은 `Docs/Art/HorrorAtmosphere.md`의 인디 화면 질감 항목을 따른다.

단말기 최초 적용 시 무관한 월드 Transform 235개와 설정·패키지·Volume·Idle 관련 파일 21개를 보존했다. 단말기 실제 Play 검증에서 상태값·체력 비율·음수·숫자 표시 범위·피해/토큰 피드백·비활성화 초기화와 기존 Idle 지속을 확인했다. 결과는 `output/verification/latest/status-terminal/Placement-preservation.txt`, `preserved-files-after.json`, `Display-playmode-validation.txt`에 있다. 최신 전체 화면은 `output/status-terminal/MainScene-StatusTerminal.png`, 근접 화면은 `StatusTerminal-closeup.png`다.

테스트 아이템 배치 당시의 전체 화면은 `output/item-layout/MainScene-Items.png`다. 플레이어·보스 각 6개, 총 12개의 3D 소품과 서빙 쟁반을 저장했다. 고정 카메라의 실제 가림 검사에서 모든 소품이 단독 렌더 대비 91.1% 이상 보이며, 기존 강도 선택·콜라 클릭 확정·병 초기화와 Idle 지속을 실제 Play에서 재확인했다. 상세 기록은 `Docs/Art/TestItemLayout.md`와 `output/item-layout`을 따른다.

최신 화면은 압력계를 18% 축소하고 다이얼·테두리와 국소 조명의 시선 균형을 조정한 `output/gauge-focus/MainScene-Focus.png`다. `output/scene-layout/MainScene-preview.png`도 갱신했다. 압력계 받침 접촉점·다른 Transform 369개·보호 파일 32개를 보존했고 기존 버튼·콜라 입력을 실제 Play에서 재검증했다. 화면 경계 면적은 기존의 65.2%다.

플레이어·보스의 강도별 콜라 흔들기는 캐릭터와 병의 클립 총 12개로 구성된다. 추가했던 소매 보조 메시를 제거하고 원래 FPS 팔 모델을 사용한다. 보스는 일반 플레이의 다음 차례에 자동으로 흔들도록 연결했다. 자세한 내용은 `Docs/Art/ColaShakeAnimations.md`, 동작 검증은 Play 모드에서 진행하며 `output/verification/latest/shake-animation/Playmode-validation.txt`에 상태 검사 결과를 기록한다. 시각적 확인은 실제 Game 뷰에서 한다.

플레이어의 `Wrist.R`와 보스의 `hand.R`를 명시적으로 연결했다. 병은 잡은 손의 위치·회전을 따라가고 기다리는 상대의 양손은 테이블에 둔 Idle을 유지한다. 원본 FBX 스킨 메시의 실제 정점 이동과 자동 보스 응답까지 확인하며, 검증 시 원본 모델·프리팹·텍스처도 해시로 비교한다.

## 현재 검증 원칙

과거에 기록한 프리뷰 렌더·캡처 수치는 당시 제작 참고 자료다. 앞으로의 변경 검증은 Play 모드에서 진행하며, 실제 Game 뷰 확인과 자동 상태 검사의 범위를 구분한다. `MainSceneTools.Verify`도 Play에 진입해 검사하며 검증용 이미지를 생성하지 않는다.

현재 보스 대기 동작·카메라 호흡·예상 바늘/확률의 부드러운 전환은 `Docs/Art/PresentationMotion.md`를 따른다. 카메라의 기준 Transform·FOV는 유지하며 Cinemachine 출력에만 작은 호흡 보정을 더한다.
