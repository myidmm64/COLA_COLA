# 압력계 모델

## 디자인과 범위

확정 컨셉 `output/concept-art/pizzeria-boss-v4.png`를 기준으로 만든 메인 씬 좌측의 실물 3D 소품이다. 청록색 다각형 몸체, 크림색 다이얼, 금속 테두리, 눈금, 붉은 바늘과 검은 균형추, 비공개 한계 기호 `?`, 숫자창 `15`로 구성한다. `현재 압력`과 `흔들기 강도` 제목은 넣지 않는다.

이 프리팹은 압력계 외형을 담당한다. 아래쪽 콘솔과 버튼은 `ShakeConsole.prefab`으로 분리했으며, 메인 씬에서는 두 장치를 함께 돌려 플레이어가 앉은 방향을 바라보게 한다. 버튼 피벗과 배치 설명은 `Docs/Art/ShakeConsole-And-Idle.md`에 있다. 숫자 `15`와 바늘 방향은 정적 시안이며 경기의 실제 압력 계산에는 아직 연결하지 않는다.

## 파일

- 프리팹: `Assets/_COLA_COLA/Art/Prefabs/PressureGauge.prefab`
- 메시: `Assets/_COLA_COLA/Art/Models/PressureGauge`
- 재질: `Assets/_COLA_COLA/Art/Materials/PressureGauge`
- 모델 생성 소스: `Assets/Editor/COLACOLA/PressureGaugeModelFactory.cs`
- 기존 씬의 압력계만 교체하는 도구: `Assets/Editor/COLACOLA/PressureGaugeSceneTools.cs`
- 압력계·콘솔 방향 정렬: `Assets/Editor/COLACOLA/PlayerFacingDeviceLayout.cs`
- 씬: `Assets/Scenes/MainScene.unity`
- 모델 근접 렌더: `output/scene-layout/PressureGauge-closeup.png`
- 교체 검증: `output/scene-layout/PressureGauge-validation.txt`

## 조립과 피벗

정면은 로컬 **-Z**, 위쪽은 **+Y**다. 기본 크기는 약 가로 0.34m, 높이 0.42m, 두께 0.12m이며 실제 완성 경계와 삼각형 수는 검증 기록을 기준으로 한다.

몸체·테두리·다이얼·눈금·숫자창을 각각 메시로 구분한다. 바늘은 다이얼 중심의 `NeedlePivot` 아래에 있어 로컬 Z축 회전으로 방향을 조정할 수 있다. 글자는 외부 폰트에 의존하지 않는 픽셀 형태의 메시로 표현한다.

메인 씬의 방향은 콘솔 중심 `(-0.74, 0.90, -0.19)`에서 플레이어 카메라를 향하는 수평 벡터로 계산한다. 초기 배치 기준 Y 회전은 약 `-31.45°`다. 압력계도 같은 각도로 콘솔 중심을 돌아 이동하므로 받침과 다이얼의 상대 배치·높이를 유지한다. 프리팹 자체의 로컬 정면과 버튼 상판 경사는 바꾸지 않는다.

## 교체 방식

`PressureGaugeSceneTools.BuildAndReplace`는 저장된 MainScene의 `PLACEHOLDER_PressureGauge` 위치를 읽어 실제 프리팹으로 교체한다. 방·보스·카메라를 재생성하지 않는다. 기존 씬 백업은 `output/scene-layout/MainScene-before-pressure-gauge.unity`에 남긴다. 원본 Cube 이외 오브젝트의 위치·회전이 보존되는지 검사하고, 저장본을 다시 열어 재질·카메라·남은 Cube를 확인한다.

현재 배치의 플레이어 방향 정렬은 `COLA_COLA > Revise Player Facing Devices and Natural Idle` 메뉴 또는 `ConsoleAndIdleSceneTools.ReviseNaturalIdle`에서 적용한다. 이 도구는 장치 정렬과 새 Idle을 함께 적용하며, 적용 전 씬은 `output/scene-layout/MainScene-before-natural-idle.unity`에 백업한다. 정렬 결과는 `output/scene-layout/PlayerFacingDevices-validation.txt`에 기록한다.

후속 작업에서는 바늘 회전과 숫자창을 승인된 게임 상태에 연결하면 된다. 압력 한계의 실제 숫자는 표시 대상에 포함하지 않는다.

## 검증 결과

압력계 모델은 커스텀 메시 17개, 삼각형 1,788개이며 로컬 완성 경계는 `(0.34, 0.42, 0.12)m`다. 콘솔·버튼도 별도 모델로 교체한 MainScene에는 아이템 Cube 4개가 남는다.

`PlayerFacingDevices-validation.txt`에서 최종 Y 회전 -31.44872°, 재적용 시 회전 변화 0°, 장치 정면과 카메라 방향의 내적 1.0을 확인했다. 콘솔 중심과 장치 사이 간격·장착 높이를 유지했다. 버튼 앞 인접 아이템의 윗면은 키 상단보다 약 4.8~6.8cm 낮고 접근 경로 경고는 0개다. 최신 `ShakeConsole-closeup.png`와 `MainScene-preview.png`에서 플레이어 방향으로 놓인 장치 전체를 볼 수 있다.
