# 압력계와 흔들기 선택

## 현재 동작

- 다이얼 마지막 눈금의 `?`를 **현재 콜라의 공개 한계 압력**으로 교체했다. 외부 경기 진행에서 `BeginBottle(limit)`로 바꿀 수 있다.
- 기존 `15` 숫자창은 **선택한 강도로 흔든 뒤의 예상 폭발 확률**을 표시한다. 선택 전에는 `--%`다.
- 빨간 바늘은 현재 압력, 주황색 점선 바늘은 선택 후 예상 압력이다.
- 1·2·3 버튼은 강도 선택만 담당한다. 선택한 키는 12mm 눌린 상태가 되고, 바늘·확률만 갱신한다.
- **중앙 콜라를 직접 클릭해야** 압력 증가와 폭발 판정을 한 번 확정한다. 강도 미선택, 흔들기 연출 중 연타·선택 변경은 진행하지 않는다.
- 폭발 후에는 압력을 0으로 초기화하고 새 콜라의 한계를 공개한다.

확률은 사용자 선택에 따라 `(흔든 뒤 압력 / 한계 압력)^2`의 제곱 곡선으로 확정했다. 한계 20, 흔든 뒤 압력 10이면 25%다. 비율은 0~1로 제한하여 한계 도달·초과는 100%다. 한계 범위 18~24와 강도별 증가량 1·2·3은 임시 설정이며, 지수와 한계 범위는 설정 에셋에서 바꾼다. 보스 AI·피해·토큰 정산·상점 흐름은 이 입력 프로토타입의 구현 범위에 포함하지 않는다.

## 에셋과 코드

| 구성 | 경로 |
|---|---|
| 압력계 프리팹 | `Assets/_COLA_COLA/Art/Prefabs/PressureGauge.prefab` |
| 메시 / 재질 | `Assets/_COLA_COLA/Art/Models/PressureGauge`, `Assets/_COLA_COLA/Art/Materials/PressureGauge` |
| 모델 생성 | `Assets/Editor/COLACOLA/PressureGaugeModelFactory.cs` |
| 실제 압력 / 예측 / 확정 규칙 | `Assets/_COLA_COLA/Scripts/Core/BottlePressureModel.cs` |
| 바늘 / 마지막 눈금 / 확률 표시 | `Assets/_COLA_COLA/Scripts/Presentation/PressureGaugeDisplay.cs` |
| 물리 버튼 / 콜라 클릭 / 흔들기 피드백 | `Assets/_COLA_COLA/Scripts/Presentation/ShakeSelectionInteraction.cs` |
| 편집용 설정 | `Assets/_COLA_COLA/Data/PressureInteractionSettings.asset` |
| 부분 적용 / 실제 Play 검증 | `Assets/Editor/COLACOLA/PressureInteractionSceneTools.cs` |

## 모델과 배치

기존 청록색 12각 몸체·크림색 다이얼·금속 테두리와 버튼 콘솔을 유지한다. 정면은 로컬 **-Z**, 위는 **+Y**다. 프리팹 기본 크기는 약 `(0.34, 0.42, 0.12)m`, 메인 씬 배율은 `0.82`로 약 18% 줄였다. 씬의 실제 크기는 약 `(0.28, 0.34, 0.10)m`다. 바늘은 `NeedlePivot`, 예측 바늘은 `PreviewNeedlePivot` 아래에 있다. 두 바늘은 압력 비율에 따라 225°부터 -45°까지 움직인다.

숫자·기호는 공유 픽셀 메시로 표시한다. 마지막 눈금은 최대 세 자리의 양의 정수, 확률창은 최대 여섯 글자다. 아주 작은 양수 확률은 `0%`로 오해하지 않도록 `<0.1%`, 확정 폭발은 `100%`로 표시한다.

현재 콘솔 중심은 `(-0.34, 0.90, -0.10)`, 압력계는 `(-0.3602, 1.1522, -0.0226)`이며 Y 회전은 약 `345.34°`다. 축소할 때 받침의 장착 위치를 고정해 압력계가 콘솔 위에 뜨지 않도록 중심을 약 3.8cm 낮췄다. 플레이어 화면에서 **상태 단말기 → 압력계 → 중앙 콜라** 순서를 유지한다. 버튼 콘솔·카메라·콜라·캐릭터·양쪽 아이템의 크기와 위치는 보존한다.

## 중앙 콜라와 시선 균형

압력계가 중앙 콜라보다 먼저 눈에 들어오던 화면을 조정했다. 다이얼을 어두운 회색빛 크림색으로 낮추고, 안쪽 금속 테두리와 위쪽 청록 캡의 밝기·반사를 줄였다. 빨간 현재 바늘·주황색 예측 바늘·공개 한계·확률 표시는 유지한다.

기존 압력계 보조광 강도는 `0.40 → 0.16`, 중앙 콜라 라벨 보조광은 `0.55 → 0.90`으로 조정했다. 콜라 쪽은 조금 따뜻한 색과 `46°` 범위를 사용한다. 전역 노출·그레인·비네트·배경 조명은 유지한다.

부분 적용 메뉴는 `COLA_COLA > Balance Gauge and Cola Focus`, 도구는 `Assets/Editor/COLACOLA/TabletopFocusSceneTools.cs`다. 다시 적용해도 배율이 누적되지 않으며 전체 `MainSceneTools.Build`에도 연결했다. 기존 압력계 교체 도구는 씬의 배율을 보존하고, 모델 생성기의 팔레트도 같은 색을 사용한다.

실제 화면은 `output/gauge-focus/MainScene-Focus.png`, 이전 화면은 `Before.png`, 배치·화면 면적 검사는 `Focus-validation.txt`다. 같은 폴더에 적용 전 씬과 세 재질의 백업을 남긴다. 버튼 선택·예측·콜라 클릭 확정은 `output/pressure-interaction`의 실제 Play 검증을 사용한다.

Unity 저장본 재열기와 실제 Play 검증을 통과했다. 고정 카메라에서 압력계 경계가 차지하는 면적은 이전의 65.2%이며 받침 접촉점 Y `0.9800m`를 유지했다. 압력계 외 Transform 369개와 기존 장치·아이템 프리팹, 설정·Idle 등 보호 파일 32개를 보존했다. 한계 숫자·현재/예측 바늘·확률과 1·2·3 선택 후 콜라 클릭 확정도 유지된다.

## 적용과 검증

`COLA_COLA > Apply Pressure Preview and Cola Confirmation`은 저장된 MainScene의 압력계만 교체하고 기존 위치·회전·크기를 보존한다. 콘솔·중앙 콜라에 입력을 연결한다. 적용 전 씬은 `output/pressure-interaction/MainScene-before-pressure-interaction.unity`에 남긴다. 전체 씬 재생성 대신 이 부분 적용 메뉴를 사용한다.

`COLA_COLA > Verify Pressure Preview and Confirmation`은 저장본을 다시 열고 실제 Play에서 3D Raycast로 세 버튼과 콜라를 클릭한다. 미리보기 중 압력·난수가 변하지 않는지, 예측과 확정 결과가 같은지, 연타·선택 초기화·폭발 후 새 콜라·중단 후 지연 콜백을 검사한다.

| 출력 | 경로 |
|---|---|
| 강도별 미리보기 | `output/pressure-interaction/Preview-Strength1.png` ~ `Preview-Strength3.png` |
| 강도 3 선택 근접 화면 | `output/pressure-interaction/Preview-Closeup.png` |
| 선택 전 저장본 화면 | `output/pressure-interaction/MainScene-PressureInteraction.png` |
| 새 한계 압력 예시 | `output/pressure-interaction/ChangedLimit-28.png` |
| 규칙 / 실제 Play / 저장본 검사 | `Core-validation.txt`, `Playmode-validation.txt`, `Scene-validation.txt` (동일 출력 폴더) |
| 배치 보존 | `output/pressure-interaction/Placement-preservation.txt` |

미리보기 이미지의 현재 압력 8·한계 20은 검증용 상태다. 저장된 씬은 압력 0·강도 미선택으로 시작하고 Play에서 새 한계를 정한다. 자세한 흐름과 API는 `Docs/Gameplay/PressureSelection.md`를 참고한다. 예전 압력계 방향·정적 15·? 이미지와 검증은 이전 아트 단계 기록이다.
