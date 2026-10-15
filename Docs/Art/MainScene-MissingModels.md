# COLA_COLA 메인 씬 — 미보유 모델 및 후속 아트 작업

## 기준

- 확정 컨셉아트: `C:/Work/COLA_COLA/output/concept-art/pizzeria-boss-v4.png`
- 세부 디자인 근거: `C:/Work/COLA_COLA/output/concept-art/pizzeria-boss-v4-prompt.md`, `C:/Work/COLA_COLA/output/concept-art/pizzeria-boss-v2-prompt.md`
- 구성: 피자리아 내부에서 플레이어와 보스가 테이블을 사이에 둔 1대1 대치. 중앙 콜라, 화면 아래 플레이어 팔, 맞은편 보스, 좌측 실물 압력계와 강도 버튼, 양쪽 테이블 위 실물 아이템.
- 아래 이름은 남은 Cube 대체물의 식별 기준이다. 실제 배치와 완료 기록은 `MainScene-Layout.md` 및 `output/scene-layout` 검증 결과를 함께 확인한다.
- 아이템은 화면에 붙는 슬롯 UI가 아니라 테이블 위 3D 소품이다. 좌측의 `현재 압력`, `흔들기 강도` 문구는 추가하지 않는다.

## 미보유 전용 모델

프로젝트의 모델 파일명과 피자리아 프리팹 내부 오브젝트 이름을 조사했을 때, 아래 컨셉 전용 모델은 확인되지 않았다. 전용 모델을 제작하기 전에는 Unity Cube의 크기·색상·배치로 위치와 부피를 표시한다.

| 필요한 모델 | Cube 식별 이름 | 화면 위치 | 제작할 형태와 특징 |
|---|---|---|---|
| 압력 배출 밸브 | `PLACEHOLDER_ReleaseValve` | 테이블 앞쪽 좌측, 플레이어 손 옆 | 짧은 은색 배관 몸체, 돌출된 출구, 위쪽 붉은 회전 손잡이. 콜라보다 작고 다른 소품과 실루엣이 구분되어야 한다. |
| 냉각팩 | `PLACEHOLDER_ColdPack` | 배출 밸브 옆, 테이블 앞쪽 좌측 | 낮고 넓은 청록색 주머니형 몸체와 작은 흰색 마개. 각진 접힘과 납작한 부피가 보이도록 제작한다. |
| 종이 피자 쿠폰 | `PLACEHOLDER_PizzaCoupon_Player`, `PLACEHOLDER_PizzaCoupon_Boss` | 플레이어 쪽 좌측 아이템 무리와 보스 쪽 우측 아이템 무리 | 크림색 종이를 반으로 접어 세운 작은 소품. 붉은 피자 표시와 쿠폰 인쇄. 양쪽은 같은 모델을 재사용하며 수량·방향만 달리한다. Cube 단계에서는 얇은 직육면체로 표시한다. |

압력계와 흔들기 강도 콘솔은 별도 프리팹으로 제작되어 Cube를 교체했다. 남은 Cube는 밸브·냉각팩·양쪽 쿠폰의 4개다. 접힌 종이·둥근 마개 같은 남은 소품의 세부 실루엣은 아직 구현하지 않았다.

## 제작 완료 모델

| 모델 | 파일 | 완료 범위 |
|---|---|---|
| 탁상 압력계 | `Assets/_COLA_COLA/Art/Prefabs/PressureGauge.prefab` | 12각 청록 몸체, 이중 테두리, 다이얼·눈금·픽셀 `?`·`15`, 별도 `NeedlePivot`. 커스텀 메시 17개, 1,788 triangles. MainScene의 압력계 Cube 교체 및 카메라 렌더 확인. |
| 흔들기 강도 콘솔 | `Assets/_COLA_COLA/Art/Prefabs/ShakeConsole.prefab` | 청록색 경사 몸체, 금속 테두리·소켓, 크림색 1·2 키와 붉은색 3 키, 돌출 픽셀 숫자. 커스텀 메시 18개, 1,808 triangles, 재질 7개. 기존 콘솔 Cube와 버튼 Cube 3개를 교체했다. 각 키는 별도 Transform과 Collider를 갖는다. 저장본 재열기·카메라 렌더를 확인했다. |

압력계 제작 소스와 피벗 설명은 `Docs/Art/PressureGauge.md`, 실제 근접 렌더는 `output/scene-layout/PressureGauge-closeup.png`에 있다. 콘솔과 Idle 설명은 `Docs/Art/ShakeConsole-And-Idle.md`에 있다. 압력계 숫자·바늘과 버튼 선택은 경기 상태 및 입력에 아직 연결하지 않는다.

## 재사용할 수 있는 기존 모델

다음은 프로젝트에 존재하는 에셋이므로 누락 모델 목록에 포함하지 않는다.

| 구성 요소 | 기존 에셋 | 재사용 기준 |
|---|---|---|
| 중앙 콜라와 부푼 콜라 | `Assets/2.Assets/01_Cola_Bottle/Prefabs/cola_bottle_fbx.prefab`, `cola_bottle_pressurized_fbx.prefab` | 중앙 병 및 추후 압력에 따른 외형 변화의 기반. |
| 보스 인물 | `Assets/2.Assets/03_Simple_Character_PSX/Prefabs/simple-character-psx_fbx.prefab` | 맞은편 인물에 사용. 양손을 테이블에 지지하는 비대칭 착석 자세로 조정하고 30초 반복 Idle을 적용했다. |
| 1인칭 플레이어 팔 | `Assets/2.Assets/02_Player_Arms/Prefabs/Player_Arms_fbx.prefab` | 화면 아래 양손에 사용. 손바닥을 테이블에 지지하는 자세로 조정하고 30초 반복 Idle을 적용했다. |
| 피자리아 환경 | `Assets/2.Assets/04_Pizzeria/Prefabs/Pizzeria_Scene_fbx.prefab` | 배경 소품과 텍스처의 기반. 건축 구조는 아래 재배치용 구조 블록으로 구분한다. |
| 대결 테이블 | `Assets/2.Assets/04_Pizzeria/Prefabs/Pizzeria_Props_fbx.prefab`의 `Chair_Table` | 중앙 대결 테이블과 주변 테이블에 재사용한다. |
| 보스 및 주변 벤치 | 같은 피자리아 소품 프리팹의 `Chair_Table.001` | 보스 좌석과 주변 벤치에 재사용한다. |
| 주방 소품 | Pizzeria Props의 `Preparation_table`, `Pizza_oven`, `Soda_machine`과 Pizzeria Scene의 `Pizza_box` 등 | 카운터·주방·음료기·피자 상자를 구성하는 기존 모델을 재사용한다. |
| 보스 쪽 콜라 캔 | 같은 피자리아 소품 프리팹의 `Can_Soda` | 기존 캔 모델 재사용. |
| 보스 쪽 집게 | 같은 피자리아 소품 프리팹의 `tongs` | 기존 집게 모델 재사용. |

## 재배치용 건축 구조 블록

원본 피자리아의 `building_01.001`은 외벽·바닥·천장이 하나로 결합된 메시다. 기존 건축 모델이 누락된 것은 아니며, 컨셉에 맞춰 대결 공간과 배경의 거리를 조정하기 위해 이번 구성에서는 벽·바닥·천장을 Cube로 나눈다. 전용 아이템의 임시 대체물과 구별하도록 `ArchitectureBlockout_` 이름을 사용한다.

| 구조 블록 이름 | 외관 재사용 기준 |
|---|---|
| `ArchitectureBlockout_Floor` | 기존 피자리아 `Tiles_0.png` 텍스처를 사용한 바닥 재질 |
| `ArchitectureBlockout_BackWall` | 기존 피자리아 `BrickSmallBrown.jpg` 텍스처를 사용한 벽돌 재질 |
| `ArchitectureBlockout_LeftWall` | 같은 벽돌 재질 |
| `ArchitectureBlockout_RightWall` | 같은 벽돌 재질 |
| `ArchitectureBlockout_Ceiling` | 기존 피자리아 `Office_Ceiling.jpg` 텍스처를 사용한 천장 재질 |
| `ArchitectureBlockout_MenuBoard` | 기존 피자리아 `Menu.png`를 사용한 얇은 메뉴 보드 |

이 구조 블록은 카메라 구도에 맞춘 실내 배치를 위한 것이다. 정교한 출입구·창문·몰딩 등의 건축 디테일을 구현했다는 의미는 아니다.

## 모델 누락과 구분할 후속 작업

- **보스 동작:** 벤치에 앉고 양손을 테이블에 지지한 자세에서 불균일한 호흡과 간헐적인 고개·시선·체중 이동을 30초 Idle로 적용했다. 흔들기·피격·아이템 사용 등 행동 애니메이션은 후속 작업이다.
- **플레이어 손 동작:** 카메라 하단에서 손을 테이블에 지지하고 호흡에 따른 위팔의 미세한 움직임과 드문 손가락 이완을 30초 Idle로 적용했다. 흔들기·버튼 누르기·아이템 사용 등 행동 애니메이션은 후속 작업이다.
- **우측 상태 UI:** v4의 보스/플레이어 이름, 분절형 픽셀 체력바, 숫자, 토큰은 화면 HUD 디자인이다. 이번 모델·카메라 배치 범위가 아니며, 3D 모델 누락 목록과 Cube 대체 대상에 포함하지 않는다.
- **상호작용과 연출:** 바늘 이동, 버튼 입력, 병 흔들기·팽창·폭발, 아이템 사용, 체력·토큰 갱신은 배치와 별개 구현 작업이다.
- **렌더 스타일:** 컨셉의 픽셀감, 낮은 해상도 텍스처, 디더링, 조명은 모델 배치만으로 완성되지 않는다. 기존 재질 적합성 및 최종 화면은 별도 렌더 확인이 필요하다.

## 확인 범위

이 문서는 컨셉 이미지·생성 프롬프트, 프로젝트 모델 파일 목록, 피자리아 프리팹의 오브젝트 이름과 씬·모델 생성 도구를 기준으로 작성했다. 메인 씬 대상 경로는 `Assets/Scenes/MainScene.unity`다. 모든 메시의 세부 형상, 동작의 자연스러움, 최종 카메라에서의 가림·비율·조명은 목록 조사만으로 검증되지 않는다. 씬 생성·배치·렌더·재생 검증의 완료 여부는 실제 씬과 검증 결과를 기준으로 판단한다.

MainScene 저장·재열기와 카메라 렌더의 기준 기록은 `output/scene-layout/MainScene-validation.txt`, 실제 화면은 `output/scene-layout/MainScene-preview.png`, 배치 설명은 `Docs/Art/MainScene-Layout.md`에 있다. 콘솔 교체 후 남은 Cube 4개를 확인했다. 실제 Play 모드를 31.01초 실행해 두 캐릭터의 기본 Idle 자동 시작과 30초 경계를 넘는 반복 재생을 확인했으며 기록은 `output/scene-layout/Idle-playmode-validation.txt`에 있다. 30초 미리보기는 `output/scene-layout/MainScene-natural-idle-preview.gif`다. 경기 행동과 연결한 최종 애니메이션 검증은 후속 작업이다.
