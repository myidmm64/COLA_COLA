# COLA_COLA 메인 씬 — 미보유 모델 및 후속 아트 작업

## 기준

- 확정 컨셉아트: `C:/Work/COLA_COLA/output/concept-art/pizzeria-boss-v4.png`
- 세부 디자인 근거: `C:/Work/COLA_COLA/output/concept-art/pizzeria-boss-v4-prompt.md`, `C:/Work/COLA_COLA/output/concept-art/pizzeria-boss-v2-prompt.md`
- 구성: 피자리아 내부에서 플레이어와 보스가 테이블을 사이에 둔 1대1 대치. 화면 왼쪽부터 실물 체력/토큰 단말기·압력계·중앙 콜라, 아래 플레이어 팔, 맞은편 보스와 양쪽 테이블 위 실물 아이템.
- 아이템 공간 확인을 위해 소품 6종을 제작해 양쪽에 배치했다. 실제 배치와 완료 기록은 `MainScene-Layout.md`, `TestItemLayout.md` 및 `output/item-layout` 검증 결과를 함께 확인한다.
- 아이템은 화면에 붙는 슬롯 UI가 아니라 테이블 위 3D 소품이다. 좌측의 `현재 압력`, `흔들기 강도` 문구는 추가하지 않는다.

## 미보유 전용 모델

현재 메인 씬에 아이템 대체용 Cube는 없다. 이전 밸브·냉각팩·양쪽 쿠폰의 Cube 4개를 커스텀 로우폴리 모델로 교체하고, 사탕·팽창 뚜껑·스포이트 병을 추가해 여섯 종류를 양쪽에 하나씩 놓았다. 총 아이템 수는 12개다. 기존 보스 캔·집게는 배치에서 빼고 원본 에셋은 보존한다.

이번 모델은 보유 공간을 확인하기 위한 테스트 소품이다. 최종 채택할 아이템 목록·효과·사용 애니메이션·최종 모델 품질을 확정한 것은 아니다. 추후 선정한 아이템에 맞춰 모델을 교체하거나 보강한다. 세부 형태·크기·프리팹은 `Docs/Art/TestItemLayout.md`를 따른다.

## 제작 완료 모델

| 모델 | 파일 | 완료 범위 |
|---|---|---|
| 탁상 압력계 | `Assets/_COLA_COLA/Art/Prefabs/PressureGauge.prefab` | 12각 청록 몸체·이중 테두리·공개 한계 숫자·예상 폭발 확률. 현재 압력 빨간 바늘과 주황색 예측 바늘을 분리했다. 현재 메시 수와 검증은 `output/verification/latest/pressure-interaction/Scene-validation.txt` 참고. |
| 흔들기 강도 콘솔 | `Assets/_COLA_COLA/Art/Prefabs/ShakeConsole.prefab` | 청록색 경사 몸체, 금속 테두리·소켓, 크림색 1·2 키와 붉은색 3 키, 돌출 픽셀 숫자. 커스텀 메시 18개, 1,808 triangles, 재질 7개. 기존 콘솔 Cube와 버튼 Cube 3개를 교체했다. 각 키는 별도 Transform과 Collider를 갖는다. 저장본 재열기·카메라 렌더를 확인했다. |
| 체력·토큰 단말기 | `Assets/_COLA_COLA/Art/Prefabs/StatusTerminal.prefab` | 청록색 금속 외장·크림색 베젤·나사·통풍구·전원선과 분리된 CRT 화면. BOSS/PLAYER의 분절형 체력바·HP 숫자·토큰을 실제 메시로 표시한다. 표시 API와 피해/토큰 피드백 포함. 메시·삼각형 수는 `output/verification/latest/status-terminal/StatusTerminal-validation.txt` 참조. |
| 테스트 아이템 6종·서빙 쟁반 | `Assets/_COLA_COLA/Art/Prefabs/TestItems/` | 밸브·냉각팩·접힌 쿠폰·포장 사탕·팽창 뚜껑·스포이트 병을 각각 별도 메시로 제작했다. 양쪽에 같은 소품 6개씩과 실물 쟁반을 배치한다. 효과·클릭 처리는 없는 공간 확인용이다. |

압력계는 `Docs/Art/PressureGauge.md`, 콘솔과 Idle은 `Docs/Art/ShakeConsole-And-Idle.md`, 단말기는 `Docs/Art/StatusTerminal.md`에 정리했다. 압력 선택·미리보기·콜라 클릭 확정과 기본 자동 보스 응답은 `Docs/Gameplay/PressureSelection.md`, `Docs/Art/ColaShakeAnimations.md`를 따른다. 피해·토큰 연동은 아직 없고 단말기의 체력·토큰 값은 예시다.

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
| 콜라 캔 | 같은 피자리아 소품 프리팹의 `Can_Soda` | 사용 가능한 기존 모델. 현재 대결 테이블에서는 테스트 아이템 배치로 교체했다. |
| 집게 | 같은 피자리아 소품 프리팹의 `tongs` | 사용 가능한 기존 모델. 현재 대결 테이블에서는 테스트 아이템 배치로 교체했다. |

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

- **보스 동작:** 벤치에 앉고 양손을 테이블에 지지한 자세에서 30초 Idle을 적용했다. 플레이어 행동 다음 차례에 보스 원본 스킨 메시의 손으로 병을 잡고 흔들어 내려놓는다. 임시 무작위 강도 1~3을 사용하며 전술·피격·아이템 사용은 후속 작업이다.
- **플레이어 손 동작:** 원래 FPS 팔 모델과 텍스처·손가락 본을 사용한다. 추가 소매 보조 메시와 생성 코드를 제거했다. 강도 선택 후 콜라를 클릭하면 기존 오른손으로 병을 잡고 흔들어 내려놓고 보스 행동이 끝난 뒤 입력을 받는다. 버튼 누르기·아이템 사용은 후속 작업이다.
- **상태 UI:** 기존 우측 HUD 구상을 좌측의 실물 공용 단말기로 대체했다. 모델·표시 컴포넌트·체력 감소/토큰 변경 피드백은 완료했다. 실제 경기 상태를 표시 API에 전달하는 연결은 후속 작업이다.
- **상호작용과 연출:** 버튼 선택·현재/예측 바늘·예상 확률·콜라 클릭 확정에 원본 FPS 손과 병의 동기화 애니메이션을 연결했다. 보스도 같은 세 강도 동작을 갖고 실제 다음 차례에 자동으로 응답한다. 정식 폭발 이펙트·버튼 누르기·아이템 사용·피해/토큰 계산은 후속 범위다.
- **렌더 스타일:** 컨셉의 픽셀감, 낮은 해상도 텍스처, 디더링, 조명은 모델 배치만으로 완성되지 않는다. 기존 재질 적합성 및 최종 화면은 별도 렌더 확인이 필요하다.

## 확인 범위

이 문서는 컨셉 이미지·생성 프롬프트, 프로젝트 모델 파일 목록, 피자리아 프리팹의 오브젝트 이름과 씬·모델 생성 도구를 기준으로 작성했다. 메인 씬 대상 경로는 `Assets/Scenes/MainScene.unity`다. 모든 메시의 세부 형상, 동작의 자연스러움, 최종 카메라에서의 가림·비율·조명은 목록 조사만으로 검증되지 않는다. 씬 생성·배치·렌더·재생 검증의 완료 여부는 실제 씬과 검증 결과를 기준으로 판단한다.

MainScene 저장·재열기와 카메라 렌더의 기준 기록은 `output/verification/latest/scene-layout/MainScene-validation.txt`, 실제 화면은 `output/scene-layout/MainScene-preview.png`, 배치 설명은 `Docs/Art/MainScene-Layout.md`에 있다. 콘솔 제작 당시의 아이템 Cube 4개는 이번 12개 소품 배치로 교체했다. 실제 Play 모드를 31.01초 실행해 두 캐릭터의 기본 Idle 자동 시작과 30초 경계를 넘는 반복 재생을 확인한 이전 기록은 `output/verification/latest/scene-layout/Idle-playmode-validation.txt`에 있다. 30초 미리보기는 `output/scene-layout/MainScene-natural-idle-preview.gif`다. 콜라 흔들기와 입력·취소·Idle 복귀의 실제 Play 검증은 `output/verification/latest/shake-animation/Playmode-validation.txt`, 구현 설명은 `Docs/Art/ColaShakeAnimations.md`를 따른다.

단말기 적용 후 왼쪽부터 단말기·압력계·중앙 콜라 순서를 저장본에서 확인했다. 카메라·캐릭터·기존 Idle을 유지했으며 현재 배치는 `output/verification/latest/scene-layout/MainScene-layout.txt`, 단말기 렌더·바인딩·실제 Play 검증은 `output/status-terminal`의 이미지와 보고서를 따른다. 기존 Idle 미리보기는 단말기 추가 전의 제작 검증 기록이다.

테스트 아이템 12개를 배치한 최신 화면은 `output/item-layout/MainScene-Items.png`다. 아이템 개수·테이블 접촉·간격·가림 검사와 장치 입력 재검증은 `Docs/Art/TestItemLayout.md` 및 `output/item-layout`의 최종 기록을 기준으로 한다.
