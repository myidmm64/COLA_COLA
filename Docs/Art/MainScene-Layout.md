# COLA_COLA 메인 씬 배치

## 씬과 기준 이미지

- 씬: `Assets/Scenes/MainScene.unity`
- 확정 컨셉: `output/concept-art/pizzeria-boss-v4.png`
- 실제 Unity 카메라 렌더: `output/scene-layout/MainScene-preview.png`
- 호러 조명 적용 후 렌더: `output/horror-atmosphere/MainScene-Horror.png`
- Cinemachine·호러 조명·후처리 구성: `Docs/Art/HorrorAtmosphere.md`
- 누락 모델과 Cube 대체 목록: `Docs/Art/MainScene-MissingModels.md`
- 실제 배치값: `output/scene-layout/MainScene-layout.txt`
- 엔진 재열기 검증 기록: `output/scene-layout/MainScene-validation.txt`

이 씬은 보스와의 1대1 구도를 위한 모델 배치와 대기 애니메이션을 포함한다. 게임 규칙, 보스 AI, 흔들기·아이템 사용 애니메이션, 입력 기능과 우측 HUD는 별도 구현 범위다.

## 열어서 확인하기

Unity의 `COLA_COLA > Open Main Scene` 메뉴로 열 수 있다. Game 뷰는 **16:9**로 확인한다. Play를 누르면 플레이어 팔과 보스의 Idle이 자동으로 반복된다. 빌드 설정의 첫 번째 활성 씬은 MainScene이며 기존 SampleScene도 보존한다.

카메라는 테이블 앞에 앉은 1인칭 구도다. 위치 `(0, 1.32, -1.40)`, 회전 `(11, 0, 0)`, 수직 FOV `50`, Near Clip `0.035`를 사용한다. Cinemachine `CM_SeatedDuel`과 Main Camera의 Brain이 이 구도를 유지한다. Main Camera에 HDR와 URP 후처리를 켜고, 씬 전용 Global Volume으로 화면 효과를 적용한다.

## 계층

| 그룹 | 구성 |
|---|---|
| `01_Pizzeria` | 벽·바닥·천장 배치용 Cube, 메뉴 텍스처 보드, 기존 카운터·오븐·음료 기계·피자 박스·옆 테이블·벤치·천장등 |
| `02_DuelTable` | 기존 식탁·보스 벤치·중앙 콜라 |
| `03_Characters` | 기존 PSX 보스와 1인칭 팔, 테이블 자세를 유지하는 Idle Animator |
| `04_TabletopItems` | 기존 음료 캔·집게 |
| `05_CubePlaceholders` | 밸브·냉각팩·양쪽 쿠폰, 총 4개 |
| `06_Lighting` | 일정한 테이블 키 라이트, 얼굴·콜라 라벨 보조광, 보스의 붉은 역광, 압력계 보조광, 녹색 주방·음료 기계 조명, 약하게 밝기가 변하는 형광등, 붉은 벽등 주변광 등 조명 10개 |
| `07_Cameras` | Main Camera·AudioListener·CinemachineBrain, 고정 구도의 `CM_SeatedDuel` CinemachineCamera |
| `08_Devices` | 제작된 실제 압력계와 1·2·3 버튼 콘솔 프리팹 |
| `09_Atmosphere` | 씬 전용 Global Volume, 녹색 주방 스트립과 붉은 벽 비상등 메시 |

아이템은 Canvas 슬롯이 아니라 테이블 위 월드 오브젝트다. 좌측 장치에 `현재 압력`, `흔들기 강도` 문구를 넣지 않았다. 압력계와 버튼 콘솔은 실제 저폴리 메시로 교체했다. 콘솔 중심 `(-0.74, 0.90, -0.19)`을 유지하고 압력계와 콘솔을 함께 Y축으로 약 `-31.45°` 돌려 플레이어 방향을 바라보게 했다. 압력계와 콘솔의 조립 간격·높이와 상판 경사는 유지한다. 남은 밸브·냉각팩·쿠폰은 Cube의 부피·위치로 표시한다.

## 기존 에셋을 사용하는 방식

- 식탁은 `Pizzeria_Props_fbx/Chair_Table`, 벤치는 `Chair_Table.001`을 사용한다.
- 캔·집게·카운터·오븐·음료 기계는 Pizzeria Props의 자식 모델을 분리 복제한다. 피자 박스와 천장등은 Pizzeria Scene에서 가져온다.
- FBX의 축·단위 변환을 보존한 뒤 경계 크기로 정규화한다. 각 소품의 최상위 Transform은 배치 위치를 가리키므로 Inspector에서 옮기기 쉽다.
- 원본 건물은 벽·실내 바닥·천장이 결합된 메시라 이번 실내는 Cube 구조블록과 기존 피자리아 텍스처로 구성한다. 메뉴도 기존 `Menu.png`를 입힌 얇은 구조블록이다.
- 보스와 팔은 기존 Generic 리그를 사용한다. 양손을 테이블에 지지하는 자세로 다시 잡고, 각각 30초의 Idle 클립과 Animator Controller를 적용한다. 길이와 깊이가 다른 호흡 6번, 보스의 간헐적인 고개·시선·체중 이동, 플레이어의 드문 손가락 이완을 배치한다. 제작 중 팔 IK로 손의 위치와 방향을 유지한 결과를 일반 AnimationClip으로 굽기 때문에 런타임 IK 의존성은 없다. 원본 FBX는 수정하지 않는다.
- 캐릭터와 콜라의 재질 보정은 `Assets/_COLA_COLA/Art/Materials/Blockout`의 복사본에 적용했다. 임포트한 원본 재질은 보존한다.

## 재생성과 검증

에디터 전용 도구는 `Assets/Editor/COLACOLA/MainSceneTools.cs`에 있다.

- `Build`: 컨셉 기준 씬을 재생성한다. 기존 MainScene이 있으면 `output/scene-layout/MainScene-before-rebuild.unity`에 백업하고 생성본으로 저장한다. 수동 배치 수정 후에는 씬 재생성 대신 일반 편집을 사용한다.
- `Verify`: 저장된 씬을 다시 열어 카메라·AudioListener·누락 스크립트·재질 참조·Cube 대체물·시작 씬·중앙 콜라 구도를 확인하고 실제 렌더를 출력한다.
- `OpenMainScene`: 일반 편집기에서 메인 씬과 Game 뷰를 연다.

`Build`에는 압력계·콘솔 생성, 플레이어 방향 정렬과 새 Idle 적용도 포함한다. 현재 배치의 장치 방향과 Idle만 수정할 때는 `COLA_COLA > Revise Player Facing Devices and Natural Idle` 메뉴 또는 `Assets/Editor/COLACOLA/ConsoleAndIdleSceneTools.cs`의 `ReviseNaturalIdle`을 사용한다. 적용 전 백업은 `output/scene-layout/MainScene-before-natural-idle.unity`에 저장한다. 방향 정렬 코드는 `PlayerFacingDeviceLayout.cs`다. 에셋·버튼 피벗·애니메이션과 기준 자세 JSON의 사용법은 `Docs/Art/ShakeConsole-And-Idle.md`를 참고한다.

`Build`에는 Cinemachine과 호러 조명·후처리 적용도 포함한다. 현재 배치를 보존하면서 해당 연출만 적용할 때는 `COLA_COLA > Apply Horror Lighting and Cinemachine`을 사용한다. 조정 값과 재질·Volume 경로는 `Docs/Art/HorrorAtmosphere.md`를 참고한다.

컴파일과 엔진 검증 결과는 `output/scene-layout` 및 `output/horror-atmosphere`의 로그와 validation 기록을 기준으로 확인한다. 호러 조명, 안개, ACES·색 보정·그레인·비네트·Bloom·약한 색 분리를 적용한다. 컨셉아트와 동일한 픽셀 셰이더·디더링은 별도 구현 범위다.

## 검증 기록

Unity 6000.6.4f1을 사용한다. 저장본의 카메라·AudioListener·재질·누락 스크립트·남은 아이템 Cube는 `MainScene-validation.txt`, 클립 곡선과 Controller는 `ConsoleIdle-assets-validation.txt`를 기준으로 확인한다. 장치 정렬과 관련 없는 배치의 보존은 `PlayerFacingDevices-validation.txt`, 손의 지지 자세와 베이크 오차는 `NaturalIdle-authoring-validation.txt`, 실제 자동 재생과 30초 반복은 `Idle-playmode-validation.txt`에 기록한다.

최신 실제 Play 검증은 31.01초이며 두 Animator가 기본 `Idle`에 자동 진입해 normalizedTime 1.024까지 반복했다. 캐릭터 루트는 고정되고 손목 지지 위치 오차는 0.0005mm 이하였다. 손 메시의 최저 높이는 월드 Y 0.83300m로 테이블보다 3mm 높았다. 장치의 최종 Y 회전은 -31.44872°이고 재적용 시 추가 회전은 0°였다. 장치·캐릭터 외 무대 로컬 Transform 94개를 유지했다.

자연스러운 Idle 작업 시 저장본의 Renderer 77개, 카메라·AudioListener 각 1개, 누락 스크립트 0개, 아이템 Cube 4개를 확인했다. 이 수치는 호러 연출용 조명 메시 추가 전 기록이다. 해당 작업에서 `SampleScene.unity`, `URPProjectSettings.asset`, `QualitySettings.asset`, `EditorSettings.asset`은 작업 전후 동일했다. 파일별 기록은 `output/scene-layout/preserved-files-natural-idle.json`에 있다.

실제 Play 프레임 150장으로 만든 30초 미리보기는 `output/scene-layout/MainScene-natural-idle-preview.gif`이며, 기존 `MainScene-idle-preview.gif` 경로도 같은 내용으로 갱신했다. 장치 근접 렌더는 `ShakeConsole-closeup.png`다. 게임 규칙·HUD·독립 실행 빌드는 이 배치 작업의 검증 범위에 포함하지 않는다.

호러 연출 작업의 배치 보존은 `output/horror-atmosphere/Placement-validation.txt`, Cinemachine 활성 상태·기존 구도·URP Volume 평가·형광등 밝기 변화의 실제 Play 검증은 `Horror-validation.txt`에 기록한다. 적용 후 렌더는 `MainScene-Horror.png`이며, `output/scene-layout/MainScene-preview.png`도 최신 화면으로 갱신한다.

호러 연출 검증에서는 게임 시간 18.00초와 연속 렌더 50장을 기록했고, Cinemachine `6.6.0 (BuiltIn)`의 `CM_SeatedDuel`이 기존 구도를 유지했다. HDR·카메라 후처리·Global Volume이 활성 상태이며, 배경 형광등 강도 `0.6092~0.8726`과 일정한 테이블 키 라이트를 확인했다. 기존 카메라·모델·본의 월드 자세 269개를 보존했고, 공용 파이프라인·Volume과 기존 설정 파일 8개의 해시는 작업 전과 동일했다. 파일 목록은 `output/horror-atmosphere/original-file-hashes.json`에 있다.

최종 저장본을 다시 열어 조명 메시 포함 Renderer 81개, 재질·셰이더 참조, 카메라·AudioListener 각 1개, 누락 스크립트 0개, 아이템 Cube 4개와 두 캐릭터의 반복 Idle Controller를 확인했다. 현재 수치는 `output/scene-layout/MainScene-validation.txt`를 기준으로 한다.

이후 인디 화면 질감으로 Film Grain을 Thin2 / 0.26 / Response 0.60으로 조정하고, 약한 검정 레벨 상승·출력 Dithering·완화한 대비와 비네트를 적용했다. 최신 화면은 `output/indie-look/MainScene-Indie.png`, 움직이는 질감은 `Indie-grain-preview.webp`에서 볼 수 있다. 조명·모델·카메라 구도·Idle은 유지하며 상세 값은 `Docs/Art/HorrorAtmosphere.md`의 인디 화면 질감 항목을 따른다.
